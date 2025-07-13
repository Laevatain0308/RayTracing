using System;
using UnityEngine;

[Serializable]
public struct Sphere
{
    public Vector3 position;
    public float radius;
    public RayTracingMaterial material;

    public Sphere(Vector3 position , float radius , RayTracingMaterial material)
    {
        this.position = position;
        this.radius = radius;
        this.material = material;
    }
}
