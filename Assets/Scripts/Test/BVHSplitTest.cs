using System;
using System.Collections.Generic;
using UnityEngine;

public class BVHSplitTest : MonoBehaviour
{
    private MeshFilter meshFilter;
    private BVH bvh;
    
    private List<BVHBox> boxes = new List<BVHBox>();

    [SerializeField] private int visualDepth = 0;


    private void Start()
    {
        boxes.Clear();
        DrawNodes(0);
    }

    public void DrawNodes(int nodeIndex , int depth = 0)
    {
        if (depth > visualDepth || nodeIndex < 0)
            return;
        
        bool fill = depth == visualDepth;
        
        boxes.Add(new BVHBox(nodeIndex , depth , fill ? BVHBox.DisplayType.Focus : BVHBox.DisplayType.Wire));

        BVHNode node = bvh.allNodes[nodeIndex];
        if (node.childIndex < 0)
            return;
        
        DrawNodes(node.childIndex , depth + 1);
        DrawNodes(node.childIndex + 1 , depth + 1);
    }

    public void Up()
    {
        visualDepth++;
        boxes.Clear();
        DrawNodes(0);
    }
    
    public void Down()
    {
        visualDepth--;
        boxes.Clear();
        DrawNodes(0);
    }
    
    private void OnDrawGizmos()
    {
        for (int i = 0 ; i < boxes.Count ; i++)
        {
            BVHNode node = bvh.allNodes[boxes[i].nodeIndex];
            
            Gizmos.color = boxes[i].color;
            
            Gizmos.DrawWireCube(node.GetBoundsCenter() , node.GetBoundsSize());
            
            if (boxes[i].type == BVHBox.DisplayType.Focus)
                Gizmos.DrawCube(node.GetBoundsCenter() , node.GetBoundsSize());
        }
    }

    private void OnValidate()
    {
        meshFilter = GetComponent<MeshFilter>();
        bvh = new BVH(meshFilter.sharedMesh.vertices , meshFilter.sharedMesh.normals , meshFilter.sharedMesh.triangles);
    }
}