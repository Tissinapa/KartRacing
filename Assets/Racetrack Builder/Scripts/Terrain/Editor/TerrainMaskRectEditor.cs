using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TerrainMaskRect))]
public class TerrainMaskRectEditor : Editor
{
    public void OnSceneGUI()
    {
        var mask = (TerrainMaskRect)target;

        // Draw terrain quad
        Handles.matrix = mask.transform.localToWorldMatrix;
        Vector3[] pts = new[]
        {
            new Vector3(-mask.Width / 2, 0.0f, 0.0f),
            new Vector3(-mask.Width / 2, 0.0f, mask.Length),
            new Vector3( mask.Width / 2, 0.0f, mask.Length),
            new Vector3( mask.Width / 2, 0.0f, 0.0f)
        };
        Handles.color = new Color(0.65f, 0.25f, 0.0f, 1.0f);
        for (int i = 0; i < 4; i++)
            Handles.DrawDottedLine(pts[i], pts[(i + 1) % 4], 5.0f);

        // Length handle
        Handles.color = Color.white;
        EditorGUI.BeginChangeCheck();
        Vector3 handle = new Vector3(0.0f, 0.0f, mask.Length);
        float handleSize = HandleUtility.GetHandleSize(handle);
        Vector3 movedHandle = Handles.Slider(
            handle,
            Vector3.forward,
            handleSize * 0.05f,
            Handles.ConeHandleCap,
            0.01f);
        if (EditorGUI.EndChangeCheck())
        {
            using (var undo = new ScopedUndo("Set Length"))
            {
                undo.RecordObject(mask);
                mask.Length += movedHandle.z - handle.z;
                if (mask.Length < 0.0f) mask.Length = 0.0f;
            }
        }
    }

}
