using System;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[ExecuteAlways]
public class RayTracedObject : MonoBehaviour
{
    public RayTracingMaterial material;
    
    [SerializeField , HideInInspector] protected int materialObjectID;
    [SerializeField , HideInInspector] protected bool materialInitFlag;


    protected virtual void OnValidate()
    {
        if (!materialInitFlag)
        {
            materialInitFlag = true;
            material.SetDefaultValue();
        }

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            if (materialObjectID != gameObject.GetInstanceID())
            {
                meshRenderer.material = new Material(meshRenderer.sharedMaterial);
                materialObjectID = gameObject.GetInstanceID();
            }

            meshRenderer.sharedMaterial.color = material.color;
        }
    }
}