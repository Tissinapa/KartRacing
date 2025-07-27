using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RacetrackCurve)), CanEditMultipleObjects]
public class RacetrackCurveEditor : Editor
{
    /// <summary>
    /// Miscellaneous section foldout state
    /// </summary>
    static bool showMisc = false;
    static bool isEndPosRelative = false;

    static Lazy<GUIStyle> labelStyle = new Lazy<GUIStyle>(() => new GUIStyle { wordWrap = true });
    static Lazy<GUIStyle> headingStyle = new Lazy<GUIStyle>(() => new GUIStyle { fontStyle = FontStyle.Bold });

    private List<Group> groups = new List<Group>();

    private class Group
    {
        public Racetrack track;
        public List<RacetrackCurve> curves;
    }

    /// <summary>
    /// Render inspector
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Ensure editor host services are used
        RacetrackHostServices.Instance = RacetrackEditorServices.Instance;

        groups.Clear();

        // Find curve and track
        var curves = targets.Cast<RacetrackCurve>().Where(c => c.Track != null).ToList();
        if (!curves.Any())
            return;

        // Group selected curves by racetrack
        groups.AddRange(curves.GroupBy(c => c.Track).Select(g => new Group { track = g.Key, curves = g.ToList() }));

        var track = curves.First().Track;
        var editorSettings = track.GetEditorSettings();

