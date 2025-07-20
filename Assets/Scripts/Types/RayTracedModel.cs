using System;
using UnityEngine;

[ExecuteAlways]
public class RayTracedModel : MonoBehaviour
{
    private MeshFilter meshFilter;
    public Mesh mesh => meshFilter.sharedMesh;
    
    public RayTracingMaterial material;


    private void OnEnable()
    {
        RayTracingManager.instance ? .RegisterModel(this);
    }
    
    private void OnDisable()
    {
        RayTracingManager.instance ? .UnregisterModel(this);
    }

    private void OnValidate()
    {
        meshFilter = GetComponent<MeshFilter>();
    }
}