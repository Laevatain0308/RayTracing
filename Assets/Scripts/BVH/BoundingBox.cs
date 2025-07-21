using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct BoundingBox
{
    public Vector3 min;
    public Vector3 max;
    public Vector3 center => (max + min) * 0.5f;
    public Vector3 size => max - min;

    private bool hasPoint;

    
    public void GrowToInclude(Vector3 _min , Vector3 _max)
    {
        if (hasPoint)
        {
            min = Vector3.Min(min , _min);
            max = Vector3.Max(max , _max);
        }
        else
        {
            min = _min;
            max = _max;

            hasPoint = true;
        }
    }

    public void GrowToInclude(BVHTriangle bvhTri)
    {
        GrowToInclude(bvhTri.boundsMin , bvhTri.boundsMax);
    }
}