Shader "Ray Tracing/RayTracingShader"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        
        Pass
        {
            CGPROGRAM

            #pragma vertex vert;
            #pragma fragment frag;
    
            #include "UnityCG.cginc"

            static const float PI = 3.14159;

            // 相机
            float3 _ViewParams;
            float4x4 _CameraLocalToWorldMatrix;

            // 光线
            int _MaxBounceCount;
            int _RayCountPerPixel;

            // 帧累积
            int _Frame;

            // 环境光
            int _EnvironmentEnabled;
            
            float3 _SunDirection;
            float _SunFocus;
            float _SunIntensity;
            
            float4 _SkyHorizonColor;
            float4 _SkyZenithColor;
            float4 _GroundColor;
    
            
            struct appdata
            {
                float4 positionOS   : POSITION;
                float2 texcoord     : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
            };

            
    
            //========== 结构体 ==========//
            struct Ray
            {
                float3 origin;
                float3 dir;
            };
            
            struct RayTracingMaterial
            {
                float4 color;
                float4 emissionColor;
                float emissionStrength;
                float smoothness;
            };

            struct Sphere
            {
                float3 position;
                float radius;
                RayTracingMaterial material;
            };

            struct Triangle
            {
                float3 posA , posB , posC;
                float3 normalA , normalB , normalC;
            };

            struct MeshInfo
            {
                uint firstTriangleIndex;
                uint triangleCount;
                float3 boxMin;
                float3 boxMax;
                RayTracingMaterial material;
            };
            
            struct HitInfo
            {
                bool didHit;
                float distance;
                float3 pos;
                float3 normal;
                RayTracingMaterial material;
            };

            

            //========== Buffer ==========//
            StructuredBuffer<Sphere> _Spheres;
            int _SphereCount;

            StructuredBuffer<Triangle> _Triangles;
            StructuredBuffer<MeshInfo> _Meshes;
            int _MeshCount;

            
            //========== Tools ==========//
            // 获取生成随机数的种子
            uint CreateRandomSeed(float2 uv)
            {
                uint2 pixelCount = _ScreenParams.xy;
                uint2 pixelCoord = uv * pixelCount;
                uint pixelIndex = pixelCoord.x * pixelCoord.y + pixelCoord.x;
                
                return pixelIndex + _Frame * 20050308;
            }
            
            // 生成 0 ~ 1 间的随机数
            float RandomValue01(inout uint seed)
            {
                // PCG (Permuted Congruential Generator)
                // www.pcg-random.org and www.shadertoy.com/view/XlGcRh
                
                seed = seed * 747796405 + 2891336453;
                uint result = ((seed >> ((seed >> 28) + 4)) ^ seed) * 277803737;
                result = (result >> 22) ^ result;
                return result / 4294967295.0;
            }
            
            // 生成标准正态分布下的随机数
            float RandomValueNormalDistribution(inout uint seed)
            {
                // Thanks to https://stackoverflow.com/a/6178290
                
                float theta = 2 * PI * RandomValue01(seed);
                float rho = sqrt(-2 * log(RandomValue01(seed)));
                return rho * cos(theta);
            }

            // 生成随机方向
            float3 RandomDirection(inout uint seed)
            {
                float x = RandomValueNormalDistribution(seed);
                float y = RandomValueNormalDistribution(seed);
                float z = RandomValueNormalDistribution(seed);
                return normalize(float3(x , y , z));
            }

            // 生成给定的法线对应半球下的随机方向
            float3 RandomHemisphereDirection(float3 normal , inout uint seed)
            {
                // 若生成方向在半球外则反转方向
                float3 dir = RandomDirection(seed);
                return dir * sign(dot(dir , normal));
            }
            
            
            
            //========== SDF ==========//
            HitInfo RaySphere(Ray ray , Sphere sphere)
            {
                HitInfo info = (HitInfo)0;
    
                // 转换至局部坐标系
                float3 offsetOrigin = ray.origin - sphere.position;
    
                
                // 当 pow(ro+rd*d , 2) = pow(r , 2) 时，射线与球的表面接触
                // 联立方程组即可得到关于 距离d 的一元二次方程:
                //      dot(rd,rd) * d*d + 2*dot(ro,rd) * d + dot(ro,ro)-r*r = 0
                // 其中：
                //      delta = b*b-4*a*c = 4*dot(ro,rd)*dot(ro,rd) - 4*dot(rd,rd)*(dot(ro,ro)-r*r)
                // 由于rd为单位向量
                // 所以：
                //      delta = 4*dot(ro,rd)*dot(ro,rd) - 4*(dot(ro,ro)-r*r)
                // 当delta大于0时，射线与球有两个交点；当delta等于0时，射线与球有且只有一个交点；当delta小于0时，射线与球无交点
                // 求解：
                //      d = (-b (+或-) sqrt(delta)) / (2*a)
                // 代入得：
                //      d = (-2*dot(ro,rd) (+或-) sqrt(4*dot(ro,rd)*dot(ro,rd) - 4*(dot(ro,ro)-r*r))) / (2*dot(rd,rd))
                //        = -dot(ro,rd) (+或-) sqrt(dot(ro,rd)*dot(ro,rd) - (dot(ro,ro)-r*r))
    
    
                float a = dot(ray.dir , ray.dir);
                float b = 2 * dot(offsetOrigin , ray.dir);
                float c = dot(offsetOrigin , offsetOrigin) - sphere.radius * sphere.radius;
    
                float delta = b * b - 4 * a * c;
                if (delta >= 0)
                {
                    float d = (-b - sqrt(delta)) / (2 * a);                     // 使用减号取两个交点中更接近射线起点的那个（只有一个交点时加减号无差异）
                    if (d >= 0)                                                 // 排除负数距离
                    {
                        info.didHit = true;
                        info.distance = d;
                        info.pos = ray.origin + ray.dir * d;                    // 世界坐标
                        info.normal = normalize(info.pos - sphere.position);
                    }
                }
                
                return info;
            }

            HitInfo RayTriangle(Ray ray , Triangle tri)
            {
                // Möller-Trumbore 算法（是啥东西（？）
                HitInfo info = (HitInfo)0;

                // 求三角面法线
                float3 edgeAB = tri.posB - tri.posA;
                float3 edgeAC = tri.posC - tri.posA;
                float3 normal = cross(edgeAB , edgeAC);

                float3 vectorAO = ray.origin - tri.posA;
                float3 crossAOD = cross(vectorAO , ray.dir);

                float determinant = -dot(ray.dir , normal);                 // 当 determinant > 0 时，射线穿过三角形正面；determinant < 0 时，射线穿过三角形背面；determinant = 0 时，射线与三角面平行
                                                                            // 出于精度问题，故认为当 |determinant| < (某一很小的数) 时，射线与三角面平行
                float inDeterminant = 1.0 / determinant;                    // 取倒数

                // 求距离及交点
                float distance = dot(vectorAO , normal) * inDeterminant;

                // 求三角形各顶点法线比重
                float u = -dot(edgeAB , crossAOD) * inDeterminant;          // 顶点 C
                float v = dot(edgeAC , crossAOD) * inDeterminant;           // 顶点 B
                float w = 1.0 - u - v;                                      // 顶点 A

                // 输出
                bool didHit = determinant >= 1e-6 && distance >= 0 && u >= 0 && v >= 0 && w >= 0;
                if (didHit)
                {
                    info.didHit = didHit;
                    info.distance = distance;
                    info.pos = ray.origin + ray.dir * distance;
                    info.normal = normalize(tri.normalA * w + tri.normalB * v + tri.normalC * u);
                }
                
                return info;
            }

            bool RayBounds(Ray ray , float3 boxMin , float3 boxMax , out float tEnter , out float tExit)
            {
                float3 inDir = 1.0 / ray.dir;
                
                float3 tMin = (boxMin - ray.origin) * inDir;
                float3 tMax = (boxMax - ray.origin) * inDir;

                float3 t1 = min(tMin , tMax);
                float3 t2 = max(tMin , tMax);

                tEnter = max(max(t1.x , t1.y) , t1.z);
                tExit = min(min(t2.x , t2.y) , t2.z);
                
                return tExit >= 0 && tEnter <= tExit;
            }

            HitInfo CalculateRayCollision(Ray ray)
            {
                HitInfo closestHit = (HitInfo)0;
                closestHit.distance = 1.#INF;                   // 设置初始距离为无限远

                for (int i=0 ; i<_SphereCount ; i++)
                {
                    Sphere sphere = _Spheres[i];
                    HitInfo info = RaySphere(ray , sphere);

                    if (info.didHit && info.distance < closestHit.distance)
                    {
                        closestHit = info;
                        closestHit.material = sphere.material;
                    }
                }

                for (int i=0 ; i<_MeshCount ; i++)
                {
                    MeshInfo meshInfo = _Meshes[i];
                    float tEnter , tExit;

                    // 当 射线未触碰包围盒 或 射线穿入包围盒交点距离大于最近距离（该包围盒在别的物体后方） 时，跳过该网格
                    if (!RayBounds(ray , meshInfo.boxMin , meshInfo.boxMax , tEnter , tExit) || tEnter > closestHit.distance)
                        continue;
                    
                    for (uint j=0 ; j<meshInfo.triangleCount ; j++)
                    {
                        int index = meshInfo.firstTriangleIndex + j;
                        HitInfo info = RayTriangle(ray , _Triangles[index]);

                        if (info.didHit && info.distance < closestHit.distance)
                        {
                            closestHit = info;
                            closestHit.material = meshInfo.material;
                        }
                    }
                }

                return closestHit;
            }


            
            //========== 环境光 ==========//
            float3 GetEnvironmentLight(Ray ray)
            {
                if (!_EnvironmentEnabled)
                    return 0;
                
                float t = smoothstep(0.0 , 0.4 , ray.dir.y);
                float3 skyColor = lerp(_SkyHorizonColor , _SkyZenithColor , t);
                float sun = pow(max(0 , dot(ray.dir , -_SunDirection)) , _SunFocus) * _SunIntensity;

                float groundToSky = smoothstep(-0.01 , 0 , ray.dir.y);
                float sunMask = groundToSky >= 1;
                return lerp(_GroundColor , skyColor , groundToSky) + sun * sunMask;
            }
            
            
            //========== Trace ==========//
            float3 Trace(Ray ray , inout uint seed)
            {
                float3 incomingLight = 0;                                                    // 初始化光源颜色
                float3 rayColor = 1;                                                         
                                                                                             
                for (int i=0 ; i<=_MaxBounceCount ; i++)                                     
                {                                                                            
                    HitInfo info = CalculateRayCollision(ray);                               
                                                                                             
                    if (info.didHit)                                                         
                    {                                                                        
                        ray.origin = info.pos;
                        
                        float3 diffuseDir = normalize(info.normal + RandomDirection(seed));
                        float3 specularDir = normalize(reflect(ray.dir , info.normal));
                        ray.dir = lerp(diffuseDir , specularDir , info.material.smoothness);
                                                                                             
                        RayTracingMaterial mat = info.material;                              
                        float3 emittedLight = mat.emissionColor * mat.emissionStrength;      // 获取自发光信息
                        incomingLight += emittedLight * rayColor;

                        rayColor *= mat.color;
                    }
                    else
                    {
                        incomingLight += GetEnvironmentLight(ray) * rayColor;
                        break; 
                    }
                }

                return incomingLight;
            }

            
            
            //========== 着色器 ==========//
            v2f vert(appdata input)
            {
                v2f output;
                
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.texcoord;
                
                return output;
            }
            
            float4 frag(v2f input) : SV_Target
            {
                // 获取随机数种
                uint seed = CreateRandomSeed(input.uv);

                
                // 生成光线
                float3 viewPointLocal = float3(input.uv - 0.5 , 1.0) * _ViewParams;                      // 获取当前屏幕坐标在相机空间下的相对坐标（取屏幕中心点为原点）
                float3 viewPoint = mul(_CameraLocalToWorldMatrix , float4(viewPointLocal , 1.0));
                
                Ray ray;
                ray.origin = _WorldSpaceCameraPos;
                ray.dir = normalize(viewPoint - ray.origin);

                
                // 光线追踪
                float3 totalIncomingLights = 0;
                for (int i=0 ; i<_RayCountPerPixel ; i++)
                {
                    totalIncomingLights += Trace(ray , seed);
                }
                float3 pixelColor = totalIncomingLights / _RayCountPerPixel;
                
                return float4(pixelColor , 1);
            }





            //========== 趣味小函数 XD ==========//
            float4 RandomColorForScreen(float2 uv)
            {
                uint seed = CreateRandomSeed(uv);
                
                float r = RandomValue01(seed);
                float g = RandomValue01(seed);
                float b = RandomValue01(seed);
                
                return float4(r , g , b , 1);
            }
    
            ENDCG
        }
    }
}