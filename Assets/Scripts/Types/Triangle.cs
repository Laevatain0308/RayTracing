using System;
using UnityEngine;

[Serializable]
public struct Triangle
{
    public Vector3 posA , posB , posC;
    public Vector3 normalA , normalB , normalC;

    public Triangle(Vector3 posA , Vector3 posB , Vector3 posC , Vector3 normalA , Vector3 normalB , Vector3 normalC)
    {
        this.posA = posA;
        this.posB = posB;
        this.posC = posC;
        this.normalA = normalA;
        this.normalB = normalB;
        this.normalC = normalC;
    }
}
