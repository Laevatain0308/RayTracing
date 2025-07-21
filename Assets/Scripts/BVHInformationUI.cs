using UnityEngine;
using UnityEngine.UI;

public class BVHInformationUI : MonoBehaviour
{
    public static BVHInformationUI instance;

    [SerializeField] private Text information;

    [HideInInspector] public float bvhBuildTime;


    public void UpdateInformation(RenderData data)
    {
        int leafCount = 0;
        int leafAllTris = 0;
        int leafMaxTriCount = 0;
        int leafMinTriCount = data.triangles.Count;
        
        foreach (var node in data.nodes)
        {
            if (node.childIndex < 0)
            {
                leafCount++;
                leafAllTris += node.triangleCount;
                leafMaxTriCount = Mathf.Max(leafMaxTriCount , node.triangleCount);
                leafMinTriCount = Mathf.Min(leafMinTriCount , node.triangleCount);
            }
        }

        information.text = "BVH Build Time:  " + Mathf.RoundToInt(bvhBuildTime * 1000) + " ms"
                                              + "\nTriangles:  " + data.triangles.Count 
                                              + "\nMax Depth:  " + BVH.MAX_DEPTH
                                              + "\nNode Count:  " + data.nodes.Count
                                              + "\nLeaf Count:  " + leafCount
                                              + "\nLeaf Tris:  "
                                              + "\n - Min:  " + leafMinTriCount
                                              + "\n - Max:  " + leafMaxTriCount
                                              + "\n - Average:  " + (leafCount != 0 ? leafAllTris / (float)leafCount : -1);
    }


    public void StartBuild()
    {
        bvhBuildTime = Time.realtimeSinceStartup;
    }

    public void SuccessBuild()
    {
        bvhBuildTime = Time.realtimeSinceStartup - bvhBuildTime;
    }
    
    

    private void OnEnable()
    {
        if (instance == null)
            instance = this;
    }
    
    private void OnDisable()
    {
        instance = null;
    }
}
