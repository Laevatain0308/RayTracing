using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct BVHNode
{
    public Vector3 boundsMin;
    public Vector3 boundsMax;
    public int triangleIndex;
    public int triangleCount;
    public int childIndex;                  // 一个节点拥有两个子节点，该处存储的是第一个子节点的索引，另一个紧随其后
    
    
    public BVHNode(BoundingBox bounds , int triangleIndex , int triangleCount)
    {
        this.boundsMin = bounds.min;
        this.boundsMax = bounds.max;
        this.triangleIndex = triangleIndex;
        this.triangleCount = triangleCount;
        this.childIndex = -1;
    }


    public Vector3 GetBoundsCenter() => (boundsMax + boundsMin) / 2;

    public Vector3 GetBoundsSize() => boundsMax - boundsMin;
}
