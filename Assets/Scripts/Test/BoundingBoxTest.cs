using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class BoundingBoxTest : MonoBehaviour
{
    [SerializeField] private Transform minSphere;
    [SerializeField] private Transform maxSphere;

    [Space , SerializeField] private Color faceColor;
    [SerializeField] private Color edgeColor;


    private void OnDrawGizmos()
    {
        Gizmos.color = faceColor;
        // Gizmos.DrawCube(boundingBox.center , boundingBox.size);
        Gizmos.DrawCube((minSphere.position + maxSphere.position) * 0.5f , maxSphere.position - minSphere.position);

        Gizmos.color = edgeColor;
        // Gizmos.DrawWireCube(boundingBox.center , boundingBox.size);
        Gizmos.DrawWireCube((minSphere.position + maxSphere.position) * 0.5f , maxSphere.position - minSphere.position);
    }
}