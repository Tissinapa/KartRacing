using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RacetrackRelative))]
public class RacetrackRelativeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var component = (RacetrackRelative)target;
        var parent = (MonoBehaviour)component.GetComponentInParent<RacetrackCurve>() ?? component.GetComponentInParent<Racetrack>();
        var obj = new SerializedObject(component);
        RacetrackEditorUtil.PropertyEditors(obj, parent != null, "position");
        if (obj.ApplyModifiedProperties())
        {
            Undo.RecordObject(component.transform, "Position on racetrack");
            component.PositionOnRacetrack();
        }

        // Edit rotation as Euler angles.
        // This is required for Unity 2018.4.31f1 (later Unity versions have built in Quaternion property drawers)        
        var angles = component.rotation.eulerAngles;
        var updatedAngles = EditorGUILayout.Vector3Field("Rotation", angles);
        if (updatedAngles != angles)
        {
            using (var undo = new ScopedUndo("Update rotation"))
            {
                undo.RecordObject(component);
                component.rotation = Quaternion.Euler(updatedAngles);
                undo.RecordObject(component.transform);
                component.PositionOnRacetrack();
            }
        }

        if (parent == null)
            EditorGUILayout.HelpBox("Object must be placed underneath a Racetrack or Racetrack Curve in the scene hierarchy.", MessageType.Warning);
        else if (GUILayout.Button("Update position"))
        {
            Undo.RecordObject(component.transform, "Position on racetrack");
            component.PositionOnRacetrack();
        }
    }

    // Note: Scene GUI disabled, as when it doesn't work it can translate objects
    // large distances or set their coordinates to "infinity".

    //public void OnSceneGUI()
    //{
    //    var component = (RacetrackRelative)target;
    //    var parent = (MonoBehaviour)component.GetComponentInParent<RacetrackCurve>() ?? component.GetComponentInParent<Racetrack>();
    //    if (parent == null) return;

    //    if (Tools.current != Tool.None)
    //    {
    //        saveTool = Tools.current;
    //        Tools.current = Tool.None;
    //    }

    //    if (saveTool == Tool.Move)
    //    {
    //        EditorGUI.BeginChangeCheck();
    //        Vector3 newPos = Handles.PositionHandle(component.transform.position, component.transform.rotation);
    //        if (EditorGUI.EndChangeCheck())
    //        {
    //            using (var undo = new ScopedUndo("Position on racetrack"))
    //            {
    //                undo.RecordObject(component);
    //                var offset = component.transform.InverseTransformPoint(newPos);
    //                component.position += offset;
    //                undo.RecordObject(component.transform);
    //                component.PositionOnRacetrack();
    //            }
    //        }
    //    }
    //    else if (saveTool == Tool.Rotate)
    //    {
    //        EditorGUI.BeginChangeCheck();
    //        Quaternion newRotation = Handles.RotationHandle(component.transform.rotation, component.transform.position);
    //        if (EditorGUI.EndChangeCheck())
    //        {
    //            using (var undo = new ScopedUndo("Position on racetrack"))
    //            {
    //                undo.RecordObject(component);
    //                var delta = Quaternion.Inverse(component.transform.rotation) * newRotation;
    //                component.rotation *= delta;
    //                undo.RecordObject(component.transform);
    //                component.PositionOnRacetrack();
    //            }
    //        }
    //    }
    //}

    //private Tool saveTool;

    //private void OnEnable()
    //{
    //    saveTool = Tools.current;
    //    Tools.current = Tool.None;
    //}

    //private void OnDisable()
    //{
    //    Tools.current = saveTool;
    //}
}
