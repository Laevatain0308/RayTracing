using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways , ImageEffectAllowedInSceneView]
public class RayTracingManager : MonoBehaviour
{
    public static RayTracingManager instance;
    
    
    public const int trianglesPerMeshLimit = 1500;
    
    
    [Header("Toggle")]
    [SerializeField] private bool useShaderInSceneView;
    
    
    [Header("Reference")]
    [SerializeField] private Shader rayTracingShader;
    [SerializeField] private Shader frameAccumulator;
    public ComputeShader transformTriangles;
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
    [SerializeField] private Color skyHorizonColor;             // 地平线颜色 
    [SerializeField] private Color skyZenithColor;              // 天顶颜色
    [SerializeField] private Color groundColor;


    [Header("Ray Traced Objects")]
    [SerializeField] private List<RayTracedSphere> sphereObjects;
    [SerializeField] private List<RayTracedMesh> meshObjects;

    [Space]
    [SerializeField] private int meshChunkCount;
    [SerializeField] private int triangleCount;
    
    private List<Sphere> spheres;
    private ComputeBuffer sphereBuffer;

    private List<Triangle> triangles;
    private List<MeshInfo> meshInfos;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer meshBuffer;

    
    private RenderTexture resultRT;
    private int frameCount;
    
    
    
    private void Start()
    {
        frameCount = 0;
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
        
        // 更新光追物体
        UpdateSpheres();
        UpdateMeshes();
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
    }


    private void UpdateCameraParam(Camera cam)
    {
        float screenPlaneHeight = cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2;
        float screenPlaneWidth = screenPlaneHeight * cam.aspect;

        rayTracingMaterial.SetVector("_ViewParams" , new Vector3(screenPlaneWidth , screenPlaneHeight , cam.nearClipPlane));
        rayTracingMaterial.SetMatrix("_CameraLocalToWorldMatrix" , cam.transform.localToWorldMatrix);
    }


    private void UpdateSpheres()
    {
        sphereObjects ??= new List<RayTracedSphere>();
        
        spheres ??= new List<Sphere>();
        spheres.Clear();

        for (int i = 0 ; i < sphereObjects.Count ; i++)
        {
            RayTracedSphere s = sphereObjects[i];
            spheres.Add(new Sphere(s.GetPosition() , s.GetRadius() , s.material));
        }
        
        ShaderHelper.CreateStructuredBuffer<Sphere>(ref sphereBuffer , spheres);
        rayTracingMaterial.SetBuffer("_Spheres" , sphereBuffer);
        rayTracingMaterial.SetInt("_SphereCount" , sphereObjects.Count);
    }

    private void UpdateMeshes()
    {
        meshObjects ??= new List<RayTracedMesh>();
        
        triangles ??= new List<Triangle>();
        meshInfos ??= new List<MeshInfo>();
        triangles.Clear();
        meshInfos.Clear();

        for (int i = 0 ; i < meshObjects.Count ; i++)
        {
            MeshChunk[] chunks = meshObjects[i].GetMeshChunks();
            foreach (var chunk in chunks)
            {
                RayTracingMaterial mat = meshObjects[i].GetMaterial(chunk.subMeshIndex);
                meshInfos.Add(new MeshInfo(triangles.Count , chunk.triangles.Length , chunk.bounds , mat));
                triangles.AddRange(chunk.triangles);
            }
        }

        meshChunkCount = meshInfos.Count;
        triangleCount = triangles.Count;

        ShaderHelper.CreateStructuredBuffer<Triangle>(ref triangleBuffer , triangles);
        ShaderHelper.CreateStructuredBuffer<MeshInfo>(ref meshBuffer , meshInfos);
        rayTracingMaterial.SetBuffer("_Triangles" , triangleBuffer);
        rayTracingMaterial.SetBuffer("_Meshes" , meshBuffer);
        rayTracingMaterial.SetInt("_MeshCount" , meshInfos.Count);
    }


    
    //========== Ray Traced Objects ==========//
    public void RegisterSphere(RayTracedSphere s)
    {
        if (!sphereObjects.Contains(s))
            sphereObjects.Add(s);
    }
    public void UnregisterSphere(RayTracedSphere s) => sphereObjects.Remove(s);

    public void RegisterMesh(RayTracedMesh m)
    {
        if (!meshObjects.Contains(m))
            meshObjects.Add(m);
    }
    public void UnregisterMesh(RayTracedMesh m) => meshObjects.Remove(m);


    private void OnEnable()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void OnDisable()
    {
        ShaderHelper.Release(sphereBuffer , triangleBuffer , meshBuffer);
        ShaderHelper.Release(resultRT);

        instance = null;
    }
}
