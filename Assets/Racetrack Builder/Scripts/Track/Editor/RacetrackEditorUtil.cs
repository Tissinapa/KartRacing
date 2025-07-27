using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

public static class RacetrackEditorUtil
{
    public static void PropertyEditors(SerializedObject obj, bool enabled, params string[] names)
    {
        bool saveEnabled = GUI.enabled;
        try
        {
            GUI.enabled = enabled;
            foreach (var name in names)
            {
                var prop = obj.FindProperty(name);
                if (prop != null)
                    EditorGUILayout.PropertyField(prop);
                else
                    Debug.LogError("RacetrackEditorUtil.PropertyEditors: Invalid property name '" + name + "', for object of class: " + obj.targetObject.GetType().Name);
            }
        }
        finally
        {
            GUI.enabled = saveEnabled;
        }
    }

    public static void ConfigureRacetrackObjectDefaults(GameObject obj, out Racetrack racetrack, out RacetrackCurve curve, RacetrackMeshTemplate template = null)
    {
        // Create a racetrack with a curve
        racetrack = obj.AddComponent<Racetrack>();
        curve = racetrack.AddCurve();

        // If no mesh template supplied, attempt to load the "asphalt poles" mesh template
        if (template == null)
        {
            var templateObj = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Racetrack Builder/Prefabs/Track Templates/Higher res/asphalt poles.prefab");
            template = templateObj != null ? templateObj.GetComponent<RacetrackMeshTemplate>() : null;
        }

        // Assign template to the curve
        if (template != null)
        {
            curve.Template = template;
        }
    }

