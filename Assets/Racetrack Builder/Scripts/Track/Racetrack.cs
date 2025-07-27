using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The main racetrack component.
/// Manages a collection of RacetrackCurve curves. Warps meshes around them to create 
/// the visual and physical racetrack model.
/// </summary>
[ExecuteInEditMode]
public class Racetrack : MonoBehaviour, IHasEditorSettings {

    private const int MaxSpacingGroups = 16;

    [RacetrackCurveAngles]
    public Vector3 StartCurveAngles = new Vector3();
    public Vector3 StartCurvePosition = new Vector3();
    public float StartBankPivotX = 0.0f;
    public RacetrackWidening StartCurveWidening;

    [Header("Mesh warping")]
    [Tooltip("Curves are converted into a sequence of small straight 'segments' of this length.")]
    public float SegmentLength = 0.25f;

    [Tooltip("How to interpolate between curve bank (Z) angles")]
    public Racetrack1DInterpolationType BankAngleInterpolation;

    [Tooltip("How to interpolate between 'widening' values")]
    public Racetrack1DInterpolationType WideningInterpolation;

    [Tooltip("Automatically remove internal faces between consecutive mesh templates of the same type")]
    public bool RemoveInternalFaces = false;

    [Header("Respawning")]
    [Tooltip("Height above road for car respawn points. Used by RacetrackProgressTracker")]
    public float RespawnHeight = 0.75f;
    public float RespawnZOffset = 2.0f;

    [Header("Looping")]
    [Tooltip("What to do with the part of the mesh that runs past the end of the last curve")]
    public RacetrackMeshOverrunOption MeshOverrun = RacetrackMeshOverrunOption.Extrapolate;

    [Tooltip("Offset looped meshes by this amount to avoid Z fighting. Applies when MeshOverrun is set to 'Loop'")]
    public float LoopYOffset = -0.001f;

    [Tooltip("Display rotate and translate handles for the curve shape (translate handles for Bezier curves only)")]
    public bool ShowManipulationHandles = true;

    [Tooltip("Show buttons (and other UI) in the main editor window")]
    public bool ShowOnScreenButtons = true;

    [Tooltip("Automatically update track after (some) curve changes")]
    public bool AutoUpdate = true;

    [Tooltip("Connect start of racetrack to connector")]
    public RacetrackConnector StartConnector;

    [Tooltip("Connect end of racetrack to connector")]
    public RacetrackConnector EndConnector;

    /// <summary>
    /// Runtime information for each curve. Used by RacetrackProgressTracker.
    /// </summary>
    [HideInInspector]
    [NonSerialized]         // Don't need to store. Calculated on startup.
    public CurveRuntimeInfo[] CurveInfos;

    [HideInInspector, Obsolete("Use Path.TotalLength instead.")]
    public float RacetrackLength
    {
        get { return this.Path.TotalLength; }
    }

    /// <summary>
    /// Singleton instance
    /// </summary>
    [Obsolete("Use explicit reference, or Racetrack.AllRacetracks.FirstOrDefault() if you know there is only one instance")]
    public static Racetrack Instance;

    /// <summary>
    /// Update required flag. Used by editor to flag that racetrack needs updating.
    /// </summary>
    public bool IsUpdateRequired { get; set; }

    /// <summary>
    /// Message from last RacetrackBuilder.Update() call. Displayed in inspector.
    /// </summary>
    public string LastUpdateMsg { get; set; }

    public static readonly List<Racetrack> AllRacetracks = new List<Racetrack>();

    private RacetrackMeshInfoCache meshInfoCache = new RacetrackMeshInfoCache();

    public Racetrack()
    {
        AllRacetracks.Add(this);
    }

    private void Awake()
    {
#pragma warning disable CS0618                                  // Suppress [Obsolete] warning
        Instance = this;
#pragma warning restore CS0618
        RacetrackBuilder.CalculateRuntimeInfo(this);            // Ensure runtime info is up to date
    }

    void Start()
    {
    }

    public void Reset()
    {
        SegmentLength = 0.25f;
        BankAngleInterpolation = Racetrack1DInterpolationType.Bezier;
        WideningInterpolation = Racetrack1DInterpolationType.Linear;
        RemoveInternalFaces = true;
        RespawnHeight = 0.75f;
        RespawnZOffset = 2.0f;
        LoopYOffset = 0.001f;
        ShowManipulationHandles = true;
        ShowOnScreenButtons = true;
        AutoUpdate = true;
    }

