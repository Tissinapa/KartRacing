using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RacetrackTerrainModifier))]
public class RacetrackTerrainModifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        // Update terrain button
        if (GUILayout.Button("Update Terrain", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
        {
            using (var undo = new ScopedUndo("Update Terrain"))
            {
                var modifier = (RacetrackTerrainModifier)target;
                undo.RecordObject(modifier);
                modifier.ModifyTerrain();
            }
        }
    }
}