    public static void DrawConnectionHandles(RacetrackConnector[] connectors, Racetrack[] racetracks, bool isJunctionMode)
    {
        var allConnectors = GameObject.FindObjectsOfType<RacetrackConnector>();
        var allRacetracks = GameObject.FindObjectsOfType<Racetrack>();

        if (connectors == null)
            connectors = allConnectors;
        if (racetracks == null)
            racetracks = allRacetracks;

        // Find drag handles
        var racetrackHandles = racetracks
            .Where(r => r.Path.Segments.Any() && r.MeshOverrun != RacetrackMeshOverrunOption.Loop)
            .SelectMany(r => new[] {
                new RacetrackHandleInfo(r, true),
                new RacetrackHandleInfo(r, false)
            })
            .ToArray();
        var connectorHandles = connectors
            .Select(c => new ConnectorHandleInfo(c))
            .ToArray();

        if (isJunctionMode)
            racetrackHandles = racetrackHandles.Where(h => !h.IsConnected).ToArray();
        else
            connectorHandles = connectorHandles.Where(h => !allRacetracks.Any(r => r.StartConnector == h.connector || r.EndConnector == h.connector)).ToArray();

        // Drag connector handles
        Handles.color = isJunctionMode ? Color.red : Color.blue;
        foreach (var c in connectorHandles)
        {
            if (!isJunctionMode)
            {
                if (Event.current.type == EventType.Repaint)
                    Handles.DotHandleCap(0, c.Pos3, Quaternion.identity, c.Size, EventType.Repaint);
                continue;
            }

            EditorGUI.BeginChangeCheck();
            var fmh_88_63_638865555227096240 = Quaternion.identity; Vector3 dragPos3 = Handles.FreeMoveHandle(c.Pos3, c.Size, Vector3.one * 0.5f, Handles.DotHandleCap);
            Vector2 dragPos2 = HandleUtility.WorldToGUIPoint(dragPos3);
            if (EditorGUI.EndChangeCheck())
            {
                // Find nearest racetrack handle
                var nearest = (from r in racetrackHandles
                               let dist = (r.Pos2 - dragPos2).magnitude
                               where dist < 25.0f
                               orderby dist
                               select r).FirstOrDefault();
                if (nearest != null)
                {
                    // Connect start of racetrack to connector
                    if (nearest.IsStart && nearest.Racetrack.StartConnector != c.connector)
                    {
                        using (var undo = new ScopedUndo("Connect " + c.connector.gameObject.name + " to racetrack " + nearest.Racetrack.gameObject.name + " start"))
                        {
                            // Unlink any other racetracks
                            UnlinkRacetracksFromConnector(c.connector, undo);
                            
                            // Link racetrack to connector
                            undo.RecordObject(nearest.Racetrack);
                            nearest.Racetrack.StartConnector = c.connector;
                            RacetrackEditor.ConnectStart(nearest.Racetrack, undo);

                            // Position junction so connector aligns to racetrack
                            var junction = c.connector.GetComponentInParent<RacetrackJunction>();
                            if (junction != null)
                            {
                                undo.RecordObject(junction.transform);
                                junction.AlignConnectorToRacetrack(c.connector, nearest.Racetrack, true);
                                undo.SetTransformParent(junction.transform, nearest.Racetrack.transform.parent);

                                // Update all racetracks connected to junction
                                var connectedRacetracks = junction.GetConnectors().Select(jc => jc.GetConnectedRacetrack()).Where(r => r != null);
                                foreach (var racetrack in connectedRacetracks)
                                {
                                    RacetrackEditor.ConnectTrack(racetrack);
                                }
                            }

                            // Update racetrack 
                            RacetrackEditor.ConnectTrack(nearest.Racetrack);
                        }
                    }

                    // Connect end of racetrack to connector
                    if (!nearest.IsStart && nearest.Racetrack.EndConnector != c.connector) 
                    {
                        using (var undo = new ScopedUndo("Connect " + c.connector.gameObject.name + " to racetrack " + nearest.Racetrack.gameObject.name + " end"))
                        {
                            // Unlink any other racetracks
                            UnlinkRacetracksFromConnector(c.connector, undo);

                            // Link racetrack to connector
                            undo.RecordObject(nearest.Racetrack);
                            nearest.Racetrack.EndConnector = c.connector;
                            RacetrackEditor.ConnectEnd(nearest.Racetrack, undo);

                            // Position junction so connector aligns to racetrack
                            var junction = c.connector.GetComponentInParent<RacetrackJunction>();
                            if (junction != null)
                            {
                                undo.RecordObject(junction.transform);
                                junction.AlignConnectorToRacetrack(c.connector, nearest.Racetrack, false);
                                undo.SetTransformParent(junction.transform, nearest.Racetrack.transform.parent);

                                // Update all racetracks connected to junction
                                var connectedRacetracks = junction.GetConnectors().Select(jc => jc.GetConnectedRacetrack()).Where(r => r != null);
                                foreach (var racetrack in connectedRacetracks)
                                {
                                    RacetrackEditor.ConnectTrack(racetrack);
                                }
                            }

                            // Update racetrack 
                            RacetrackEditor.ConnectTrack(nearest.Racetrack);
                        }
                    }
                }                    
            }
        }

        // Drag racetrack handles
        Handles.color = isJunctionMode ? Color.blue : Color.red;
        foreach (var r in racetrackHandles)
        {
            if (isJunctionMode)
            {
                if (Event.current.type == EventType.Repaint)
                    Handles.DotHandleCap(0, r.Pos3, Quaternion.identity, r.Size, EventType.Repaint);
                continue;
            }

            EditorGUI.BeginChangeCheck();
            var fmh_183_63_638865555227117244 = Quaternion.identity; Vector3 dragPos3 = Handles.FreeMoveHandle(r.Pos3, r.Size, Vector3.one * 0.5f, Handles.DotHandleCap);
            Vector2 dragPos2 = HandleUtility.WorldToGUIPoint(dragPos3);
            if (EditorGUI.EndChangeCheck())
            {
                // Find nearest connector handle
                var nearest = (from c in connectorHandles
                               let dist = (c.Pos2 - dragPos2).magnitude
                               where dist < 25.0f
                               orderby dist
                               select c).FirstOrDefault();
                if (nearest != null)
                {
                    var junction = nearest.connector.GetComponentInParent<RacetrackJunction>();

                    // Connect start/end of racetrack to connector
                    if (r.IsStart && r.Racetrack.StartConnector != nearest.connector)
                    {
                        using (var undo = new ScopedUndo("Connect racetrack " + r.Racetrack.gameObject.name + " start to connector " + nearest.connector.gameObject.name))
                        {
                            // Unlink any other racetracks
                            UnlinkRacetracksFromConnector(nearest.connector, undo);

                            // Link racetrack to connector
                            undo.RecordObject(r.Racetrack);
                            r.Racetrack.StartConnector = nearest.connector;
                            undo.RecordObject(r.Racetrack.transform);
                            RacetrackEditor.ConnectStart(r.Racetrack, undo);
                            undo.SetTransformParent(junction.transform, r.Racetrack.transform.parent);

                            // Update racetrack
                            RacetrackEditor.ConnectTrack(r.Racetrack);
                        }
                    }

                    if (!r.IsStart && r.Racetrack.EndConnector != nearest.connector)
                    {
                        using (var undo = new ScopedUndo("Connect racetrack " + r.Racetrack.gameObject.name + " end to connector " + nearest.connector.gameObject.name))
                        {
                            // Unlink any other racetracks
                            UnlinkRacetracksFromConnector(nearest.connector, undo);
                            
                            // Add new curve to end of racetrack
                            if (!r.IsConnected)
                                r.Racetrack.CreateCurve();

                            // Link racetrack to connector
                            undo.RecordObject(r.Racetrack);
                            r.Racetrack.EndConnector = nearest.connector;
                            RacetrackEditor.ConnectEnd(r.Racetrack, undo);
                            undo.SetTransformParent(junction.transform, r.Racetrack.transform.parent);

                            // Update racetrack
                            RacetrackEditor.ConnectTrack(r.Racetrack);
                        }
                    }
                }
            }
        }
    }

