using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RacetrackConnector))]
public class RacetrackConnectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Get connector object
        var connector = (RacetrackConnector)target;

        // Regular properties
        var obj = new SerializedObject(connector);
        RacetrackEditorUtil.PropertyEditors(obj, true, "MeshTemplate");
        obj.ApplyModifiedProperties();

        // Update/new button
        ConnectorButton(connector);
    }

    public void OnSceneGUI()
    {
        var connector = (RacetrackConnector)target;
        RacetrackEditorUtil.DrawConnectionHandles(new[] { connector }, null, true);
    }

    public static void ConnectorButton(RacetrackConnector connector)
    {
        // Look for connected racetrack
        var racetrack = connector.GetConnectedRacetrack();

        if (racetrack != null)
        {
            if (GUILayout.Button("Disconnect", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                using (var undo = new ScopedUndo("Disconnect racetrack from connector"))
                {
                    undo.RecordObject(racetrack);
                    if (racetrack.EndConnector == connector)
                    {
                        racetrack.EndConnector = null;
                    }
                    if (racetrack.StartConnector == connector)
                    {
                        racetrack.StartConnector = null;                        
                    }

                    Selection.activeObject = racetrack;
                }
            }
        }
        else
        {
            if (GUILayout.Button("New racetrack", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                using (var undo = new ScopedUndo("Create Racetrack at connector"))
                {
                    // Create object
                    var obj = new GameObject("Racetrack");
                    obj.isStatic = true;

                    // Undo logic
                    undo.RegisterCreatedObjectUndo(obj);

                    // Place object underneath junction parent, and set local transform to identity
                    var junction = connector.GetComponentInParent<RacetrackJunction>();
                    if (junction != null)
                    {
                        obj.transform.parent = junction.transform.parent;
                        obj.transform.localPosition = Vector3.zero;
                        obj.transform.localRotation = Quaternion.identity;
                    }

                    // Configure racetrack defaults
                    RacetrackCurve curve;
                    RacetrackEditorUtil.ConfigureRacetrackObjectDefaults(obj, out racetrack, out curve, connector.MeshTemplate);
                    curve.RemoveStartInternalFaces = RemoveInternalFacesOption.Yes;

                    // Connect to connector
                    racetrack.StartConnector = connector;
                    racetrack.ConnectRacetrackStart();

                    // Build racetrack meshes
                    if (curve.Template != null)
                    {
                        RacetrackBuilder.Build(racetrack);
                        RacetrackBuilder.CalculateRuntimeInfo(racetrack);
                    }
                }
            }
        }
    }
}
