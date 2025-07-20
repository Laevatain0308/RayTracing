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

            // 景深
            int _DepthOfFieldEnabled;
            float _DefocusStrength;
            float _DivergeStrength;
    
            
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
                float metallic;
                float4 specularColor;
            };

            struct Triangle
            {
                float3 posA , posB , posC;
                float3 normalA , normalB , normalC;
            };

            struct BVHNode
            {
                float3 boundsMin;
                float3 boundsMax;
                int triangleIndex;
                int triangleCount;
                int childIndex;
            };

            struct MeshInfo
            {
                int triangleOffset;
                int nodeOffset;
                float4x4 localToWorldMatrix;
                float4x4 worldToLocalMatrix;
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
            StructuredBuffer<Triangle> _Triangles;
            StructuredBuffer<BVHNode> _Nodes;
            StructuredBuffer<MeshInfo> _MeshInfos;
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
            
            // 生成单位圆内的随机点
            float2 RandomPointInCircle(inout uint seed)
            {
                float angle = RandomValue01(seed) * 2 * PI;
                float2 pointOnCircle = float2(cos(angle) , sin(angle));
                return pointOnCircle * sqrt(RandomValue01(seed));
            }

            
            
            //========== Ray Collision ==========//
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

            // 当命中包围盒时，返回命中点距离，否则返回无限大
            float RayBounds(Ray ray , float3 boxMin , float3 boxMax)
            {
                float3 inDir = 1.0 / ray.dir;
                
                float3 tMin = (boxMin - ray.origin) * inDir;
                float3 tMax = (boxMax - ray.origin) * inDir;

                float3 t1 = min(tMin , tMax);
                float3 t2 = max(tMin , tMax);

                float tEnter = max(max(t1.x , t1.y) , t1.z);
                float tExit = min(min(t2.x , t2.y) , t2.z);

                bool didHit = tExit >= 0 && tEnter <= tExit;
                
                return didHit ? tEnter : 1.#INF;
            }

            HitInfo RayModel(Ray ray , int nodeOffset , int triangleOffset)
            {
                HitInfo closestHit = (HitInfo)0;
                closestHit.distance = 1.#INF;

                // 节点栈
                int nodeIndexStack[32];                                                 // 栈大小为 BVH 最大深度
                int stackIndex = 0;
                nodeIndexStack[stackIndex++] = nodeOffset;                              // 压入根节点

                // 循环递归
                while (stackIndex > 0)
                {
                    BVHNode node = _Nodes[nodeIndexStack[--stackIndex]];
                    
                    // 若为叶子节点则进行三角形检测
                    if (node.childIndex < 0)
                    {
                        int triIndexStart = triangleOffset + node.triangleIndex;
                        
                        for (int i=triIndexStart ; i<triIndexStart + node.triangleCount ; i++)
                        {
                            Triangle tri = _Triangles[i];
                            HitInfo info = RayTriangle(ray , tri);

                            if (info.didHit && info.distance < closestHit.distance)
                            {
                                closestHit = info;
                            }
                        }
                    }
                    else
                    {
                        // 若不为叶子节点则检测包围盒
                        BVHNode nodeA = _Nodes[nodeOffset + node.childIndex];
                        BVHNode nodeB = _Nodes[nodeOffset + node.childIndex + 1];
                        
                        float distanceA = RayBounds(ray , nodeA.boundsMin , nodeA.boundsMax);
                        float distanceB = RayBounds(ray , nodeB.boundsMin , nodeB.boundsMax);

                        // 将距离相机更近的包围盒后压入栈顶，优先递归
                        if (distanceA > distanceB)
                        {
                            if (distanceA < closestHit.distance)
                                nodeIndexStack[stackIndex++] = nodeOffset + node.childIndex;

                            if (distanceB < closestHit.distance)
                                nodeIndexStack[stackIndex++] = nodeOffset + node.childIndex + 1;
                        }
                        else
                        {
                            if (distanceB < closestHit.distance)
                                nodeIndexStack[stackIndex++] = nodeOffset + node.childIndex + 1;
                            
                            if (distanceA < closestHit.distance)
                                nodeIndexStack[stackIndex++] = nodeOffset + node.childIndex;
                        }
                    }
                    
                }
                
                return closestHit;
            }

            HitInfo CalculateRayCollision(Ray ray)
            {
                HitInfo closestInfo = (HitInfo)0;
                closestInfo.distance = 1.#INF;

                for (int i=0 ; i<_MeshCount ; i++)
                {
                    MeshInfo mesh = _MeshInfos[i];
                    
                    Ray localRay;
                    localRay.origin = mul(mesh.worldToLocalMatrix , float4(ray.origin , 1));
                    localRay.dir = normalize(mul(mesh.worldToLocalMatrix , float4(ray.dir , 0)));
                    
                    HitInfo localInfo = RayModel(localRay , mesh.nodeOffset , mesh.triangleOffset);

                    if (localInfo.didHit && localInfo.distance < closestInfo.distance)
                    {
                        closestInfo = localInfo;
                        closestInfo.pos = ray.origin + ray.dir * localInfo.distance;
                        closestInfo.normal = normalize(mul(mesh.localToWorldMatrix , float4(localInfo.normal , 0)));
                        closestInfo.material = mesh.material;
                    }
                }

                return closestInfo;
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
                        RayTracingMaterial mat = info.material;
                        
                        ray.origin = info.pos;
                        
                        float3 diffuseDir = normalize(info.normal + RandomDirection(seed));
                        float3 specularDir = normalize(reflect(ray.dir , info.normal));

                        bool isSpecularBounce = mat.metallic >= RandomValue01(seed);
                        
                        ray.dir = lerp(diffuseDir , specularDir , mat.smoothness * isSpecularBounce);
                                                                                             
                        float3 emittedLight = mat.emissionColor * mat.emissionStrength;      // 获取自发光信息
                        incomingLight += emittedLight * rayColor;

                        rayColor *= lerp(mat.color , mat.specularColor , isSpecularBounce);
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
                
                
                float3 viewPointLocal = float3(input.uv - 0.5 , 1.0) * _ViewParams;                      // 获取当前屏幕坐标在相机空间下的相对坐标（取屏幕中心点为原点）
                float3 viewPoint = mul(_CameraLocalToWorldMatrix , float4(viewPointLocal , 1.0));

                float3 cameraRight = _CameraLocalToWorldMatrix._m00_m10_m20;
                float3 cameraUp = _CameraLocalToWorldMatrix._m01_m11_m21;

                
                // float3 totalIncomingLights = 0;
                // for (int i=0 ; i<_RayCountPerPixel ; i++)
                // {
                //     // 生成光线
                //     Ray ray;
                //
                //     if (_DepthOfFieldEnabled)
                //     {
                //         // 附加景深
                //         float2 defocusJitter = RandomPointInCircle(seed) * _DefocusStrength / _ScreenParams.x;
                //         ray.origin = _WorldSpaceCameraPos + cameraRight * defocusJitter.x + cameraUp * defocusJitter.y;
                //     }
                //     else
                //     {
                //         ray.origin = _WorldSpaceCameraPos;
                //     }
                //
                //     // 附加模糊以处理抗锯齿
                //     float2 jitter = RandomPointInCircle(seed) * _DivergeStrength / _ScreenParams.x;
                //     float3 jitterViewPoint = viewPoint + cameraRight * jitter.x + cameraUp * jitter.y;
                //     
                //     ray.dir = normalize(jitterViewPoint - ray.origin);
                //
                //     
                //     // 光线追踪
                //     totalIncomingLights += Trace(ray , seed);
                // }
                // float3 pixelColor = totalIncomingLights / _RayCountPerPixel;
                //
                //
                // return float4(pixelColor , 1);

                
                Ray ray;
                ray.origin = _WorldSpaceCameraPos;
                ray.dir = normalize(viewPoint - ray.origin);

                HitInfo info = CalculateRayCollision(ray);
                
                return float4((info.normal * 0.5 + 0.5) * info.didHit , 1);
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