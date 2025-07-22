using UnityEngine;

[RequireComponent(typeof(MeshCollider))]
public class ColliderCombiner : MonoBehaviour
{
    [Tooltip("If empty, all child MeshFilters will be combined.")]
    public MeshFilter[] meshFilters;

    [Tooltip("Name for the generated mesh asset.")]
    public string combinedMeshName = "CombinedColliderMesh";

    void Awake()
    {
        CombineMeshes();
    }

    public void CombineMeshes()
    {
        // 1) Gather MeshFilters
        if (meshFilters == null || meshFilters.Length == 0)
        {
            meshFilters = GetComponentsInChildren<MeshFilter>();
        }

        // 2) Build CombineInstance array
        var combines = new CombineInstance[meshFilters.Length];
        for (int i = 0; i < meshFilters.Length; i++)
        {
            var mf = meshFilters[i];
            combines[i].mesh = mf.sharedMesh;
            // Transform into this GameObject's local space
            combines[i].transform =
                transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        }

        // 3) Create combined mesh
        var combinedMesh = new Mesh
        {
            name = combinedMeshName
        };
        combinedMesh.CombineMeshes(combines, mergeSubMeshes: true, useMatrices: true);

        // 4) Assign to MeshCollider
        var mc = GetComponent<MeshCollider>();
        mc.sharedMesh = combinedMesh;
        //mc.convex = true;
        mc.isTrigger = false;

        // (Optional) Clean up child colliders/meshes
        foreach (var mf in meshFilters)
        {
            var col = mf.GetComponent<Collider>();
            if (col != null) Destroy(col);
            // If you no longer need visuals:
            // Destroy(mf.gameObject);
        }
    }
}
