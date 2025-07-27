using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RacetrackCarTracker))]
public class RacetrackCarTrackerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var tracker = (RacetrackCarTracker)target;
        DrawDefaultInspector();

        GUILayout.Space(20);
        if (GUILayout.Button("Reset car"))
        {
            Undo.RecordObject(target, "Reset car");
            tracker.PutCarOnRoad();
        }
    }
}
