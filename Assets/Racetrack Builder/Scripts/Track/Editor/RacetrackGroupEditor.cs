using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RacetrackGroup))]
public class RacetrackGroupEditor : Editor
{
    static bool showParameters = false;
    static bool showUISettings = false;
    static bool showCopyForPrefabSettings = false;

    public override void OnInspectorGUI()
    {
        var group = (RacetrackGroup)target;

        // Detect changes to properties
        var obj = new SerializedObject(group);

        showParameters = EditorGUILayout.Foldout(showParameters, "Parameters");
        if (showParameters)
        {
            RacetrackEditorUtil.PropertyEditors(obj, true, "SegmentLength", "BankAngleInterpolation", "WideningInterpolation", "RemoveInternalFaces", "RespawnHeight", "RespawnZOffset");
            GUILayout.Space(RacetrackConstants.SpaceHeight);
        }

        showUISettings = EditorGUILayout.Foldout(showUISettings, "UI settings");
        if (showUISettings)
        {
            RacetrackEditorUtil.PropertyEditors(obj, true, "ShowManipulationHandles", "ShowOnScreenButtons", "AutoUpdate");
            GUILayout.Space(RacetrackConstants.SpaceHeight);
        }

        showCopyForPrefabSettings = EditorGUILayout.Foldout(showCopyForPrefabSettings, "Copy for prefab settings");
        if (showCopyForPrefabSettings)
        {
            RacetrackEditorUtil.PropertyEditors(obj, true, "MoveStartToOrigin", "AlignStart", "CreateStartMarker", "CreateEndMarker");
            GUILayout.Space(RacetrackConstants.SpaceHeight);
        }

        // Apply changes
        obj.ApplyModifiedProperties();

        GUILayout.BeginHorizontal(); 
        GUILayout.Label(" ", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
        if (GUILayout.Button("Update tracks", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
        {
            using (var undo = new ScopedUndo("Update tracks"))
            {
                UpdateTracks(RacetrackEditor.UpdateTrack);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(" ", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
        if (GUILayout.Button("Recreate tracks", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
        {
            using (var undo = new ScopedUndo("Recreate tracks"))
            {
                UpdateTracks(track => RacetrackEditor.BuildTrack(track));
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(" ", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
        if (GUILayout.Button("Create secondary UVs", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
        {
            if (EditorUtility.DisplayDialog("Create secondary UVs", "Racetrack meshes will be rebuilt with secondary UV coordinates for baked lighting.\nThis operation is relatively slow, and should be done immediately before backing lighting.\nSecondary UV data will be lost when track is next modified.", "Proceed", "Cancel"))
            {
                using (var undo = new ScopedUndo("Create secondary UVs"))
                {
                    UpdateTracks(track => RacetrackEditor.BuildTrack(track, true));
                }
            }
        }
        GUILayout.EndHorizontal();
    }

    private void UpdateTracks(Action<Racetrack> updateAction)
    {
        var tracks = ((RacetrackGroup)target).GetComponentsInChildren<Racetrack>();
        foreach (var track in tracks)
            updateAction(track);
    }

    [MenuItem("GameObject/3D Object/Racetrack Group", false, 10)]
    static void CreateNewRacetrackGroup(MenuCommand menuCommand)
    {
        using (var undo = new ScopedUndo("Create Racetrack Group"))
        {
            // Create object
            var obj = new GameObject("Racetrack Group");
            obj.isStatic = true;

            // Set parent and alignment
            GameObjectUtility.SetParentAndAlign(obj, menuCommand.context as GameObject);

            // Undo logic
            undo.RegisterCreatedObjectUndo(obj);

            // Create component
            var racetrackGroup = obj.AddComponent<RacetrackGroup>();

            // Create racetrack
            RacetrackEditor.CreateRacetrack(obj, undo);
        }
    }


}
