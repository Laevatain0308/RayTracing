using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CameraTest : MonoBehaviour
{
    [SerializeField] private Vector2 debugPointCount;
    [SerializeField] private float pointRadius;
    [SerializeField] private Color pointColor;

    [Range(0f , 1f)] [SerializeField] private float rayLength;

    private List<Vector3> pointPoses;

    [Space] [SerializeField] private bool drawGizmos;


    private void Update()
    {
        if (!drawGizmos)
            return;
        
        CameraRayTest();
    }


    private void CameraRayTest()
    {
        pointPoses = new List<Vector3>();
        
        Camera cam = Camera.main;
        Transform camTransform = cam.transform;
        
        // 根据近裁面选择合适的屏幕空间
        float screenPlaneHeight = cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2;
        float screenPlaneWidth = screenPlaneHeight * cam.aspect;
        
        // 给出屏幕左下角相对于相机的相对坐标
        Vector3 bottomLeftLocal = new Vector3(-screenPlaneWidth / 2 , -screenPlaneHeight / 2 , cam.nearClipPlane);

        // 显示Debug小球
        for (int i = 0 ; i < debugPointCount.x ; i++)
        {
            for (int j = 0 ; j < debugPointCount.y ; j++)
            {
                // 归一化
                float x = i / (debugPointCount.x - 1f);
                float y = j / (debugPointCount.y - 1f);

                Vector3 local = bottomLeftLocal + new Vector3(screenPlaneWidth * x , screenPlaneHeight * y , 0);
                // Vector3 world = camTransform.position 
                //                 + camTransform.right * local.x
                //                 + camTransform.up * local.y 
                //                 + camTransform.forward * local.z;
                Vector3 world = cam.transform.localToWorldMatrix * new Vector4(local.x , local.y , local.z , 1);

                pointPoses.Add(world);
            }
        }
    }


    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;
        
        foreach (var pos in pointPoses)
        {
            Gizmos.color = pointColor;
            Gizmos.DrawSphere(pos , pointRadius);

            Gizmos.color = Color.yellow;
            Vector3 camPos = Camera.main.transform.position;
            Gizmos.DrawLine(camPos , camPos + (pos - camPos).normalized * rayLength);
        }
    }
}
