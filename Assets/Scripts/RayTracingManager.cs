using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways , ImageEffectAllowedInSceneView]
public class RayTracingManager : MonoBehaviour
{
    public static RayTracingManager instance;
    
    
    [Header("Toggle")]
    [SerializeField] private bool useShaderInSceneView;
    
    
    [Header("Reference")]
    [SerializeField] private Shader rayTracingShader;
    [SerializeField] private Shader frameAccumulator;
    private Material rayTracingMaterial;
    private Material frameAccumulatorMaterial;
    
    
    [Header("Settings")]
    [SerializeField] private int maxBounceCount;
    [SerializeField] private int rayCountPerPixel;


    [Header("Environment")] 
    [SerializeField] private bool environmentEnabled;
    [SerializeField] private Light sun;
    [SerializeField] private float sunFocus;
    [SerializeField] private float sunIntensity;
    [SerializeField] private Color skyHorizonColor;                     // 地平线颜色 
    [SerializeField] private Color skyZenithColor;                      // 天顶颜色
    [SerializeField] private Color groundColor;


    [Header("Depth of Field")] 
    [SerializeField] private bool depthOfFieldEnabled;
    [SerializeField , Min(0.01f)] private float focusDistance;          // 焦距
    [SerializeField , Min(0)] private float defocusStrength;            // 离焦程度
    [SerializeField , Min(0)] private float divergeStrength;            // 光线发散程度（模糊程度）

    [Space] 
    [SerializeField] private bool gizmosDisplay;
    [SerializeField] private Mesh gizmosPlaneMesh;
    [SerializeField] private Color gizmosColor;


    [Header("Ray Traced Objects")]
    [SerializeField] private List<RayTracedModel> models;
    private RenderData data;
    private bool hasBVH;
    
    
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer nodeBuffer;
    private ComputeBuffer meshInfoBuffer;

    
    private RenderTexture resultRT;
    private int frameCount;
    
    
    
    private void Start()
    {
        frameCount = 0;
        hasBVH = false;
    }


    
    //========== Ray Tracing ==========//
    private void OnRenderImage(RenderTexture src , RenderTexture target)
    {
        bool isSceneCamera = Camera.current.name == "SceneCamera";
        
        if (isSceneCamera)
        {
            if (useShaderInSceneView)
            {
                InitFrame();
                Graphics.Blit(null , target , rayTracingMaterial);
            }
            else
            {
                Graphics.Blit(src , target);
            }
        }
        else
        {
            if (frameCount == 0)
                InitFrame();

            // 存储上一帧
            RenderTexture preFrameRT = RenderTexture.GetTemporary(src.width , src.height , 0 , ShaderHelper.RGBA_SFloat);
            Graphics.Blit(resultRT , preFrameRT);
            
            // 绘制当前帧
            rayTracingMaterial.SetInt("_Frame" , frameCount);
            RenderTexture currentFrameRT = RenderTexture.GetTemporary(src.width , src.height , 0 , ShaderHelper.RGBA_SFloat);
            Graphics.Blit(null , currentFrameRT , rayTracingMaterial);
            
            // 叠加上一帧与当前帧
            frameAccumulatorMaterial.SetInt("_Frame" , frameCount);
            frameAccumulatorMaterial.SetTexture("_PreFrameTex" , preFrameRT);
            Graphics.Blit(currentFrameRT , resultRT , frameAccumulatorMaterial);
            
            // 输出结果
            Graphics.Blit(resultRT , target);
            
            // 释放 RenderTexture
            RenderTexture.ReleaseTemporary(preFrameRT);
            RenderTexture.ReleaseTemporary(currentFrameRT);
            
            frameCount += Application.isPlaying ? 1 : 0;
        }
    }


    private void InitFrame()
    {
        // 初始化后处理材质
        ShaderHelper.InitMaterial(rayTracingShader , ref rayTracingMaterial);
        ShaderHelper.InitMaterial(frameAccumulator , ref frameAccumulatorMaterial);
        
        // 初始化 RenderTexture
        ShaderHelper.CreateRenderTexture(ref resultRT , Screen.width , Screen.height , FilterMode.Bilinear ,
                                         ShaderHelper.RGBA_SFloat , "Result");
        
        // 更新参数
        SetRayTracingParams();
        UpdateCameraParam(Camera.current);
        
        
        // 创建BVH
        if (!hasBVH)
        {
            hasBVH = true;
            data = CreateRenderData(models);
            SendMeshToShader();
        }
    }


