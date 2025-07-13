using System;
using UnityEngine;

public class RayTracedSphere : RayTracedObject
{
    public Vector3 GetPosition() => transform.position;

    public float GetRadius() => transform.localScale.x / 2;


    private void OnEnable()
    {
        RayTracingManager.instance ? .RegisterSphere(this);
    }

    private void OnDisable()
    {
        RayTracingManager.instance ? .UnregisterSphere(this);
    }
}
