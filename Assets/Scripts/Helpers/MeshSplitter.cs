using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class MeshSplitter
{
    private const int maxSplitDepth = 6;
    private const int maxTrisPerChunk = 48;


    // 为网格创建细分区块
    public static MeshChunk[] CreateChunks(Mesh mesh)
    {
        MeshChunk[] subMeshes = new MeshChunk[mesh.subMeshCount];
        
        // 获取所有网格数据
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] indices = mesh.triangles;

        // 获取原始网格的子网格信息，并创建区块
        for (int i = 0 ; i < subMeshes.Length ; i++)
        {
            // 利用子网格中的数据将顶点数组分割成多个子数组
            SubMeshDescriptor subMeshInfo = mesh.GetSubMesh(i);
            var subMeshIndices = indices.AsSpan(subMeshInfo.indexStart , subMeshInfo.indexCount);       // 利用Span托管数组可以避免对原数组的复制，从而避免内存浪费
            subMeshes[i] = CreateSubMesh(vertices , normals , subMeshIndices , i);
        }
        
        // 对初始子网格区块进行进一步的细分
        List<MeshChunk> splitChunksList = new List<MeshChunk>();
        foreach (var subMesh in subMeshes)
        {
            Split(subMesh , splitChunksList);
        }

        return splitChunksList.ToArray();
    }


    // 创建子网格的网格区块
    private static MeshChunk CreateSubMesh(Vector3[] vertices , Vector3[] normals , Span<int> indices , int subMeshIndex)
    {
        Triangle[] triangles = new Triangle[indices.Length / 3];
        
        //初始化子网格包围盒（用该子网格的首个顶点与极小边长创建）
        Bounds bounds = new Bounds(vertices[indices[0]] , Vector3.one * 0.01f);

        for (int i = 0 ; i < triangles.Length ; i++)
        {
            int a = indices[i * 3];
            int b = indices[i * 3 + 1];
            int c = indices[i * 3 + 2];

            Vector3 posA = vertices[a];
            Vector3 posB = vertices[b];
            Vector3 posC = vertices[c];

            Vector3 normalA = normals[a];
            Vector3 normalB = normals[b];
            Vector3 normalC = normals[c];
            
            // 扩充包围盒以使其包含传入点
            bounds.Encapsulate(posA);
            bounds.Encapsulate(posB);
            bounds.Encapsulate(posC);

            triangles[i] = new Triangle(posA , posB , posC , normalA , normalB , normalC);
        }

        return new MeshChunk(triangles , bounds , subMeshIndex);
    }
    
    
    // 对网格递归细分
    private static void Split(MeshChunk currentChunk , List<MeshChunk> splitChunkList , int depth = 0)
    {
        // 当 每区块三角形数达到标准 或 达到最深递归 时，将当前区块加入列表，并退出
        if (currentChunk.triangles.Length <= maxTrisPerChunk || depth >= maxSplitDepth)
        {
            splitChunkList.Add(currentChunk);
            return;
        }

        Vector3 l = currentChunk.bounds.size / 4;
        Triangle[] allTriangles = currentChunk.triangles;
        HashSet<int> takenTriangles = new HashSet<int>();
        
        // 将区块划分为八个子区块
        for (int x = -1 ; x <= 1 ; x += 2)
        {
            for (int y = -1 ; y <= 1 ; y += 2)
            {
                for (int z = -1 ; z <= 1 ; z += 2)
                {
                    int remainingTris = allTriangles.Length - takenTriangles.Count;
                    if (remainingTris > 0)
                    {
                        Vector3 splitBoundsOffset = new Vector3(l.x * x , l.y * y , l.z * z);
                        Bounds splitBounds = new Bounds(currentChunk.bounds.center + splitBoundsOffset , l * 2);

                        MeshChunk splitChunk = Extract(allTriangles , takenTriangles , splitBounds , currentChunk.subMeshIndex);
                        if (splitChunk.triangles.Length > 0)
                        {
                            Split(splitChunk , splitChunkList , depth + 1);
                        }
                    }
                }
            }
        }
    }


    // 提取子区块三角形
    private static MeshChunk Extract(Triangle[] allTriangles , HashSet<int> takenTriangles , Bounds bounds , int subMeshIndex)
    {
        List<Triangle> extractTriangles = new List<Triangle>();
        Bounds newBounds = new Bounds(bounds.center , bounds.size);

        for (int i = 0 ; i < allTriangles.Length ; i++)
        {
            // 跳过已归纳的三角形
            if (takenTriangles.Contains(i))
                continue;
            
            if (bounds.Contains(allTriangles[i].posA) || bounds.Contains(allTriangles[i].posB) || bounds.Contains(allTriangles[i].posC))
            {
                newBounds.Encapsulate(allTriangles[i].posA);
                newBounds.Encapsulate(allTriangles[i].posB);
                newBounds.Encapsulate(allTriangles[i].posC);
                
                extractTriangles.Add(allTriangles[i]);
                takenTriangles.Add(i);
            }
        }

        return new MeshChunk(extractTriangles.ToArray() , newBounds , subMeshIndex);
    }
}