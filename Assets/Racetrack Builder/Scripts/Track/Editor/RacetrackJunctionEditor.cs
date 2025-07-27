using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RacetrackJunction))]
public class RacetrackJunctionEditor : Editor
{
    static Lazy<GUIStyle> headingStyle = new Lazy<GUIStyle>(() => new GUIStyle { fontStyle = FontStyle.Bold });

    public override void OnInspectorGUI()
    {
        // Find junction and connectors
        var junction = (RacetrackJunction)target;
        var connectors = junction.GetConnectors();

        // Show connectors
        GUILayout.Space(RacetrackConstants.SpaceHeight);
        GUILayout.Label("Connectors", headingStyle.Value);
        foreach (var connector in connectors)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(connector.gameObject.name, GUILayout.Width(EditorGUIUtility.labelWidth - 5));
            RacetrackConnectorEditor.ConnectorButton(connector);
            GUILayout.EndHorizontal();
        }

        // Update all racetracks button
        var racetracks = connectors.Select(c => c.GetConnectedRacetrack()).Where(r => r != null);
        if (racetracks.Any())
        {
            GUILayout.Space(RacetrackConstants.SpaceHeight);
            if (GUILayout.Button("Update racetracks", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                foreach (var racetrack in racetracks)
                {
                    RacetrackEditor.ConnectTrack(racetrack);
                }
            }
        }
    }

    public void OnSceneGUI()
    {
        // Find junction and connectors
        var junction = (RacetrackJunction)target;
        var connectors = junction.GetConnectors();

        // Draw connection handles
        if (connectors.Any())
            RacetrackEditorUtil.DrawConnectionHandles(connectors, null, true);
    }
}
