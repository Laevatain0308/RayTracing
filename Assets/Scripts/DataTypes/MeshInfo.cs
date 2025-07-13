using System;
using UnityEngine;

[Serializable]
public struct MeshInfo
{
    public int firstTriangleIndex;
    public int triangleCount;
    public Vector3 boxMin;
    public Vector3 boxMax;
    public RayTracingMaterial material;

    public MeshInfo(int firstTriangleIndex , int triangleCount , Bounds bounds , RayTracingMaterial material)
    {
        this.firstTriangleIndex = firstTriangleIndex;
        this.triangleCount = triangleCount;
        this.boxMin = bounds.min;
        this.boxMax = bounds.max;
        this.material = material;
    }
}