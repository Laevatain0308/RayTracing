using System.Collections.Generic;
using UnityEngine;

public class RenderData
{
    public List<Triangle> triangles;
    public List<BVHNode> nodes;
    public List<MeshInfo> meshInfos;

    public RenderData()
    {
        triangles = new List<Triangle>();
        nodes = new List<BVHNode>();
        meshInfos = new List<MeshInfo>();
    }
}