    public static void DrawRacetrackPath(List<RacetrackCurve> selectedCurves, Racetrack track)
    {
        // Render track path
        var path = track.Path;
        Vector3 lastPos = track.transform.TransformPoint(track.StartCurvePosition);         // Start drawing from the track origin
        Vector3? lastPivotPos = null;

        // Find first and last segment of current curve, for positioning manipulation handles
        int counter = 0;                                    // Counter used to skip segments for performance
        for (int i = 0; i < path.Segments.Count; i++)
        {
            RacetrackSegment seg = path.Segments[i];
            bool isCurve = selectedCurves.Any(c => c.Index == seg.Curve.Index);

            // Determine whether to draw a line segment
            if (i == path.Segments.Count - 1                                  // End of track reached
                || path.Segments[i + 1].Curve.Index != seg.Curve.Index)       // Or end of curve reached
            {
                counter = 0;
            }
            else
            {
                counter++;
                if (isCurve && counter >= 8 || counter >= 40)
                    counter = 0;
            }

            if (counter == 0)
            {
                // Highlight current curve
                Color curveCol;
                if (isCurve)
                    curveCol = Color.white;
                else
                {
                    // Otherwise draw in alternating shades, based on the type of curve
                    if (seg.Curve.Type == RacetrackCurveType.Arc)
                        curveCol = (seg.Curve.Index % 2) == 0 ? RacetrackConstants.CurveColor1 : RacetrackConstants.CurveColor2;
                    else
                        curveCol = (seg.Curve.Index % 2) == 0 ? RacetrackConstants.BezierColor1 : RacetrackConstants.BezierColor2;
                }

                // Draw line segment
                Vector3 pos = track.transform.TransformPoint(seg.Position);
                Handles.color = curveCol;
                Handles.DrawLine(lastPos, pos);
                lastPos = pos;

                // Draw bank pivot
                Vector3? pivotPos = seg.BankPivotX != 0.0f
                        ? (Vector3?)track.transform.TransformPoint(seg.GetSegmentToTrack().MultiplyPoint(new Vector3(seg.BankPivotX, 0.0f, 0.0f)))
                        : null;
                if (pivotPos != null && lastPivotPos != null)
                {
                    Handles.color = RacetrackConstants.BankPivotColor;
                    Handles.DrawDottedLine(lastPivotPos.Value, pivotPos.Value, 3.0f);
                }
                lastPivotPos = pivotPos;
            }
        }
    }

