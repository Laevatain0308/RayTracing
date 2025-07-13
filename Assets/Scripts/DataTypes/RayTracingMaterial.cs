using System;
using UnityEngine;

[Serializable]
public struct RayTracingMaterial
{
    public Color color;
    public Color emissionColor;
    public float emissionStrength;
    [Range(0f , 1f)] public float smoothness;

    public void SetDefaultValue()
    {
        color = new Color(0 , 0 , 0 , 1);
        emissionColor = new Color(0 , 0 , 0 , 1);
        emissionStrength = 0;
        smoothness = 0;
    }
}