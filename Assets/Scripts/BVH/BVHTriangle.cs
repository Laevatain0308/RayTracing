using UnityEngine;
using Unity.Mathematics;

public readonly struct BVHTriangle
{
    public readonly float3 boundsMin;
    public readonly float3 boundsMax;
    public readonly float3 center;
    public readonly int triangleIndex;

    public BVHTriangle(float3 posA , float3 posB , float3 posC , int triIndex)
    {
        boundsMin = math.min(math.min(posA , posB) , posC);
        boundsMax = math.max(math.max(posA , posB) , posC);

        center = (posA + posB + posC) / 3;

        triangleIndex = triIndex;
    }
}