    public static void CreateAssetFolders(string path)
    {
        // Split into folders
        var folders = path.Split('/').Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

        // Must be in the main Assets folder
        if (folders.Count == 0 || folders[0] != "Assets")
            throw new System.Exception(string.Format("Invalid folder path '{0}'. Path must be a sub-folder of 'Assets'", path));

        // Create sub folders
        var parentPath = "Assets";
        for (var i = 1; i < folders.Count; i++)
        {
            var subPath = parentPath + "/" + folders[i];
            if (!AssetDatabase.IsValidFolder(subPath))
                AssetDatabase.CreateFolder(parentPath, folders[i]);
            parentPath = subPath;
        }
    }

    public static void SaveScriptableObjectChanges(ScriptableObject obj)
    {
        var assetPath = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrWhiteSpace(assetPath))
            AssetDatabase.ForceReserializeAssets(new string[] { assetPath }, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
    }

    private static void UnlinkRacetracksFromConnector(RacetrackConnector connector, ScopedUndo undo)
    {
        foreach (var racetrack in GameObject.FindObjectsOfType<Racetrack>().Where(r => r.StartConnector == connector))
        {
            undo.RecordObject(racetrack);
            racetrack.StartConnector = null;
        }
        foreach (var racetrack in GameObject.FindObjectsOfType<Racetrack>().Where(r => r.EndConnector == connector))
        {
            undo.RecordObject(racetrack);
            racetrack.EndConnector = null;
        }
    }

    public static void CopyRacetrackSectionForPrefab(Racetrack track, List<RacetrackCurve> curves)
    {
        var firstCurve = curves.First();
        var lastCurve = curves.Last();
        var editorSettings = track.GetEditorSettings();

        // Check meshes have all been saved as assets
        var meshes = curves.SelectMany(c => c.GetComponentsInChildren<MeshFilter>().Select(mf => new { mesh = mf.sharedMesh, component = (Component)mf }))
            .Union(curves.SelectMany(c => c.GetComponentsInChildren<MeshCollider>().Select(mc => new { mesh = mc.sharedMesh, component = (Component)mc })))
            .Where(m => m != null)
            .ToList();
        var unsaved = meshes.FirstOrDefault(m => string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(m.mesh)));
        if (unsaved != null)
        {
            Debug.LogError(string.Format("Mesh '{0}' has not been saved as a project asset", unsaved.mesh.name), unsaved.component);
            Selection.activeObject = unsaved.component.gameObject;
            return;
        }

