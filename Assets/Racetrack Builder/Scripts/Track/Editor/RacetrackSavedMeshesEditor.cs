using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RacetrackSavedMeshes))]
public class RacetrackSavedMeshesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var savedMeshes = (RacetrackSavedMeshes)target;

        GUILayout.Space(RacetrackConstants.SpaceHeight);
        if (GUILayout.Button("Remove deleted meshes", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
        {
            savedMeshes.Meshes.RemoveAll(m => m.Mesh == null);
            RacetrackEditorUtil.SaveScriptableObjectChanges(savedMeshes);
        }
    }
}
