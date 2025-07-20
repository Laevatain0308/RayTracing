using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BVH
{
    public const int MAX_DEPTH = 32;
    
    public readonly BVHNode root;
    public readonly List<BVHNode> allNodes = new List<BVHNode>();
    public List<Triangle> allTriangles = new List<Triangle>();
    

    public BVH(Vector3[] vertices , Vector3[] normals , int[] indices)
    {
        // 创建包围盒
        BoundingBox bounds = new BoundingBox();
        foreach (var vertice in vertices)
        {
            bounds.GrowToInclude(vertice , vertice);
        }
        
        // 创建三角形列表
        int triangleCount = indices.Length / 3;
        for (int i = 0 ; i < triangleCount ; i++)
        {
            int a = indices[i * 3];
            int b = indices[i * 3 + 1];
            int c = indices[i * 3 + 2];

            Triangle tri = new Triangle(vertices[a] , vertices[b] , vertices[c] ,
                                        normals[a] , normals[b] , normals[c]);
            allTriangles.Add(tri);
        }
        
        // 创建根节点
        root = new BVHNode(bounds , 0 , allTriangles.Count);
        allNodes.Add(root);
        Split(0);
    }


    private void Split(int nodeIndex , int depth = 0)
    {
        if (depth == MAX_DEPTH || allNodes[nodeIndex].triangleCount <= 1)
            return;
        

        // 获取父节点，并更新其子节点索引
        BVHNode parent = allNodes[nodeIndex];
        parent.childIndex = allNodes.Count;

        
        // 创建包围盒
        BoundingBox boxA = new BoundingBox();
        BoundingBox boxB = new BoundingBox();


        // 子节点 A 三角形统计量
        int triangleInBoxACount = 0;

        
        // 分配三角形至子节点包围盒
        for (int i = parent.triangleIndex ; i < parent.triangleIndex + parent.triangleCount ; i++)
        {
            Triangle tri = allTriangles[i];
            
            bool inChildA = IsTriangleInChildA(allTriangles[i] , parent);

            if (inChildA)
            {
                boxA.GrowToInclude(tri);
                triangleInBoxACount++;
                
                // 对两个子节点的三角形索引进行重排序
                int swap = parent.triangleIndex + triangleInBoxACount - 1;
                (allTriangles[i] , allTriangles[swap]) = (allTriangles[swap] , allTriangles[i]);
            }
            else
            {
                boxB.GrowToInclude(tri);
            }
        }

        
        // 创建子节点
        allNodes.Add(new BVHNode(boxA , parent.triangleIndex , triangleInBoxACount));
        allNodes.Add(new BVHNode(boxB , parent.triangleIndex + triangleInBoxACount , parent.triangleCount - triangleInBoxACount));
        
        
        // 更新父节点
        allNodes[nodeIndex] = parent;

        
        // Debug.Log("Node Index: " + nodeIndex);
        // Debug.Log("     Bounds: " + allNodes[nodeIndex].boundsMin + " " + allNodes[nodeIndex].boundsMax);
        // Debug.Log("     Child Index: " + allNodes[nodeIndex].childIndex);
        // Debug.Log("     Triangle: " + allNodes[nodeIndex].triangleIndex + " " + allNodes[nodeIndex].triangleCount);

        
        // 当子节点中有一个的三角形数为0时，停止递归（代表该节点无法再分割）
        if (triangleInBoxACount == 0 || parent.triangleCount - triangleInBoxACount == 0)
            return;
        
        
        // 递归
        Split(allNodes[nodeIndex].childIndex , depth + 1);
        Split(allNodes[nodeIndex].childIndex + 1 , depth + 1);
    }


    private bool IsTriangleInChildA(Triangle tri , BVHNode node)
    {
        // 获取三角形中心点
        Vector3 triCenter = (tri.posA + tri.posB + tri.posC) / 3;
        
        // 获取包围盒最长轴
        Vector3 boundsSize = node.GetBoundsSize();
        int splitAxis = boundsSize.x > Mathf.Max(boundsSize.y , boundsSize.z) 
                            ? 0 
                            : (boundsSize.y > boundsSize.z ? 1 : 2);
        
        return triCenter[splitAxis] < node.GetBoundsCenter()[splitAxis];
    }
}