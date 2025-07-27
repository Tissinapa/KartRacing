using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

[CustomEditor(typeof(RacetrackMeshManager))]
public class RacetrackMeshManagerEditor : Editor
{
    private static bool showSceneMeshes = false;
    private static List<TemplateCopyUIState> templateCopyStates = new List<TemplateCopyUIState>();

    public override void OnInspectorGUI()
    {
        var manager = (RacetrackMeshManager)target;

        showSceneMeshes = EditorGUILayout.Foldout(showSceneMeshes, string.Format("Scene meshes ({0})", manager.SceneMeshes.Count));
        if (showSceneMeshes)
        {
            if (!manager.AreTemplatesUpToDate)
                manager.GarbageCollect();

            var byTemplate = manager.SceneMeshes
                .GroupBy(m => m.MeshTemplates, HashSet<RacetrackMeshTemplate>.CreateSetComparer())
                .Select(g => new
                {
                    GroupKey = string.Join(",", g.Key.Select(t => t.gameObject.name).OrderBy(n => n)),
                    Meshes = g
                });

            // Update template copy states
            templateCopyStates.RemoveAll(s => !byTemplate.Any(t => t.GroupKey == s.GroupKey));
            foreach (var template in byTemplate)
            {
                var meshes = template.Meshes.ToList();

                // Find UI state
                var uiState = templateCopyStates.FirstOrDefault(s => s.GroupKey == template.GroupKey);
                if (uiState == null)
                {
                    uiState = new TemplateCopyUIState
                    {
                        GroupKey = template.GroupKey,
                        Expand = false
                    };
                    templateCopyStates.Add(uiState);
                }

                bool allSelected = meshes.All(m => m.SelectForSave);

                GUILayout.BeginHorizontal();
                bool newAllSelected = EditorGUILayout.ToggleLeft(string.Format("{0} ({1})", template.GroupKey, meshes.Max(m => m.RefCount)), allSelected);
                if (newAllSelected != allSelected)
                    meshes.ForEach(m => m.SelectForSave = newAllSelected);
                if (GUILayout.Button("Select in scene", GUILayout.MinHeight(RacetrackConstants.SmallButtonHeight)))
                {
                    Selection.objects = this.FindMeshGameObjects(meshes.Select(r => r.Mesh)).ToArray();
                }
                GUILayout.EndHorizontal();

                uiState.Expand = EditorGUILayout.Foldout(uiState.Expand, string.Format("    {0}/{1} selected", meshes.Count(m => m.SelectForSave), meshes.Count()));
                if (uiState.Expand)
                {
                    foreach (var mesh in template.Meshes)
                    {
                        GUILayout.BeginHorizontal();
                        mesh.SelectForSave = EditorGUILayout.ToggleLeft(string.Format("{0} ({1})", mesh.BaseMesh != null ? mesh.BaseMesh.name : "[null]", mesh.RefCount), mesh.SelectForSave);
                        if (GUILayout.Button("Select in scene", GUILayout.MinHeight(RacetrackConstants.SmallButtonHeight)))
                        {
                            Selection.objects = this.FindMeshGameObjects(mesh.Mesh).ToArray();
                        }
                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.Space(15.0f);
            }
        }

        // Saved meshes 
        GUILayout.Space(RacetrackConstants.SpaceHeight);
        var obj = new SerializedObject(manager);
        RacetrackEditorUtil.PropertyEditors(obj, true, "SavedMeshes");

        obj.ApplyModifiedProperties();

        // Buttons
        if (manager.SceneMeshes.Any())
        {
            GUILayout.Space(RacetrackConstants.SpaceHeight);

            if (GUILayout.Button("Clear scene meshes", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                using (var undo = new ScopedUndo("Clear scene meshes"))
                {
                    undo.RecordObject(manager);
                    manager.SceneMeshes.Clear();
                    manager.AreTemplatesUpToDate = true;
                }
            }

            if (manager.SavedMeshes != null && GUILayout.Button("Save scene meshes", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                // Ensure folder exists
                RacetrackEditorUtil.CreateAssetFolders(manager.SavedMeshes.SaveFolder);

                using (var undo = new ScopedUndo("Save scene meshes"))
                {
                    undo.RecordObject(manager);
                    this.SaveMeshes(manager);
                    RacetrackEditorUtil.SaveScriptableObjectChanges(manager.SavedMeshes);
                }
            }
        }
    }

    public void SaveMeshes(RacetrackMeshManager manager)
    {
        if (manager.SavedMeshes == null)
        {
            Debug.LogError("SavedMeshes property must be set before meshes can be saved");
            return;
        }

        // Iterate copy of array
        var sceneMeshes = manager.SceneMeshes.Where(m => m.SelectForSave).ToList();
        try
        {
            foreach (var mesh in sceneMeshes)
            {
                if (!AssetDatabase.Contains(mesh.Mesh))
                {
                    // Save mesh as asset
                    var filename = manager.SavedMeshes.GetNewAssetFilename();
                    AssetDatabase.CreateAsset(mesh.Mesh, filename);

                    // Move to saved meshes list
                    manager.SavedMeshes.Meshes.Add(
                        new RacetrackMeshReferenceSaved
                        {
                            BaseMesh = mesh.BaseMesh,
                            TemplateCopyHash = mesh.TemplateCopyHash,
                            TransformHash = mesh.TransformHash,
                            Mesh = mesh.Mesh
                        });
                }

                manager.SceneMeshes.Remove(mesh);
                manager.AreTemplatesUpToDate = false;
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
        }
    }

    private IEnumerable<GameObject> FindMeshGameObjects(Func<Mesh, bool> predicate)
    {
        return GameObject.FindObjectsOfType<MeshFilter>().Where(mf => predicate(mf.sharedMesh)).Select(mf => mf.gameObject)
            .Concat(GameObject.FindObjectsOfType<MeshCollider>().Where(mc => predicate(mc.sharedMesh)).Select(mc => mc.gameObject))
            .Distinct();
    }

    private IEnumerable<GameObject> FindMeshGameObjects(Mesh mesh)
    {
        return FindMeshGameObjects(m => m == mesh);
    }

    private IEnumerable<GameObject> FindMeshGameObjects(IEnumerable<Mesh> meshes)
    {
        return FindMeshGameObjects(m => meshes.Contains(m));
    }


    private class TemplateCopyUIState
    {
        // Key
        public string GroupKey;

        // UI data
        public bool Expand;
    }
}
