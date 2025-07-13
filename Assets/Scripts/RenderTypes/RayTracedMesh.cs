using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class RayTracedMesh : RayTracedObject
{
    private MeshFilter meshFilter;
    
    [Header("Properties")] 
    [SerializeField] private int triangleCount;
    [SerializeField] private int verticeCount;

    [Header("Mesh Chunk")]
    [SerializeField] private Mesh mesh;
    [SerializeField] private MeshChunk[] localChunks;       // 物体坐标系下的区块数组
    private MeshChunk[] worldChunks;                        // 世界坐标系下的区块数组
    
    

    public MeshInfo GetMeshInfo(ref List<Triangle> currentTriangles)
    {
        int firstTriangleIndex = currentTriangles.Count;
        
        // 获取数据
        Vector3[] vertices = meshFilter.sharedMesh.vertices;
        Vector3[] normals = meshFilter.sharedMesh.normals;
        int[] indices = meshFilter.sharedMesh.triangles;

        // 存储转换后的顶点与法线
        Vector3[] verticesAfterTrans = new Vector3[vertices.Length];
        Vector3[] normalsAfterTrans = new Vector3[normals.Length];
        
        // 转换矩阵
        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        Matrix4x4 normalMatrix = transform.worldToLocalMatrix.transpose;    // 对法线进行非均匀缩放时会使其被破坏，故需要使用逆转置矩阵来变换
        
        // 包围盒
        Vector3 boundsMin = localToWorld.MultiplyPoint3x4(vertices[indices[0]]);
        Vector3 boundsMax = boundsMin;

        // 转换所有顶点及法线
        for (int i = 0 ; i < vertices.Length ; i++)
        {
            verticesAfterTrans[i] = localToWorld.MultiplyPoint3x4(vertices[i]);
            
            // 重计算包围盒
            boundsMin = Vector3.Min(boundsMin , verticesAfterTrans[i]);
            boundsMax = Vector3.Max(boundsMax , verticesAfterTrans[i]);
        }
        for (int i = 0 ; i < normals.Length ; i++)
        {
            normalsAfterTrans[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
        }
        
        // 构造三角形
        List<Triangle> triangles = new List<Triangle>();
        for (int i = 0 ; i < indices.Length / 3 ; i++)
        {
            int a = indices[i * 3];
            int b = indices[i * 3 + 1];
            int c = indices[i * 3 + 2];
            
            triangles.Add(new Triangle(verticesAfterTrans[a] , verticesAfterTrans[b] , verticesAfterTrans[c] ,
                                       normalsAfterTrans[a] , normalsAfterTrans[b] , normalsAfterTrans[c]));
        }

        Bounds bounds = new Bounds((boundsMax + boundsMin) / 2 , boundsMax - boundsMin);
        
        currentTriangles.AddRange(triangles);
        return new MeshInfo(firstTriangleIndex , triangles.Count , bounds , material);
    }

    

    // 获取细分后的子网格列表
    public MeshChunk[] GetMeshChunks()
    {
        // 网格面数限制
        if (mesh.triangles.Length / 3 > RayTracingManager.trianglesPerMeshLimit)
        {
            throw new Exception($"请使用三角形面数少于 {RayTracingManager.trianglesPerMeshLimit} 的网格");
        }
        
        // 若 当前没有缓存网格细分区块 或 当前网格与缓存网格不同 时，将网格细分
        if (meshFilter != null && (mesh != meshFilter.sharedMesh || localChunks == null || localChunks.Length == 0))
        {
            mesh = meshFilter.sharedMesh;
            localChunks = MeshSplitter.CreateChunks(mesh);
        }

        // 对世界坐标系下的区块数组同步更改
        if (worldChunks == null || worldChunks.Length != localChunks.Length)
        {
            worldChunks = new MeshChunk[localChunks.Length];
        }
        
        // 转换坐标系
        Matrix4x4 posMatrix = transform.localToWorldMatrix;
        Matrix4x4 normalMatrix = transform.worldToLocalMatrix.transpose;    // 对法线进行非均匀缩放时会使其被破坏，故需要使用逆转置矩阵来变换

        for (int i = 0 ; i < worldChunks.Length ; i++)
        {
            MeshChunk localChunk = localChunks[i];
            
            // 当 世界坐标系区块不存在 或 待更新 时，才重复创建实例
            if (worldChunks[i] == null || worldChunks[i].triangles.Length != localChunk.triangles.Length)
            {
                worldChunks[i] = new MeshChunk(new Triangle[localChunk.triangles.Length] ,
                                               localChunk.bounds ,                                          // 该处包围盒只是借来暂时占位，实际上需要转换坐标系
                                               localChunk.subMeshIndex);
            }
            TransformLocalToWorldChunkCPU(worldChunks[i] , localChunk , posMatrix , normalMatrix);
        }

        return worldChunks;
    }
    
    private void TransformLocalToWorldChunkCPU(MeshChunk worldChunk , MeshChunk localChunk , Matrix4x4 posMatrix , Matrix4x4 normalMatrix)
    {
        Triangle[] localTris = localChunk.triangles;

        // 初始化包围盒
        Vector3 boundsMin = posMatrix.MultiplyPoint3x4(localTris[0].posA);
        Vector3 boundsMax = boundsMin;

        for (int i = 0 ; i < localTris.Length ; i++)
        {
            Vector3 worldPosA = posMatrix.MultiplyPoint3x4(localTris[i].posA);
            Vector3 worldPosB = posMatrix.MultiplyPoint3x4(localTris[i].posB);
            Vector3 worldPosC = posMatrix.MultiplyPoint3x4(localTris[i].posC);

            Vector3 worldNormalA = normalMatrix.MultiplyVector(localTris[i].normalA).normalized;
            Vector3 worldNormalB = normalMatrix.MultiplyVector(localTris[i].normalB).normalized;
            Vector3 worldNormalC = normalMatrix.MultiplyVector(localTris[i].normalC).normalized;

            worldChunk.triangles[i] = new Triangle(worldPosA , worldPosB , worldPosC , worldNormalA , worldNormalB , worldNormalC);
            
            // 更新包围盒
            boundsMin = Vector3.Min(boundsMin , worldPosA);
            boundsMin = Vector3.Min(boundsMin , worldPosB);
            boundsMin = Vector3.Min(boundsMin , worldPosC);
            boundsMax = Vector3.Max(boundsMax , worldPosA);
            boundsMax = Vector3.Max(boundsMax , worldPosB);
            boundsMax = Vector3.Max(boundsMax , worldPosC);
        }

        worldChunk.bounds = new Bounds((boundsMax + boundsMin) / 2 , boundsMax - boundsMin);
        worldChunk.subMeshIndex = localChunk.subMeshIndex;
    }

    private void TransformLocalToWorldChunkGPU(MeshChunk worldChunk , MeshChunk localChunk , Matrix4x4 posMatrix , Matrix4x4 normalMatrix)
    {
        
    }


    public RayTracingMaterial GetMaterial(int subMeshIndex)
    {
        return material;
    }
    
    

    protected override void OnValidate()
    {
        base.OnValidate();

        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        mesh = meshFilter.sharedMesh;
        
        triangleCount = meshFilter.sharedMesh.triangles.Length / 3;
        verticeCount = meshFilter.sharedMesh.vertices.Length;
    }

    private void OnEnable()
    {
        RayTracingManager.instance ? .RegisterMesh(this);
    }

    private void OnDisable()
    {
        RayTracingManager.instance ? .UnregisterMesh(this);
    }
}