using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RacetrackMeshTemplate))]
public class RacetrackMeshTemplateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var template = (RacetrackMeshTemplate)target;
        var meshCache = RacetrackMeshInfoCache.Instance;
        var info = meshCache.GetTemplateInfo(template);

        var obj = new SerializedObject(template);
        RacetrackEditorUtil.PropertyEditors(obj, true, "XZAxisTransform", "AutoMinMaxZ");
        if (template.AutoMinMaxZ)
        {
            template.MinZ = info.MeasuredMinZ;
            template.MaxZ = info.MeasuredMaxZ;
        }
        RacetrackEditorUtil.PropertyEditors(obj, !template.AutoMinMaxZ, "MinZ", "MaxZ");
        if (obj.ApplyModifiedProperties())
        {
            template.MaxZ = Mathf.Max(template.MinZ + 0.0001f, template.MaxZ);
        }

        float length = template.MaxZ - template.MinZ;
        GUILayout.BeginHorizontal();
        GUILayout.Label("Length", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
        GUILayout.Label(length.ToString());
        GUILayout.EndHorizontal();

        GUILayout.Space(RacetrackConstants.SpaceHeight);
        GUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
        if (GUILayout.Button("Evict from cache"))
            meshCache.Remove(template);
        GUILayout.EndHorizontal();
    }
}
