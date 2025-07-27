using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Racetrack))]
public class RacetrackEditor : Editor
{
    /// <summary>
    /// Section foldout state
    /// </summary>
    static bool showStartState = false;
    static bool showParameters = false;
    static bool showMisc = false;
    static bool showUISettings = false;
    static bool showConnectors = false;

    static Lazy<GUIStyle> labelStyle = new Lazy<GUIStyle>(() => new GUIStyle { wordWrap = true });

    public override void OnInspectorGUI()
    {
        // Ensure editor host services are used
        RacetrackHostServices.Instance = RacetrackEditorServices.Instance;

        // Find track and curves
        var track = (Racetrack)target;
        var curves = track.Curves;

        var editorSettings = track.GetEditorSettings();

        // Detect changes to properties
        var obj = new SerializedObject(track);

        // Detect connector changes
        var prevStartConnector = track.StartConnector;
        var prevEndConnector = track.EndConnector;
        var prevAngleY = track.StartCurveAngles.y;

        // Parameters
        bool showAllSettings = track.GetComponentInParent<RacetrackGroup>() == null;
        showParameters = EditorGUILayout.Foldout(showParameters, "Parameters");
        if (showParameters)
        {
            if (showAllSettings)
                RacetrackEditorUtil.PropertyEditors(obj, true, "SegmentLength", "BankAngleInterpolation", "WideningInterpolation", "RemoveInternalFaces", "RespawnHeight", "RespawnZOffset");
            RacetrackEditorUtil.PropertyEditors(obj, true, "MeshOverrun", "LoopYOffset");
            GUILayout.Space(RacetrackConstants.SpaceHeight);
        }

        // Connectors
        showConnectors = EditorGUILayout.Foldout(showConnectors, "Connectors");
        if (showConnectors)
        {
            RacetrackEditorUtil.PropertyEditors(obj, true, "StartConnector", "EndConnector");
            GUILayout.Space(RacetrackConstants.SpaceHeight);
        }

        // Start state
        showStartState = EditorGUILayout.Foldout(showStartState, "Curve start state");
        if (showStartState)
        {
            RacetrackEditorUtil.PropertyEditors(obj, track.StartConnector == null, "StartCurvePosition", "StartCurveAngles", "StartBankPivotX");
            RacetrackEditorUtil.PropertyEditors(obj, true, "StartCurveWidening");
            GUILayout.Space(RacetrackConstants.SpaceHeight);
        }

        if (showAllSettings)
        {
            showUISettings = EditorGUILayout.Foldout(showUISettings, "UI settings");
            if (showUISettings)
            {
                RacetrackEditorUtil.PropertyEditors(obj, true, "ShowManipulationHandles", "ShowOnScreenButtons", "AutoUpdate");
                GUILayout.Space(RacetrackConstants.SpaceHeight);
            }
        }

        // Apply changes
        if (obj.ApplyModifiedProperties())
        {
            track.InvalidatePath();

            if (track.StartCurveAngles.y != prevAngleY)
            {
                // Adjust Y angle of first bezier curve, so that the end point maintains the same direction.
                var bezier = track.Curves.FirstOrDefault(c => c.Type == RacetrackCurveType.Bezier);
                if (bezier != null)
                {
                    using (var undo = new ScopedUndo("Adjust bezier for Y angle change"))
                    {
                        undo.RecordObject(bezier);
                        bezier.Angles.y = bezier.Angles.y + prevAngleY - track.StartCurveAngles.y;
                    }
                }
            }
        }

        // Connector changes require a full reconnect
        if (track.StartConnector != prevStartConnector || track.EndConnector != prevEndConnector)
        {
            if (curves.Any())
            {
                // If end connector was just set, configure last curve to connect 
                if (prevEndConnector == null && track.EndConnector != null)
                {
                    using (var undo = new ScopedUndo("Connect last curve"))
                    {
                        ConnectEnd(track, undo);
                    }
                }

                // Likewise start connector and first curve
                if (prevStartConnector == null && track.StartConnector != null)
                {
                    using (var undo = new ScopedUndo("Connect first curve"))
                    {
                        ConnectStart(track, undo);
                    }
                }
            }

            // Update racetrack connections
            ConnectTrack(track);
        }

        // Miscellaneous action buttons
        showMisc = EditorGUILayout.Foldout(showMisc, "Miscellaneous");
        if (showMisc)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(" ", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
            if (GUILayout.Button("Delete meshes", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                using (var undo = new ScopedUndo("Delete track meshes"))
                {
                    RacetrackBuilder.DeleteTemplateCopies(track.Curves);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(" ", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
            if (GUILayout.Button("Clear templates", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                if (EditorUtility.DisplayDialog("Clear templates", "Really clear templates from all curves?", "Yes - Remove them", "Cancel"))
                {
                    using (var undo = new ScopedUndo("Remove templates"))
                    {
                        ClearTemplates(track);
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(" ", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
            if (GUILayout.Button("Combine static batches", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                StaticBatchingUtility.Combine(track.gameObject);
            }
            GUILayout.EndHorizontal();
        }

        // Action buttons
        GUILayout.Space(RacetrackConstants.SpaceHeight);

        GUILayout.BeginHorizontal();
        GUILayout.Label(" ", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
        if (GUILayout.Button("Update track", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
        {
            using (var undo = new ScopedUndo("Update track"))
            {
                UpdateTrack(track);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(" ", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
        if (GUILayout.Button("Recreate track", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
        {
            using (var undo = new ScopedUndo("Recreate track"))
            {
                BuildTrack(track);
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
                    BuildTrack(track, true);
                }
            }
        }
        GUILayout.EndHorizontal();

        if (curves.Count >= 2 && track.StartConnector == null && track.EndConnector == null)
        {
            GUILayout.Space(RacetrackConstants.SpaceHeight);
            GUILayout.BeginHorizontal();
            GUILayout.Label(" ", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
            if (GUILayout.Button("Create closed circuit", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                using (var undo = new ScopedUndo("Create closed circuit"))
                {
                    try
                    {
                        var newCurve = track.CreateCircuit();
                        if (newCurve != null)
                            Selection.activeGameObject = newCurve.gameObject;
                        track.IsUpdateRequired = true;
                    }
                    catch (ApplicationException ex)
                    {
                        Debug.LogError(ex.Message);
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        if (!string.IsNullOrWhiteSpace(track.LastUpdateMsg))
        {
            GUILayout.Space(RacetrackConstants.SpaceHeight);
            GUILayout.Label(track.LastUpdateMsg, labelStyle.Value);
        }

        // Rebuild track in response to button changes
        if (track.IsUpdateRequired && editorSettings.AutoUpdate)
            UpdateTrack(track);
    }

    public void OnSceneGUI()
    {
        var track = (Racetrack)target;

        // Draw racetrack path
        RacetrackEditorUtil.DrawRacetrackPath(new List<RacetrackCurve>(), track);

        // Draw arrows indicating direction
        if (Event.current.type == EventType.Repaint)
        {
            var segments = track.Path.Segments;
            Handles.color = Color.white;
            for (int i = 40; i < segments.Count; i += 80)
            {
                var seg = segments[i];
                var pos = track.transform.TransformPoint(seg.Position);
                var dir = track.transform.TransformVector(seg.PositionDelta);
                float size = HandleUtility.GetHandleSize(pos) * 0.1f;
                Handles.ConeHandleCap(0, pos, Quaternion.LookRotation(dir), size, EventType.Repaint);
            }
        }

        // Draw connector handles
        RacetrackEditorUtil.DrawConnectionHandles(GameObject.FindObjectsOfType<RacetrackConnector>(), new[] { track }, false);
    }

    [MenuItem("GameObject/3D Object/Racetrack", false, 10)]
    static void CreateNewRacetrack(MenuCommand menuCommand)
    {
        using (var undo = new ScopedUndo("Create Racetrack"))
        {
            CreateRacetrack(menuCommand.context as GameObject, undo);
        }
    }

    public static void CreateRacetrack(GameObject parent, ScopedUndo undo)
    {
        // Create object
        var obj = new GameObject("Racetrack");
        obj.isStatic = true;

        // Set parent and alignment
        GameObjectUtility.SetParentAndAlign(obj, parent);

        // Undo logic
        undo.RegisterCreatedObjectUndo(obj);

        Racetrack racetrack;
        RacetrackCurve curve;
        RacetrackEditorUtil.ConfigureRacetrackObjectDefaults(obj, out racetrack, out curve);

        // Build racetrack meshes
        if (curve.Template != null)
        {
            RacetrackBuilder.Build(racetrack);
            RacetrackBuilder.CalculateRuntimeInfo(racetrack);
        }

        // Select new racetrack
        Selection.activeObject = curve;
    }

    public static void BuildTrack(Racetrack track, bool generateSecondaryUVs = false)
    {
        // Clear mesh info cache and force path to be recalculated
        RacetrackMeshInfoCache.Instance.Clear();
        track.InvalidatePath();

        // Rebuild track
        track.ConnectRacetrack();
        RacetrackBuilder.Build(track, generateSecondaryUVs);
        RacetrackBuilder.CalculateRuntimeInfo(track);
        track.IsUpdateRequired = false;
    }

    public static void ConnectTrack(Racetrack track)
    {
        track.ConnectRacetrack();
        track.InvalidatePath();
        UpdateTrack(track);
    }

    public static void UpdateTrack(Racetrack track)
    {
        RacetrackBuilder.Update(track);
        RacetrackBuilder.CalculateRuntimeInfo(track);
        PositionObjects(track);
        track.IsUpdateRequired = false;
    }

    public static void ClearTemplates(Racetrack track)
    {
        foreach (var curve in track.Curves)
        {
            if (curve.Template != null)
            {
                RacetrackHostServices.Instance.ObjectChanging(curve);
                curve.Template = null;
            }
        }
        RacetrackBuilder.DeleteTemplateCopies(track.Curves);
    }

    public static void ConnectStart(Racetrack track, ScopedUndo undo)
    {
        var first = track.Curves.First();
        undo.RecordObject(first);
        first.RemoveStartInternalFaces = RemoveInternalFacesOption.Yes;
    }

    public static void ConnectEnd(Racetrack track, ScopedUndo undo)
    {
        var last = track.Curves.Last();
        undo.RecordObject(last);
        last.Type = RacetrackCurveType.Bezier;
        last.RemoveEndInternalFaces = RemoveInternalFacesOption.Yes;
        last.AlignMeshesToEnd = true;
    }

    [MenuItem("CONTEXT/Racetrack/Copy for prefab")]
    public static void CopyForPrefab()
    {
        if (Selection.activeGameObject == null)
            return;
        var track = Selection.activeGameObject.GetComponent<Racetrack>();
        if (track == null)
            return;
        var curves = track.Curves;
        if (!curves.Any())
            return;

        RacetrackEditorUtil.CopyRacetrackSectionForPrefab(track, curves);
    }

    public static void PositionObjects(Racetrack track)
    {
        var objects = Racetrack.FindObjectsOfType<RacetrackRelative>();
        foreach (var o in objects)
            o.PositionOnRacetrack();
    }
}