        // Create parent object
        using (var undo = new ScopedUndo("Create copy for prefab"))
        {
            var parentObject = new GameObject();
            undo.RegisterCreatedObjectUndo(parentObject);
            parentObject.name = "Racetrack section";
            parentObject.isStatic = true;

            var clonedObjects = new List<GameObject>();

            // Clone and add curves
            foreach (var curve in curves)
            {
                var clone = GameObject.Instantiate(curve);
                clonedObjects.Add(clone.gameObject);

                // Add to parent
                clone.transform.parent = parentObject.transform;

                // Find and remove all Racetrack Builder components from clone
                var components = clone.GetComponentsInChildren<MonoBehaviour>()
                    .Where(c => c is RacetrackCurve
                        || c is RacetrackTemplateCopy
                        || c is RacetrackContinuous
                        || c is RacetrackSpaced
                        || c is RacetrackSpacedCopy
                        || c is RacetrackSpacingGroup
                        || c is RacetrackUVGenerator
                        || c is RacetrackWidenRanges
                        || c is RacetrackSurface)
                    .ToList();
                foreach (var component in components)
                    GameObject.DestroyImmediate(component);
            }

            // Find start pt transform
            float startZ = track.Curves
                .TakeWhile(c => c.Index < firstCurve.Index)
                .Sum(c => c.Length);
            float startZOffset;
            var startSeg = track.Path.GetSegmentAndOffset(startZ, out startZOffset);
            var worldFromStartpt = startSeg.GetSegmentToTrack(startZOffset);

            // Determine transform to objects content into prefab space
            var worldFromPrefab = worldFromStartpt;
            if (!editorSettings.MoveStartToOrigin)
                worldFromPrefab.SetColumn(3, new Vector4(0, 0, 0, 1));
            if (editorSettings.AlignStart == CopyForPrefabAlignType.No)
                worldFromPrefab = RacetrackUtil.ClearRotation(worldFromPrefab);
            else if (editorSettings.AlignStart == CopyForPrefabAlignType.YAxis)
                worldFromPrefab = RacetrackUtil.AlignYAxisToWorldY(worldFromPrefab);
            var prefabFromWorld = worldFromPrefab.inverse;

            // Add object corresponding to the start of the racetrack
            if (editorSettings.CreateStartMarker)
            {
                // Create object
                var startMarker = new GameObject("Start marker");
                undo.RegisterCreatedObjectUndo(startMarker);
                startMarker.transform.parent = parentObject.transform;

                // Find position in prefab space
                var prefabFromStartpt = prefabFromWorld * worldFromStartpt;

                // Position start marker
                startMarker.transform.position = prefabFromStartpt.GetColumn(3);
                startMarker.transform.rotation = prefabFromStartpt.rotation;
            }

            // Add object corresponding to the end of the racetrack
            if (editorSettings.CreateEndMarker)
            {

                // Create object
                var endMarker = new GameObject("End marker");
                undo.RegisterCreatedObjectUndo(endMarker);
                endMarker.transform.parent = parentObject.transform;

                // Calculate effective end transform
                float endZ = track.Curves
                    .TakeWhile(c => c.Index <= lastCurve.Index)
                    .Sum(c => c.Length);
                float endZOffset;
                var endSeg = track.Path.GetSegmentAndOffset(endZ, out endZOffset);
                var worldFromEndpt = endSeg.GetSegmentToTrack(endZOffset);
                if (editorSettings.AlignStart == CopyForPrefabAlignType.No)
                    worldFromEndpt = RacetrackUtil.ClearRotation(worldFromEndpt);
                else if (editorSettings.AlignStart == CopyForPrefabAlignType.YAxis)
                    worldFromEndpt = RacetrackUtil.AlignYAxisToWorldY(worldFromEndpt);

                // Find position in prefab space
                var prefabFromEndpt = prefabFromWorld * worldFromEndpt;

                // Position end marker
                endMarker.transform.position = prefabFromEndpt.GetColumn(3);
                endMarker.transform.rotation = prefabFromEndpt.rotation;
            }

            // Transform clones so that first clone aligns to identity
            foreach (var obj in clonedObjects)
            {
                var newTransform = prefabFromWorld * obj.transform.localToWorldMatrix;
                obj.transform.position = newTransform.GetColumn(3);
                obj.transform.rotation = newTransform.rotation;
            }

            Selection.activeObject = parentObject;
        }
    }

    private abstract class HandleInfo
    {
        public Vector3 Pos3 { get; private set; }
        public Vector2 Pos2 { get; private set; }
        public float Size { get; private set; }

        protected HandleInfo(Vector3 pos3)
        {
            this.Pos3 = pos3;
            this.Pos2 = HandleUtility.WorldToGUIPoint(this.Pos3);
            this.Size = HandleUtility.GetHandleSize(this.Pos3) * 0.05f;
        }
    }

    private class RacetrackHandleInfo : HandleInfo
    {
        public Racetrack Racetrack { get; private set; }
        public bool IsStart { get; private set; }

        public RacetrackHandleInfo(Racetrack racetrack, bool isStart) : base(GetPos(racetrack, isStart))
        {
            this.Racetrack = racetrack;
            this.IsStart = isStart;
        }

        public RacetrackConnector Connector
        {
            get { return this.IsStart ? this.Racetrack.StartConnector : this.Racetrack.EndConnector; }
        }

        public bool IsConnected
        {
            get { return this.Connector != null; }
        }

        private static Vector3 GetPos(Racetrack racetrack, bool isStart)
        {
            Vector3 pos = Vector3.zero;
            var segments = racetrack.Path.Segments;
            if (segments.Any())
                pos = isStart ? segments.First().Position : segments.Last().Position;
            return racetrack.transform.TransformPoint(pos);
        }
    }

    private class ConnectorHandleInfo : HandleInfo
    {
        public RacetrackConnector connector;

        public ConnectorHandleInfo(RacetrackConnector connector) : base(connector.transform.position)
        {
            this.connector = connector;
        }
    }
}