        // Display curve index
        if (curves.Count == 1)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Curve #", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
            GUILayout.Label(curves.First().Index.ToString());
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label(string.Format("Editing {0} curves", curves.Count));
        }
        GUILayout.Space(RacetrackConstants.SpaceHeight);

        // Detect changes to properties.
        // Specifically check for Y axis change, as we must make a correction in the next bezier curve
        var obj = serializedObject; 
        obj.Update();
        var prevYAngle = curves.Select(c => c.Angles.y).ToList();

        // Main properties
        bool isLastCurve = curves.Any(c => c.Index == track.Curves.Count - 1);
        bool isConnected = isLastCurve && (track.EndConnector != null || track.MeshOverrun == RacetrackMeshOverrunOption.Loop);
        if (isConnected)
        {
            GUILayout.Label("Curve is connected to " + (track.EndConnector != null ? track.EndConnector.gameObject.name : "start of racetrack"), labelStyle.Value);
            if (GUILayout.Button("Disconnect", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                using (var undo = new ScopedUndo("Disconnect " + track.gameObject.name + " from " + (track.EndConnector != null ? track.EndConnector.gameObject.name : "itself")))
                {
                    undo.RecordObject(track);
                    if (track.EndConnector != null)
                        track.EndConnector = null;
                    else
                        track.MeshOverrun = RacetrackMeshOverrunOption.Extrapolate;
                }
            }
        }
        RacetrackEditorUtil.PropertyEditors(obj, !isConnected, "Type");
        GUILayout.Space(RacetrackConstants.SpaceHeight);
        if (curves.Count == 1) {
            GUILayout.Label("Bezier", headingStyle.Value);
            isEndPosRelative = GUILayout.Toggle(isEndPosRelative, "Show Relative End Position");
            bool saveEnabled = GUI.enabled;
            GUI.enabled = curves.First().Type == RacetrackCurveType.Bezier && !isConnected;
            try
            {
                var prop = obj.FindProperty("EndPosition");
                Vector3 pos = isEndPosRelative
                    ? prop.vector3Value - curves.First().transform.localPosition
                    : prop.vector3Value;
                Vector3 newPos = EditorGUILayout.Vector3Field(prop.displayName, pos);
                prop.vector3Value = isEndPosRelative
                    ? curves.First().transform.localPosition + newPos
                    : newPos;
            }
            finally
            {
                GUI.enabled = saveEnabled;
            }
            //RacetrackEditorUtil.PropertyEditors(obj, curve.Type == RacetrackCurveType.Bezier && !isConnected, "EndPosition");
            RacetrackEditorUtil.PropertyEditors(obj, curves.First().Type == RacetrackCurveType.Bezier, "StartControlPtDist", "EndControlPtDist");
        }
        RacetrackEditorUtil.PropertyEditors(obj, curves.All(c => c.Type == RacetrackCurveType.Arc), "Length");
        RacetrackEditorUtil.PropertyEditors(obj, !isConnected, "Angles", "BankAngleInterpolation", "BankPivotX");
        RacetrackEditorUtil.PropertyEditors(obj, true, "Widening", "WideningInterpolation", "Template");
        GUILayout.Space(RacetrackConstants.SpaceHeight);

        // Miscellaneous foldout section
        showMisc = EditorGUILayout.Foldout(showMisc, "Miscellaneous");
        if (showMisc)
        {
            RacetrackEditorUtil.PropertyEditors(obj, true, "IsJump", "AlignMeshesToEnd", "CanRespawn", "RaiseTerrain", "LowerTerrain", "RemoveStartInternalFaces", "RemoveEndInternalFaces");
            if (!isLastCurve && curves.Count == 1)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(" ", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
                if (GUILayout.Button("Split track after curve", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
                {
                    using (var undo = new ScopedUndo("Split racetrack"))
                    {
                        var newTrack = track.SplitAtCurve(curves.First().Index + 1);
                        var newCurves = newTrack.Curves;
                        if (newCurves.Any())
                            Selection.activeGameObject = newCurves.First().gameObject;
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        // Apply changes
        if (track != null)
        {
            if (obj.ApplyModifiedProperties())
            {
                // Any changes invalidate the racetrack path
                track.InvalidatePath();
            }

            // Correct next bezier if angle has changed
            for (int i = 0; i < curves.Count; i++)
            {
                if (curves[i].Angles.y != prevYAngle[i])
                {
                    AdjustBezierForAngleChange(curves[i], track, curves[i].Angles.y - prevYAngle[i]);
                }
            }
        }

        DrawButtons(track, curves);

        // Rebuild track in response to button changes
        if (track != null && track.IsUpdateRequired && editorSettings.AutoUpdate)
        {
            RacetrackEditor.UpdateTrack(track);
        }
    }

    /// <summary>
    /// Racetrack on scene UI.
    /// Displays the path, with the current curve highlighted.
    /// Includes handles for manipulating curve length and angles (and position for beziers)
    /// </summary>
    public void OnSceneGUI()
    {
        Tools.current = Tool.None;

        // Ensure editor host services are used
        RacetrackHostServices.Instance = RacetrackEditorServices.Instance;

        // Find curve and track
        var curve = (RacetrackCurve)target;
        var track = curve.Track;
        var group = groups.FirstOrDefault(g => g.track == track);
        if (group == null) return;

        var editorSettings = track.GetEditorSettings();

        // Draw racetrack and highlighted curves only for the first curve.
        // (Avoid unnecessary renders, as Unity immediate mode drawing is a little slow.)
        if (curve == group.curves.First())
        {
            RacetrackEditorUtil.DrawRacetrackPath(group.curves, track);
        }

        // Draw manipulation handles
        var firstSeg = track.Path.Segments.FirstOrDefault(s => s.Curve.Index == curve.Index);
        var lastSeg = track.Path.Segments.LastOrDefault(s => s.Curve.Index == curve.Index);
        bool isLastCurve = curve.Index == track.Curves.Count - 1;
        bool isConnected = isLastCurve && (track.EndConnector != null || track.MeshOverrun == RacetrackMeshOverrunOption.Loop);
        if (editorSettings.ShowManipulationHandles && lastSeg != null && !isConnected)
        {
            // Detect changes to properties.
            // Specifically check for Y axis change, as we must make a correction in the next bezier curve
            bool isModified = false;
            float prevYAngle = curve.Angles.y;

            // Get EndPosition and orientation in world space
            Vector3 pos = track.transform.TransformPoint(curve.EndPosition);
            var rotation = track.transform.rotation * Quaternion.Euler(lastSeg.Direction);
            Vector3 dir = track.transform.TransformVector(lastSeg.PositionDelta);

            // Draw rotate handle
            Vector3 rotationHandlePos = curve.Type == RacetrackCurveType.Bezier ? pos : curve.transform.position;
            EditorGUI.BeginChangeCheck();
            Quaternion newRotation = Handles.RotationHandle(rotation, rotationHandlePos);
            if (EditorGUI.EndChangeCheck())
            {
                newRotation = Quaternion.Inverse(track.transform.rotation) * newRotation;
                var angles = newRotation.eulerAngles;
                angles.y -= firstSeg.Direction.y;                   // Y axis angle is relative. X and Z are absolute
                angles.x = Mathf.Clamp(RacetrackUtil.LocalAngle(angles.x), -90.0f, 90.0f);
                angles.y = Mathf.Clamp(RacetrackUtil.LocalAngle(angles.y), -180.0f, 180.0f);
                angles.z = Mathf.Clamp(RacetrackUtil.LocalAngle(angles.z), -90.0f, 90.0f);
                using (var undo = new ScopedUndo("Change angles"))
                {
                    undo.RecordObject(target);
                    float yAngleDelta = angles.y - curve.Angles.y;
                    curve.Angles = angles;
                    isModified = true;
                }
            }

            // Draw length handle for arcs
            if (curve.Type == RacetrackCurveType.Arc)
            {
                // Draw length handle
                Handles.color = Color.white;
                EditorGUI.BeginChangeCheck();
                float size = HandleUtility.GetHandleSize(pos) * 0.2f;
                Vector3 newPos = Handles.Slider(pos, dir, size, Handles.ConeHandleCap, 1.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    float newLength = RacetrackUtil.SnapToNearest(curve.Length + Vector3.Dot((newPos - pos), dir), editorSettings.SegmentLength);
                    if (curve.Length != newLength && newLength >= 1.0f)
                    {
                        using (var undo = new ScopedUndo("Change curve length"))
                        {
                            undo.RecordObject(target);
                            curve.Length = newLength;
                            isModified = true;
                        }
                    }
                }
            }

            // Draw move handle for beziers
            if (curve.Type == RacetrackCurveType.Bezier)
            {
                // Move handle
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(pos, rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    using (var undo = new ScopedUndo("Change end position"))
                    {
                        undo.RecordObject(target);
                        curve.EndPosition = track.transform.InverseTransformPoint(newPos);
                        isModified = true;
                    }
                }
            }

            // Apply changes
            if (isModified)
            {
                // Any changes invalidate the racetrack path
                track.InvalidatePath();

                // Correct next bezier if angle has changed
                if (curve.Angles.y != prevYAngle)
                {
                    AdjustBezierForAngleChange(curve, track, curve.Angles.y - prevYAngle);
                }
            }
        }

        // Draw on-screen buttons
        if (editorSettings.ShowOnScreenButtons && groups.Count == 1 && group.curves.Count == 1)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(60, 20, 250, 170));        // Leave space on left for Unity's floating toolbar
            DrawButtons(track, new List<RacetrackCurve> { curve });
            if (curve.Type == RacetrackCurveType.Bezier)
            {
                GUILayout.Space(RacetrackConstants.SpaceHeight);
                GUILayout.Label("Control points", headingStyle.Value);
                float newStartDist = GUILayout.HorizontalSlider(curve.StartControlPtDist, 0.0f, 1.0f);
                if (newStartDist != curve.StartControlPtDist)
                {
                    using (var undo = new ScopedUndo("Move start control pt"))
                    {
                        undo.RecordObject(target);
                        curve.StartControlPtDist = newStartDist;
                        track.InvalidatePath();
                    }
                }
                GUILayout.Space(16.0f);                             // Required in recent Unity versions. Otherwise sliders are nearly on top of each other.
                float newEndDist = GUILayout.HorizontalSlider(curve.EndControlPtDist, 0.0f, 1.0f);
                if (newEndDist != curve.EndControlPtDist)
                {
                    using (var undo = new ScopedUndo("Move end control pt"))
                    {
                        undo.RecordObject(target);
                        curve.EndControlPtDist = newEndDist;
                        track.InvalidatePath();
                    }
                }
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        // Rebuild track in response to button changes
        if (track.IsUpdateRequired && editorSettings.AutoUpdate && curve == group.curves.Last())
        {
            RacetrackEditor.UpdateTrack(track);
        }
    }

    [MenuItem("CONTEXT/RacetrackCurve/Copy for prefab")]
    public static void CopyForPrefab()
    {
        // Get selected curves
        var curves = Selection.gameObjects.Select(o => o.GetComponent<RacetrackCurve>())
            .Where(c => c != null)
            .OrderBy(c => c.Index)
            .ToList();
        if (!curves.Any())
            return;
        var track = curves[0].Track;
        if (track == null)
        {
            Debug.Log("First curve does not have a Racetrack parent");
            return;
        }

        RacetrackEditorUtil.CopyRacetrackSectionForPrefab(track, curves);
    }

    private Tool saveTool;

    private void OnEnable()
    {
        saveTool = Tools.current;
        Tools.current = Tool.None;
    }

    private void OnDisable()
    {
        Tools.current = saveTool;
    }

    private static void AdjustBezierForAngleChange(RacetrackCurve curve, Racetrack track, float yAngleDelta)
    {
        var nextBezier = track.Curves.FirstOrDefault(c => c.Type == RacetrackCurveType.Bezier && c.Index > curve.Index);
        if (nextBezier != null)
        {
            using (var undo = new ScopedUndo("Adjust bezier for Y angle change"))
            {
                undo.RecordObject(nextBezier);
                nextBezier.Angles.y = RacetrackUtil.LocalAngle(nextBezier.Angles.y - yAngleDelta);
                track.InvalidatePath();
            }
        }
    }

    /// <summary>
    /// Draw common action buttons.
    /// These are displayed in the inspector, and optionally on screen
    /// </summary>
    private static void DrawButtons(Racetrack track, List<RacetrackCurve> curves)
    {
        // Action buttons

        // Add/insert curve
        GUILayout.Space(RacetrackConstants.SpaceHeight);

        GUILayout.BeginHorizontal();
        if (track.MeshOverrun == RacetrackMeshOverrunOption.Extrapolate && track.EndConnector == null)
        {
            if (GUILayout.Button("Add curve", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                using (var undo = new ScopedUndo("Add curve"))
                {
                    // Add curve to end of racetrack
                    var newCurve = track.AddCurve();
                    RacetrackBuilder.PositionCurves(track.Curves, track.Path);
                    Selection.activeGameObject = newCurve.gameObject;
                    track.IsUpdateRequired = true;
                }
            }
        }

        if (curves.Count == 1)
        {
            bool isLastCurve = curves.First().Index == track.Curves.Count - 1;
            if (!isLastCurve)
            {
                if (GUILayout.Button("Insert curve", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
                {
                    using (var undo = new ScopedUndo("Insert curve"))
                    {
                        // Insert curve after the current curve
                        var newCurve = track.InsertCurve(curves.First().Index);
                        if (newCurve.Angles.y != 0.0f)
                            AdjustBezierForAngleChange(newCurve, track, newCurve.Angles.y);
                        RacetrackBuilder.PositionCurves(track.Curves, track.Path);
                        Selection.activeGameObject = newCurve.gameObject;
                        track.IsUpdateRequired = true;
                    }
                }
            }
        }
        if (track.MeshOverrun == RacetrackMeshOverrunOption.Loop && track.Curves.Count > 2
            || track.MeshOverrun == RacetrackMeshOverrunOption.Extrapolate && track.Curves.Count > 1)
        {
            if (GUILayout.Button(curves.Count == 1 ? "Delete curve" : string.Format("Delete {0} curves", curves.Count), GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
            {
                using (var undo = new ScopedUndo("Delete curve"))
                {
                    foreach (var curve in curves)
                    {
                        // Removing a curve is equivalent to setting the y angle to 0.
                        // Adjust next bezier curve accordingly
                        AdjustBezierForAngleChange(curve, track, 0.0f - curve.Angles.y);

                        bool wasLast = curve == track.Curves.LastOrDefault();
                        if (Selection.activeGameObject == curve.gameObject)
                        {
                            if (curve.Index > 0)
                                Selection.activeGameObject = track.Curves[curve.Index - 1].gameObject;
                            else
                                Selection.activeGameObject = track.Curves[curve.Index + 1].gameObject;
                        }
                        RacetrackHostServices.Instance.DestroyObject(curve.gameObject);
                        track.CurvesModified();
                        RacetrackBuilder.PositionCurves(track.Curves, track.Path);
                        track.IsUpdateRequired = true;

                        // Disconnect from junction/break circuit if end curve deleted.
                        if (wasLast)
                        {
                            track.EndConnector = null;
                            track.MeshOverrun = RacetrackMeshOverrunOption.Extrapolate;
                            track.InvalidatePath();
                        }
                    }
                }
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(RacetrackConstants.SpaceHeight);

        if (track != null && GUILayout.Button("Update track", GUILayout.MinHeight(RacetrackConstants.ButtonHeight)))
        {
            using (var undo = new ScopedUndo("Update track"))
            {
                RacetrackEditor.UpdateTrack(track);
            }
        }
        if (track != null && !string.IsNullOrWhiteSpace(track.LastUpdateMsg))
        {
            GUILayout.Label(track.LastUpdateMsg, labelStyle.Value);
        }
    }
}