    void OnDestroy()
    {
#pragma warning disable CS0618
        if (Instance == this)
            Instance = null;
#pragma warning restore CS0618
        AllRacetracks.Remove(this);
    }

    /// <summary>
    /// Cached list of curves for performance.
    /// Caching is used at runtime only.
    /// </summary>
    private List<RacetrackCurve> cachedCurves;

    /// <summary>
    /// Get list of curves in order.
    /// </summary>
    /// <remarks>
    /// Curve objects are immediate child objects with the RacetrackCurve component.
    /// (Typically they are added with the "Add Curve" editor buttons, rather than 
    /// created manually).
    /// </remarks>
    public List<RacetrackCurve> Curves
    {
        get
        {
            // Assume at runtime that the curves will not be modified, and we can cache them in a local field.
            // When game is not running (i.e. in editor) we always recalculate the curves
            if (cachedCurves == null || !Application.IsPlaying(gameObject))
                cachedCurves = Enumerable.Range(0, gameObject.transform.childCount)
                            .Select(i => gameObject.transform.GetChild(i).GetComponent<RacetrackCurve>())
                            .Where(c => c != null)
                            .ToList();
            return cachedCurves;
        }
    }

    /// <summary>
    /// Indicates that the curves array has changed.
    /// Calling this method is only necessary at runtime.
    /// </summary>
    public void CurvesModified()
    {
        // Force curves to be recalculated when Curves is next evaluated
        this.cachedCurves = null;

        // Ensure path is recalculated also
        this.InvalidatePath();
    }

    // Current racetrack path
    private RacetrackPath path;

    // Logic to detect when path has changed, including via Undo/Redo actions.
    // "Required" ID is serialized and will change with undo/redo operations.
    // If does not match "actual" ID, then path needs to be recreated.
    [SerializeField]
    [HideInInspector]
    private string pathRequiredID = Guid.NewGuid().ToString();
    private string pathActualID;                            

    /// <summary>
    /// Get the racetrack path. Recalculate if necessary.
    /// </summary>
    public RacetrackPath Path
    {
        get
        {
            if (path == null || this.pathRequiredID != this.pathActualID)
            {
                path = new RacetrackPath(Curves, this.GetRacetrackPathParams());
                this.pathActualID = this.pathRequiredID;        // Path is now up to date
            }
            return path;
        }
    }

