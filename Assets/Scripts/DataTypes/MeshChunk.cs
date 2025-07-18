﻿using System;
using UnityEngine;

[Serializable]
public class MeshChunk
{
    public Triangle[] triangles;
    public Bounds bounds;
    public int subMeshIndex;

    public MeshChunk(Triangle[] triangles , Bounds bounds , int subMeshIndex)
    {
        this.triangles = triangles;
        this.bounds = bounds;
        this.subMeshIndex = subMeshIndex;
    }
}