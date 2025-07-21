using System;
using UnityEngine;

public class NodeList
{
    public BVHNode[] nodes = new BVHNode[256];
    private int count;

    
    /// <summary>
    /// 加入元素并获取其在数组中的索引
    /// </summary>
    /// <param name="node">新元素</param>
    /// <returns></returns>
    public int Add(BVHNode node)
    {
        if (count >= nodes.Length)
        {
            Array.Resize(ref nodes , nodes.Length * 2);             // 成倍扩容，减少批量扩容开销
        }

        int index = count;
        nodes[count++] = node;
        return index;
    }

    public int Count => count;
}