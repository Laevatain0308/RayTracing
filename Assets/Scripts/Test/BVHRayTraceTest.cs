using System;
using System.Collections.Generic;
using UnityEngine;

public class BVHRayTraceTest : MonoBehaviour
{
    private MeshFilter meshFilter;
    private BVH bvh;

    private List<BVHBox> boxes = new List<BVHBox>();


    [SerializeField] private Transform testLight;
    private Ray theRay;
    private Result theResult;


    struct HitInfo
    {
        public bool didHit;
        public float distance;
    }

    struct Result
    {
        public float distance;
        public int triIndex;
        public BVHNode node;
    }


    private void Update()
    {
        Ray newRay = new Ray(testLight.position , testLight.forward);
        if (newRay.origin != theRay.origin || newRay.direction != theRay.direction)
        {
            theRay = newRay;
            boxes.Clear();
            theResult = new Result() { distance = Mathf.Infinity };
            RayTracingBVH(0 , theRay , ref theResult);
        }
    }


    private void RayTracingBVH(int nodeIndex , Ray ray , ref Result result , int depth = 0)
    {
        if (nodeIndex < 0)
            return;
        
        BVHNode node = bvh.allNodes[nodeIndex];
        
        BoundingBox box = new BoundingBox();
        box.GrowToInclude(node.boundsMin , node.boundsMax);
        
        bool didHit = RayBoundingBox(ray , box);
        if (didHit)
        {
            boxes.Add(new BVHBox(nodeIndex , depth , BVHBox.DisplayType.Recursion));

            if (node.childIndex < 0)
            {
                for (int i = node.triangleIndex ; i < node.triangleIndex + node.triangleCount ; i++)
                {
                    Triangle tri = bvh.allTriangles[i];
                    HitInfo triInfo = RayTriangle(ray , tri);
                    
                    if (triInfo.didHit && triInfo.distance < result.distance)
                    {
                        result.distance = triInfo.distance;
                        result.triIndex = i;
                        result.node = node;
                    }
                }
            }
            else
            {
                RayTracingBVH(node.childIndex , ray , ref result , depth + 1);
                RayTracingBVH(node.childIndex + 1 , ray , ref result , depth + 1);
            }
        }
    }

    private bool RayBoundingBox(Ray ray , BoundingBox bounds)
    {
        Vector3 dir = ray.direction;
        Vector3 inDir = new Vector3(1 / dir.x , 1 / dir.y , 1 / dir.z);
                
        Vector3 tMin = Vector3Mul(bounds.min - ray.origin , inDir);
        Vector3 tMax = Vector3Mul(bounds.max - ray.origin , inDir);

        Vector3 t1 = Vector3.Min(tMin , tMax);
        Vector3 t2 = Vector3.Max(tMin , tMax);

        float tEnter = Mathf.Max(Mathf.Max(t1.x , t1.y) , t1.z);
        float tExit = Mathf.Min(Mathf.Min(t2.x , t2.y) , t2.z);
        
        return tExit >= 0 && tEnter <= tExit;
    }

    private HitInfo RayTriangle(Ray ray , Triangle tri)
    {
        HitInfo info = new HitInfo();
        
        // 求三角面法线
        Vector3 edgeAB = tri.posB - tri.posA;
        Vector3 edgeAC = tri.posC - tri.posA;
        Vector3 normal = Vector3.Cross(edgeAB , edgeAC);

        Vector3 vectorAO = ray.origin - tri.posA;
        Vector3 crossAOD = Vector3.Cross(vectorAO , ray.direction);

        float determinant = -Vector3.Dot(ray.direction , normal);                 // 当 determinant > 0 时，射线穿过三角形正面；determinant < 0 时，射线穿过三角形背面；determinant = 0 时，射线与三角面平行
        // 出于精度问题，故认为当 |determinant| < (某一很小的数) 时，射线与三角面平行
        float inDeterminant = 1.0f / determinant;                    // 取倒数

        // 求距离及交点
        float distance = Vector3.Dot(vectorAO , normal) * inDeterminant;

        // 求三角形各顶点法线比重
        float u = -Vector3.Dot(edgeAB , crossAOD) * inDeterminant; // 顶点 C
        float v = Vector3.Dot(edgeAC , crossAOD) * inDeterminant;  // 顶点 B
        float w = 1.0f - u - v;                                     // 顶点 A

        // 输出
        info.didHit = determinant >= 1e-6 && distance >= 0 && u >= 0 && v >= 0 && w >= 0;
        info.distance = distance;
        return info;
    }


    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(theRay.origin , theRay.origin + theRay.direction * 5f);
        
        
        for (int i = 0 ; i < boxes.Count ; i++)
        {
            BVHNode node = bvh.allNodes[boxes[i].nodeIndex];
            
            Gizmos.color = boxes[i].color;
            Gizmos.DrawCube(node.GetBoundsCenter() , node.GetBoundsSize());
            Gizmos.DrawWireCube(node.GetBoundsCenter() , node.GetBoundsSize());
        }
    
    
        BVHNode n = theResult.node;
        
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> indices = new List<int>();
        
        for (int i = n.triangleIndex ; i < n.triangleIndex + n.triangleCount ; i++)
        {
            if (i == theResult.triIndex)
                continue;
            
            Triangle tri = bvh.allTriangles[i];
            
            indices.Add(vertices.Count);
            indices.Add(vertices.Count + 1);
            indices.Add(vertices.Count + 2);
            
            vertices.Add(tri.posA);
            vertices.Add(tri.posB);
            vertices.Add(tri.posC);
            
            normals.Add(tri.normalA);
            normals.Add(tri.normalB);
            normals.Add(tri.normalC);
        }
    
        if (vertices.Count == 0 || normals.Count == 0 || indices.Count == 0)
            return;
        
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.triangles = indices.ToArray();
        Gizmos.color = new Color(1 , 0 , 0 , 0.8f);
        Gizmos.DrawWireMesh(mesh);
        Gizmos.color = new Color(1 , 0 , 0 , 0.5f);
        Gizmos.DrawMesh(mesh);
    
        Mesh hitMesh = new Mesh();
        Triangle t = bvh.allTriangles[theResult.triIndex];
        hitMesh.vertices = new[] { t.posA , t.posB , t.posC };
        hitMesh.normals = new[] { t.normalA , t.normalB , t.normalC };
        hitMesh.triangles = new[] { 0 , 1 , 2 };
        Gizmos.color = new Color(0 , 1 , 0 , 0.8f);
        Gizmos.DrawWireMesh(hitMesh);
        Gizmos.color = new Color(0 , 1 , 0 , 0.5f);
        Gizmos.DrawMesh(hitMesh);
    }

    

    private void OnValidate()
    {
        meshFilter = GetComponent<MeshFilter>();
        bvh = new BVH(meshFilter.sharedMesh.vertices , meshFilter.sharedMesh.normals , meshFilter.sharedMesh.triangles);
    }


    private Vector3 Vector3Mul(Vector3 a , Vector3 b)
    {
        return new Vector3(a.x * b.x , a.y * b.y , a.z * b.z);
    }
}