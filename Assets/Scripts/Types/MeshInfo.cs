using System;
using UnityEngine;

[Serializable]
public struct MeshInfo
{
    public int triangleOffset;
    public int nodeOffset;
    public Matrix4x4 localToWorldMatrix;
    public Matrix4x4 worldToLocalMatrix;
    public RayTracingMaterial material;

    
    public MeshInfo(int _triangleOffset , int _nodeOffset , Matrix4x4 _localToWorldMatrix , Matrix4x4 _worldToLocalMatrix , RayTracingMaterial _material)
    {
        triangleOffset = _triangleOffset;
        nodeOffset = _nodeOffset;
        localToWorldMatrix = _localToWorldMatrix;
        worldToLocalMatrix = _worldToLocalMatrix;
        material = _material;
    }
}