    private void SetRayTracingParams()
    {
        // 光线
        rayTracingMaterial.SetInt("_MaxBounceCount" , maxBounceCount);
        rayTracingMaterial.SetInt("_RayCountPerPixel" , rayCountPerPixel);
        
        // 环境光
        rayTracingMaterial.SetInt("_EnvironmentEnabled" , environmentEnabled ? 1 : 0);
        
        rayTracingMaterial.SetVector("_SunDirection" , sun.transform.forward);
        rayTracingMaterial.SetFloat("_SunFocus" , sunFocus);
        rayTracingMaterial.SetFloat("_SunIntensity" , sunIntensity);
        
        rayTracingMaterial.SetColor("_SkyHorizonColor" , skyHorizonColor);
        rayTracingMaterial.SetColor("_SkyZenithColor" , skyZenithColor);
        rayTracingMaterial.SetColor("_GroundColor" , groundColor);
        
        // 景深
        rayTracingMaterial.SetInt("_DepthOfFieldEnabled" , depthOfFieldEnabled ? 1 : 0);
        rayTracingMaterial.SetFloat("_DefocusStrength" , defocusStrength);
        rayTracingMaterial.SetFloat("_DivergeStrength" , divergeStrength);
    }


    private void UpdateCameraParam(Camera cam)
    {
        float distanceToCam = focusDistance > 0 ? focusDistance : cam.nearClipPlane;
        
        float screenPlaneHeight = distanceToCam * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2;
        float screenPlaneWidth = screenPlaneHeight * cam.aspect;

        rayTracingMaterial.SetVector("_ViewParams" , new Vector3(screenPlaneWidth , screenPlaneHeight , distanceToCam));
        rayTracingMaterial.SetMatrix("_CameraLocalToWorldMatrix" , cam.transform.localToWorldMatrix);
    }
    
    

    
    //========== Ray Traced Objects ==========//
    public void RegisterModel(RayTracedModel model)
    {
        if (!models.Contains(model))
        {
            models.Add(model);
        }
    }

    public void UnregisterModel(RayTracedModel model)
    {
        models.Remove(model);
    }


    private RenderData CreateRenderData(List<RayTracedModel> models)
    {
        RenderData data = new RenderData();
        Dictionary<Mesh , (int triangleOffset , int nodeOffset)> sharedMeshDict = new();
            
        foreach (var model in models)
        {
            if (!sharedMeshDict.ContainsKey(model.mesh))
            {
                sharedMeshDict.Add(model.mesh , (data.triangles.Count , data.nodes.Count));
                    
                BVHInformationUI.instance ? .StartBuild();
                BVH bvh = new BVH(model.mesh.vertices , model.mesh.normals , model.mesh.triangles);
                BVHInformationUI.instance ? .SuccessBuild();
                
                data.triangles.AddRange(bvh.allTriangles);
                data.nodes.AddRange(bvh.allNodes.nodes);
            }
            
            data.meshInfos.Add(new MeshInfo()
            {
                triangleOffset = sharedMeshDict[model.mesh].triangleOffset ,
                nodeOffset = sharedMeshDict[model.mesh].nodeOffset ,
                localToWorldMatrix = model.transform.localToWorldMatrix ,
                worldToLocalMatrix = model.transform.worldToLocalMatrix ,
                material = model.material
            });
        }
        
        BVHInformationUI.instance ? .UpdateInformation(data);

        return data;
    }

    private void SendMeshToShader()
    {
        ShaderHelper.CreateStructuredBuffer<Triangle>(ref triangleBuffer , data.triangles);
        ShaderHelper.CreateStructuredBuffer<BVHNode>(ref nodeBuffer , data.nodes);
        ShaderHelper.CreateStructuredBuffer<MeshInfo>(ref meshInfoBuffer , data.meshInfos);
            
        rayTracingMaterial.SetBuffer("_Triangles" , triangleBuffer);
        rayTracingMaterial.SetBuffer("_Nodes" , nodeBuffer);
        rayTracingMaterial.SetBuffer("_MeshInfos" , meshInfoBuffer);
        rayTracingMaterial.SetInt("_MeshCount" , data.meshInfos.Count);
    }
    
    

    private void OnEnable()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void OnDisable()
    {
        ShaderHelper.Release(triangleBuffer , nodeBuffer , meshInfoBuffer);
        ShaderHelper.Release(resultRT);
        
        instance = null;
    }


    
    private void OnDrawGizmosSelected()
    {
        if (gizmosPlaneMesh != null && gizmosDisplay)
        {
            Gizmos.color = gizmosColor;
            
            Camera cam = Camera.current;
            Vector3 pos = cam.transform.position + cam.transform.forward * focusDistance;
            Gizmos.DrawMesh(gizmosPlaneMesh , pos , Quaternion.LookRotation(cam.transform.up , -cam.transform.forward));
        }
    }
}
