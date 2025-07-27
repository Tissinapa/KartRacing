using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Tracks warped continuous meshes and re-uses them across track template copies where possible.
/// Racetrack building code will automatically use this component if one is present in the scene.
/// </summary>
public class RacetrackMeshManager : MonoBehaviour
{
    /// <summary>
    /// Shared meshes in current scene
    /// </summary>
    public List<RacetrackMeshReference> SceneMeshes = new List<RacetrackMeshReference>();

    /// <summary>
    /// Shared meshes saved to project
    /// </summary>
    public RacetrackSavedMeshes SavedMeshes;

    [NonSerialized]
    public bool AreTemplatesUpToDate = false;

    public static HashMethod GetHashMethod()
    {
        var meshManager = GameObject.FindObjectOfType<RacetrackMeshManager>();
        return meshManager != null ? meshManager.HashMethod : HashMethod.MD5WithGapFix;
    }

    /// <summary>
    /// Look for an existing matching mesh
    /// </summary>
    /// <returns>Matching mesh if one exists, or null otherwise</returns>
    public Mesh GetMesh(Mesh baseMesh, int templateCopyHash, Matrix4x4 templateFromMesh)
    {
        // Look for matching mesh reference. Search saved meshes first, then scene meshes.
        int transformHash = CalcTransformHash(templateFromMesh);
        var meshRef = GetMeshRef(baseMesh, templateCopyHash, transformHash);
        return meshRef != null ? meshRef.Mesh : null;
    }

    /// <summary>
    /// Store a mesh so that it can potentially be re-used elsewhere
    /// </summary>
    public void StoreMesh(Mesh baseMesh, int templateCopyHash, Matrix4x4 templateFromMesh, Mesh mesh, RacetrackTemplateCopyParams p)
    {
        // Store as scene mesh if not already saved
        int transformHash = CalcTransformHash(templateFromMesh);
        if (this.GetMeshRef(baseMesh, templateCopyHash, transformHash) == null)
        {
            // Attempt to pick a reasonable default for "selectForSave".
            // Bezier curves and z-scaled curves are less likely to create identical meshes
            // that can be re-used.
            bool selectForSave = Mathf.Abs(p.ZScale - 1.0f) < 1.0f / 8192.0f && p.PathSection.GetSegments().All(s => s.Curve.Type == RacetrackCurveType.Arc);

            // Add scene mesh
            this.SceneMeshes.Add(new RacetrackMeshReference
            {
                BaseMesh = baseMesh,
                TemplateCopyHash = templateCopyHash,
                TransformHash = transformHash,
                Mesh = mesh,
                SelectForSave = selectForSave
            });
            this.AreTemplatesUpToDate = false;
        }
    }

    /// <summary>
    /// Find and delete unused meshes
    /// </summary>
    public void GarbageCollect()
    {
        foreach (var mesh in this.SceneMeshes)
        {
            mesh.RefCount = 0;
            mesh.MeshTemplates = new HashSet<RacetrackMeshTemplate>();
        }

        // Find meshes in scene
        var meshFilterMeshes = GameObject.FindObjectsOfType<MeshFilter>().Select(mf => new { Mesh = mf.sharedMesh, Obj = mf.gameObject });
        var collisionMeshes = GameObject.FindObjectsOfType<MeshCollider>().Select(mc => new { Mesh = mc.sharedMesh, Obj = mc.gameObject });
        var meshes = meshFilterMeshes.Concat(collisionMeshes).Where(m => m != null).ToList();

        // Count references and which mesh templates they are used in.
        foreach (var mesh in meshes)
        {
            var meshRef = this.SceneMeshes.FirstOrDefault(mr => mr.Mesh == mesh.Mesh);
            if (meshRef != null)
            { 
                meshRef.RefCount++;
                meshRef.BaseMeshName = meshRef.BaseMesh != null && meshRef.BaseMesh.name != null ? meshRef.BaseMesh.name : "[null]";
                var meshTemplate = mesh.Obj.GetComponentInParent<RacetrackTemplateCopy>();
                if (meshTemplate != null)
                    meshRef.MeshTemplates.Add(meshTemplate.Template);
            }
        }

        // Remove unreferenced meshes
        this.SceneMeshes.RemoveAll(m => m.RefCount == 0);
        this.SceneMeshes.Sort(new MeshReferenceSortComparer());
        this.AreTemplatesUpToDate = true;
    }

    private class MeshReferenceSortComparer : IComparer<RacetrackMeshReference>
    {
        public int Compare(RacetrackMeshReference x, RacetrackMeshReference y)
        {
            int dif = y.RefCount - x.RefCount;
            if (dif == 0)
                dif = string.Compare(x.BaseMeshName, y.BaseMeshName, true);
            return dif;
        }
    }

    private int CalcTransformHash(Matrix4x4 templateFromMesh)
    {
        return RacetrackBuilder.CreateHasher(HashMethod).RoundedMatrix4x4(templateFromMesh).Hash;
    }

    private RacetrackMeshReferenceBase GetMeshRef(Mesh baseMesh, int templateCopyHash, int transformHash)
    {
        // Search saved meshes first
        // Check saved mesh actually has a valid mesh before returning, in case the mesh has been deleted from the save folder.
        if (this.SavedMeshes != null)
        {
            var savedMesh = this.SavedMeshes.Meshes.FirstOrDefault(r => r.BaseMesh == baseMesh && r.TemplateCopyHash == templateCopyHash && r.TransformHash == transformHash && r.Mesh != null);
            if (savedMesh != null)
                return savedMesh;
        }

        // Search scene meshes
        return this.SceneMeshes.FirstOrDefault(r => r.BaseMesh == baseMesh && r.TemplateCopyHash == templateCopyHash && r.TransformHash == transformHash);
    }

    /// <summary>
    /// Hashing method to use
    /// </summary>
    private HashMethod HashMethod
    {
        get
        {
            // If SavedMeshes is present, use its value. Otherwise default to MD5 (with gap fix)
            return SavedMeshes != null ? SavedMeshes.HashMethod : HashMethod.MD5WithGapFix;
        }
    }
}
