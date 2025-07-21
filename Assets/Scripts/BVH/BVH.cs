using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BVH
{
    public const int MAX_DEPTH = 32;
    public const int MAX_SPLIT_TEST = 5;                                        // 切割测试时的最大切割次数
    
    public readonly NodeList allNodes;
    public readonly Triangle[] allTriangles;
    private BVHTriangle[] bvhTris;
    

    public BVH(Vector3[] vertices , Vector3[] normals , int[] indices)
    {
        int triangleCount = indices.Length / 3;
        
        // 构建数据
        allNodes = new NodeList();
        bvhTris = new BVHTriangle[triangleCount];
        BoundingBox bounds = new BoundingBox();
        
        // 创建包围盒
        foreach (var vertice in vertices)
        {
            bounds.GrowToInclude(vertice , vertice);
        }
        
        // 创建 BVH 三角形列表
        for (int i = 0 ; i < triangleCount ; i++)
        {
            int a = indices[i * 3];
            int b = indices[i * 3 + 1];
            int c = indices[i * 3 + 2];

            bvhTris[i] = new BVHTriangle(vertices[a] , vertices[b] , vertices[c] , i);
        }
        
        // 创建根节点并进行分割
        allNodes.Add(new BVHNode(bounds , 0 , bvhTris.Length));
        Split(0);
        
        // 依照分割中重排序的 BVH三角形 构建 渲染三角形 数组
        allTriangles = new Triangle[bvhTris.Length];
        for (int i = 0 ; i < bvhTris.Length ; i++)
        {
            int bvhTriIndex = bvhTris[i].triangleIndex;
            
            int a = indices[bvhTriIndex * 3];
            int b = indices[bvhTriIndex * 3 + 1];
            int c = indices[bvhTriIndex * 3 + 2];

            Vector3 posA = vertices[a];
            Vector3 posB = vertices[b];
            Vector3 posC = vertices[c];

            Vector3 normalA = normals[a];
            Vector3 normalB = normals[b];
            Vector3 normalC = normals[c];

            allTriangles[i] = new Triangle(posA , posB , posC , normalA , normalB , normalC);
        }
    }


    private void Split(int nodeIndex , int depth = 0)
    {
        if (depth == MAX_DEPTH)
            return;

        
        // 获取父节点
        BVHNode parent = allNodes.nodes[nodeIndex];

        
        // 若该节点的最佳切割后的子节点的总开销大于自身的开销，则停止切割
        (int splitAxis , float splitPos , float splitCost) = ChooseSplit(nodeIndex);
        if (splitCost > NodeCost(parent))
            return;
        

        // 更新其子节点索引
        parent.childIndex = allNodes.Count;

        
        // 创建包围盒
        BoundingBox boxA = new BoundingBox();
        BoundingBox boxB = new BoundingBox();


        // 子节点 A 三角形统计量
        int triangleInACount = 0;

        
        // 分配三角形至子节点包围盒
        for (int i = parent.triangleIndex ; i < parent.triangleIndex + parent.triangleCount ; i++)
        {
            BVHTriangle tri = bvhTris[i];
            
            bool inChildA = tri.center[splitAxis] < splitPos;
            if (inChildA)
            {
                boxA.GrowToInclude(tri);
                triangleInACount++;
                
                // 对两个子节点的三角形索引进行重排序
                int swap = parent.triangleIndex + triangleInACount - 1;
                (bvhTris[i] , bvhTris[swap]) = (bvhTris[swap] , bvhTris[i]);
            }
            else
            {
                boxB.GrowToInclude(tri);
            }
        }

        
        // 创建子节点
        allNodes.Add(new BVHNode(boxA , parent.triangleIndex , triangleInACount));
        allNodes.Add(new BVHNode(boxB , parent.triangleIndex + triangleInACount , parent.triangleCount - triangleInACount));
        
        
        // 更新父节点
        allNodes.nodes[nodeIndex] = parent;

        
        // 当子节点中有一个的三角形数为0时，停止递归（代表该节点的子节点无法再分割）
        if (triangleInACount == 0 || parent.triangleCount - triangleInACount == 0)
            return;
        
        
        // 递归
        Split(allNodes.nodes[nodeIndex].childIndex , depth + 1);
        Split(allNodes.nodes[nodeIndex].childIndex + 1 , depth + 1);
    }


    
    // 计算包围盒的开销 ———— SAH（Surface Area Heuristic）
    private static float NodeCost(Vector3 boundsSize , int triangleCountInBounds)
    {
        float halfArea = boundsSize.x * boundsSize.y + boundsSize.x * boundsSize.z + boundsSize.y * boundsSize.z;
        return halfArea * triangleCountInBounds;
    }

    private static float NodeCost(BVHNode node)
    {
        return NodeCost(node.GetBoundsSize() , node.triangleCount);
    }
    

    // 评估该次切割总开销
    private float EvaluateSplit(int nodeIndex , int splitAxis , float splitPos)
    {
        BoundingBox boxA = new BoundingBox();
        BoundingBox boxB = new BoundingBox();

        int triInACount = 0;
        int triInBCount = 0;

        BVHNode node = allNodes.nodes[nodeIndex];
        for (int i = node.triangleIndex ; i < node.triangleIndex + node.triangleCount ; i++)
        {
            BVHTriangle tri = bvhTris[i];
            if (tri.center[splitAxis] < splitPos)
            {
                boxA.GrowToInclude(tri);
                triInACount++;
            }
            else
            {
                boxB.GrowToInclude(tri);
                triInBCount++;
            }
        }

        return NodeCost(boxA.size , triInACount) + NodeCost(boxB.size , triInBCount);
    }

    // 获取经 SAH 评估后的最佳切割方法
    private (int axis , float pos , float cost) ChooseSplit(int nodeIndex)
    {
        float bestCost = float.MaxValue;
        float bestPos = 0;
        int bestAxis = 0;

        BVHNode node = allNodes.nodes[nodeIndex];

        for (int axis = 0 ; axis < 3 ; axis++)
        {
            // 获取该轴维度下的包围盒起点与终点
            float start = node.boundsMin[axis];
            float end = node.boundsMax[axis];

            for (int i = 0 ; i < MAX_SPLIT_TEST ; i++)
            {
                float t = (i + 1) / (MAX_SPLIT_TEST + 1f);
                float pos = start + (end - start) * t;
                float cost = EvaluateSplit(nodeIndex , axis , pos);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestPos = pos;
                    bestAxis = axis;
                }
            }
        }

        return (bestAxis , bestPos , bestCost);
    }
}