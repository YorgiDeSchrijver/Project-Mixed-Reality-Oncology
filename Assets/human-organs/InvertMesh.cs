using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public class InvertMesh : MonoBehaviour
{
    private void Awake()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;

        // 1. Reverse the triangles so collision works from the inside
        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int temp = triangles[i];
            triangles[i] = triangles[i + 1];
            triangles[i + 1] = temp;
        }
        mesh.triangles = triangles;

        // 2. Flip the normals so lighting shines properly on the inside walls
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = -normals[i];
        }
        mesh.normals = normals;

        // 3. Refresh the Mesh Collider with the newly inverted mesh
        MeshCollider collider = GetComponent<MeshCollider>();
        collider.sharedMesh = null;
        collider.sharedMesh = mesh;
    }
}