using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public struct RayTracingMaterial
{
    [Header("Base Color")]
    public Color color;
    
    [Header("Emission")]
    public Color emissionColor;
    public float emissionStrength;
    
    [Header("Specular")]
    [Range(0f , 1f)] public float smoothness;
    [Range(0f , 1f)] public float metallic;
    public Color specularColor;

    public void SetDefaultValue()
    {
        color = Color.white;
        emissionColor = new Color(0 , 0 , 0 , 1);
        emissionStrength = 0;
        smoothness = 0;
        metallic = 0;
        specularColor = Color.white;
    }
}