    /// <summary>
    /// Force racetrack path to be recalculated next time it is accessed.
    /// </summary>
    public void InvalidatePath()
    {
        RacetrackHostServices.Instance.ObjectChanging(this);
        this.pathRequiredID = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Add a curve to the end of the track.
    /// Copies previous curve settings.
    /// </summary>
    /// <returns>The new curve</returns>
    public RacetrackCurve AddCurve()
    {
        // Create curve
        var prevLastCurve = Curves.LastOrDefault();
        var curve = CreateCurve();

        // Copy defaults from last curve
        ConfigureNewCurve(prevLastCurve, curve);

        // Invalidate
        InvalidatePath();

        return curve;
    }

    public RacetrackCurve CreateCurve()
    {
        var curves = Curves;

        // Create new curve
        var obj = new GameObject();
        RacetrackHostServices.Instance.ObjectCreated(obj);
        obj.transform.parent = transform;
        obj.name = "Curve";
        obj.isStatic = gameObject.isStatic;
        var curve = obj.AddComponent<RacetrackCurve>();
        curve.Index = curves.Count;
        return curve;
    }

    /// <summary>
    /// Insert curve immediately after the specified curve
    /// </summary>
    /// <param name="index">Index of the curve to insert after</param>
    /// <returns>The new curve</returns>
    public RacetrackCurve InsertCurve(int index)
    {
        var curves = Curves;
        var lastCurve = curves[index];

        // Create new curve
        var obj = new GameObject();
        RacetrackHostServices.Instance.ObjectCreated(obj);
        obj.transform.parent = transform;
        obj.name = "Curve";
        obj.isStatic = gameObject.isStatic;
        var curve = obj.AddComponent<RacetrackCurve>();

        // Position immediately after previous curve
        curve.Index = index + 1;
        RacetrackHostServices.Instance.SetTransformParent(curve.transform, curve.transform.parent);       // This seems to be required to get undo support for SetSiblingIndex
        obj.transform.SetSiblingIndex(curve.Index);

        // Copy defaults from previous curve
        ConfigureNewCurve(lastCurve, curve);

        // Reindex following curves
        for (int i = index + 1; i < curves.Count; i++)
            curves[i].Index = i + 1;

        // Invalidate
        InvalidatePath();

        return curve;
    }

    /// <summary>
    /// Create a closed circuit racetrack by adding a joining curve at the end
    /// </summary>
    /// <returns>The newly added curve</returns>
    public RacetrackCurve CreateCircuit()
    {
        // Link the last curve to the start of the track
        var curves = Curves;
        if (curves.Count < 1)
            throw new ApplicationException("Must have at least 1 curve to create a circuit");

        RacetrackCurve newCurve = null;
        if (MeshOverrun != RacetrackMeshOverrunOption.Loop)
        {
            // Switch to looped mode
            RacetrackHostServices.Instance.ObjectChanging(this);
            MeshOverrun = RacetrackMeshOverrunOption.Loop;

            // Create a new curve to close the loop
            newCurve = CreateCurve();

            // Update curves array
            curves = Curves;
        }

        // Join last curve to first
        var first = curves.First();
        var last = curves.Last();
        last.Type = RacetrackCurveType.Bezier;
        last.EndPosition = first.transform.localPosition;

        // Line up angles
        var yAng = StartCurveAngles.y + curves.Sum(c => c.Angles.y) - last.Angles.y;
        float lastCurveX = StartCurveAngles.x;
        float lastCurveY = RacetrackUtil.LocalAngle(StartCurveAngles.y - yAng);
        float lastCurveZ = StartCurveAngles.z;
        last.Angles = new Vector3(lastCurveX, lastCurveY, lastCurveZ);

        // Line up width
        last.Widening = StartCurveWidening;

        // Invalidate
        InvalidatePath();

        // Return new curve created (or null if existing curve used)
        return newCurve;
    }

    public void ConnectRacetrack()
    {
        ConnectRacetrackStart();
        ConnectRacetrackEnd();
    }

    /// <summary>
    /// Connect the start of the racetrack to its connector.
    /// </summary>
    public void ConnectRacetrackStart()
    {
        var connect = this.StartConnector;
        if (connect == null)
            return;

        // Set curve start position and angles
        // Requires calculating the effective transform at the connection point
        // Transform needs to convert from connector space to racetrack space
        // i.e. racetrackFromConnector = racetrackFromWorld * worldFromConnector
        Matrix4x4 racetrackFromConnector = GetRacetrackFromConnectorMatrix(connect, false);

        // Position is straightforward
        this.StartCurvePosition = racetrackFromConnector.GetColumn(3);

        // Get euler angles in racetrack space from transform
        var connectorAngles = racetrackFromConnector.rotation.eulerAngles;

        float prevAngleY = this.StartCurveAngles.y;
        this.StartCurveAngles = new Vector3(
            RacetrackUtil.LocalAngle(connectorAngles.x),
            RacetrackUtil.LocalAngle(connectorAngles.y),
            RacetrackUtil.LocalAngle(connectorAngles.z));

        // Adjust Y angle of first bezier curve, so that the end point maintains the same direction.
        var bezier = this.Curves.FirstOrDefault(c => c.Type == RacetrackCurveType.Bezier);
        if (bezier != null)
            bezier.Angles.y = RacetrackUtil.LocalAngle(bezier.Angles.y + prevAngleY - connectorAngles.y);

        // BankPivotX must be 0 for clean connection
        this.StartBankPivotX = 0.0f;
    }

    public void ConnectRacetrackEnd()
    {
        var curves = Curves;
        var connect = this.EndConnector;
        if (connect == null || curves.Count < 1) return;

        // Set last curve endpoint and angles
        var last = curves.Last();

        // Requires calculating the effective transform at the connection point
        // Transform needs to convert from connector space to racetrack space
        // i.e. racetrackFromConnector = racetrackFromWorld * worldFromConnector
        Matrix4x4 racetrackFromConnector = GetRacetrackFromConnectorMatrix(connect, true);

        // Position is straightforward
        last.EndPosition = racetrackFromConnector.GetColumn(3);

        // Get euler angles in racetrack space from transform
        var connectorAngles = racetrackFromConnector.rotation.eulerAngles;

        var yAng = this.StartCurveAngles.y + curves.Sum(c => c.Angles.y) - last.Angles.y;
        float lastCurveX = RacetrackUtil.LocalAngle(connectorAngles.x);
        float lastCurveY = RacetrackUtil.LocalAngle(connectorAngles.y - yAng);
        float lastCurveZ = RacetrackUtil.LocalAngle(connectorAngles.z);
        last.Angles = new Vector3(lastCurveX, lastCurveY, lastCurveZ);

        // BankPivotX must be 0 for clean connection
        last.BankPivotX = 0.0f;
    }

    public Racetrack SplitAtCurve(int startCurveIndex)
    {
        // Create racetrack
        var obj = new GameObject();
        RacetrackHostServices.Instance.ObjectCreated(obj);
        obj.transform.parent = transform.parent;
        obj.transform.position = transform.position;
        obj.transform.rotation = transform.rotation;
        obj.name = "Racetrack";
        obj.isStatic = gameObject.isStatic;
        var newTrack = obj.AddComponent<Racetrack>();

        // Find segment at start of curve
        var curves = this.Curves;
        float curveZOffset = 0.0f;
        RacetrackMeshTemplate meshTemplate = null;          // Also find mesh template
        RacetrackWidening widening = new RacetrackWidening();
        for (int i = 0; i < startCurveIndex; i++)
        {
            var curve = curves[i];
            if (curve.Template != null)
                meshTemplate = curve.Template;
            widening = curves[i].Widening;
            curveZOffset += curves[i].Length;
        }
        int segIndex = Mathf.FloorToInt(curveZOffset / path.Params.SegmentLength);
        var seg = path.GetSegment(segIndex);

        // Set curve position as new track start position
        var trackFromSeg = seg.GetSegmentToTrack();
        newTrack.StartCurvePosition = trackFromSeg.GetColumn(3);
        newTrack.StartCurveAngles = trackFromSeg.rotation.eulerAngles;

        // Also set start widening
        newTrack.StartCurveWidening = widening;

        // Transfer curves from current racetrack to new track
        for (int i = startCurveIndex; i < curves.Count; i++)
            RacetrackHostServices.Instance.SetTransformParent(curves[i].transform, newTrack.transform);

        // Other fixups

        // Mesh template
        if (startCurveIndex < curves.Count)
        {
            var startCurve = curves[startCurveIndex];
            RacetrackHostServices.Instance.ObjectChanging(startCurve);
            startCurve.Template = meshTemplate;
        }

        // Connectors
        RacetrackHostServices.Instance.ObjectChanging(this);
        newTrack.EndConnector = this.EndConnector;
        this.EndConnector = null;

        // Looping
        this.MeshOverrun = RacetrackMeshOverrunOption.Extrapolate;
        newTrack.MeshOverrun = RacetrackMeshOverrunOption.Extrapolate;

        // Mark tracks as changed
        this.InvalidatePath();
        newTrack.InvalidatePath();

        return newTrack;
    }

    #region IHasEditorSettings

    public EditorSettings GetEditorSettings()
    {
        // If parent racetrack group exists, use its settings
        var settings = this.GetComponentInParent<RacetrackGroup>();
        if (settings != null)
            return settings.GetEditorSettings();

        // Otherwise fall back to settings stored on racetrack
        return new EditorSettings
        {
            SegmentLength = Math.Clamp(this.SegmentLength, 0.01f, 100.0f),
            BankAngleInterpolation = this.BankAngleInterpolation,
            WideningInterpolation = this.WideningInterpolation,
            RemoveInternalFaces = this.RemoveInternalFaces,

            RespawnHeight = this.RespawnHeight,
            RespawnZOffset = this.RespawnZOffset,

            AutoUpdate = this.AutoUpdate,
            ShowManipulationHandles = this.ShowManipulationHandles,
            ShowOnScreenButtons = this.ShowOnScreenButtons,

            // Use defaults for copy-for-prefab
            MoveStartToOrigin = true,
            AlignStart = CopyForPrefabAlignType.AllAxes,
            CreateStartMarker = false,
            CreateEndMarker = true
        };
    }

    #endregion

    #region Obsolete properties/methods

    // Provided for backwards compatibility with existing runtime code

    [Obsolete("Please use Path.Segments")]
    public List<RacetrackSegment> Segments
    {
        get { return Path.Segments; }
    }

    [Obsolete("Please use Path.GetSegment()")]
    public RacetrackSegment GetSegment(int i)
    {
        return Path.GetSegment(i);
    }

    [Obsolete("Please use Path.GetSegmentTransform()")]
    private void GetSegmentTransform(RacetrackSegment seg, Transform dstTransform)
    {
        Path.GetSegmentTransform(seg, dstTransform);
    }

    #endregion

    /// <summary>
    /// Get parameters for generating the racetrack path
    /// </summary>
    /// <returns></returns>
    private RacetrackPathParams GetRacetrackPathParams()
    {
        var settings = GetEditorSettings();
        return new RacetrackPathParams {
            Transform = this.transform.localToWorldMatrix,
            SegmentLength = settings.SegmentLength,
            StartCurveAngles = this.StartCurveAngles,
            StartCurvePosition = this.StartCurvePosition,
            StartBankPivotX = this.StartBankPivotX,
            StartCurveWidening = this.StartCurveWidening,
            BankAngleInterpolation = settings.BankAngleInterpolation,
            WideningInterpolation = settings.WideningInterpolation,
            Overrun = this.MeshOverrun,
            LoopYOffset = this.LoopYOffset
        };        
    }

    /// <summary>
    /// Configure properties on a newly added/inserted curve, based on the previous one
    /// </summary>
    /// <param name="lastCurve">The previous curve</param>
    /// <param name="curve">The newly inserted curve</param>
    private void ConfigureNewCurve(RacetrackCurve lastCurve, RacetrackCurve curve)
    {
        // Copy values from last curve
        if (lastCurve != null)
        {
            curve.Length = lastCurve.Length;
            curve.Angles = lastCurve.Angles;
            curve.BankPivotX = lastCurve.BankPivotX;
            curve.Widening = lastCurve.Widening;
            curve.IsJump = lastCurve.IsJump;
            curve.CanRespawn = lastCurve.CanRespawn;
            if (lastCurve.Type == RacetrackCurveType.Bezier)
            {
                curve.Type = RacetrackCurveType.Arc;
                curve.Angles.y = 0.0f;
                curve.Length = 50;
                new RacetrackPath(Curves, this.GetRacetrackPathParams());       // Evaluating path sets curve end point.
            }
            curve.Type = lastCurve.Type;
            curve.RaiseTerrain = lastCurve.RaiseTerrain;
            curve.LowerTerrain = lastCurve.LowerTerrain;
        }
    }

    /// <summary>
    /// Get matrix to convert from connector's local coordinate space to racetrack coordinate space
    /// </summary>
    /// <param name="connect">The connector</param>
    /// <param name="isInwardFacing">True to use the connector's unmodified transform (inward facing). False to rotate 180 degrees (outward facing)</param>
    /// <returns>A 4x4 transformation matrix to convert from connector space to racetrack space</returns>
    private Matrix4x4 GetRacetrackFromConnectorMatrix(RacetrackConnector connect, bool isInwardFacing)
    {
        // Matrix to convert from the connector's local coordinate space to racetrack coordinate space
        Matrix4x4 worldFromRacetrack = transform.localToWorldMatrix;
        Matrix4x4 racetrackFromWorld = worldFromRacetrack.inverse;
        Matrix4x4 worldFromConnector = connect.transform.localToWorldMatrix;
        if (!isInwardFacing)
            worldFromConnector = worldFromConnector * Matrix4x4.Rotate(Quaternion.Euler(0.0f, 180.0f, 0.0f));
        return racetrackFromWorld * worldFromConnector;
    }

    /// <summary>
    /// Runtime information attached to a curve.
    /// Can be used to track player progress along racetrack, and respawn after crashes.
    /// See RacetracProgressTracker
    /// </summary>
    [Serializable]
    public class CurveRuntimeInfo
    {
        public Vector3 Normal;                  // Upward normal in middle of curve
        public Vector3 RespawnPosition;
        public Quaternion RespawnRotation;
        public bool IsJump;                     // Copy of RacetrackCurve.IsJump
        public bool CanRespawn;                 // Copy of RacetrackCurve.CanRespawn
        public float zOffset;
    }
}

/// <summary>
/// Algorithm for interpolating between 1D floating point values
/// </summary>
public enum Racetrack1DInterpolationType
{
    Linear,
    Bezier,
    BezierUnclamped,
    Inherit
}

/// <summary>
/// What to do with the mesh that extends beyond the end of the last curve
/// </summary>
public enum RacetrackMeshOverrunOption
{
    Extrapolate,                // Mesh continues on in a straight line
    Loop                        // Loop around and follow the first curve (intended for closed-loop racetracks)
}
