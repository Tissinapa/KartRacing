using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Detects the progress of an object (e.g. the player's car) around a Racetrack.
/// Can detect when object has fallen off the track and respawn them.
/// Also collects lap time information.
/// Must be added to the object containing the rigid body to track.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RacetrackProgressTracker : MonoBehaviour
{
    // Components
    private Rigidbody carBody;

    [Header("Current position")]
    public Racetrack racetrack;
    public int currentCurve = 0;

    [Header("Parameters")]
    public int CurveSearchAhead = 2;            // # of curves to search ahead when checking whether player has progressed to the next curve. Does not count jumps (which are skipped)
    public float OffRoadTimeout = 10.0f;        // # of seconds player is off the road before they will be placed back on.
    public bool AutoReset = true;               // Enables the auto-respawn logic
    public bool CanGoBackwards = false;
    public Vector3 RayOffset = Vector3.zero;

    [Header("Lap counting")]
    public Racetrack FinishLineRacetrack;
    public int FinishLineCurve;
    public int lapCount = 0;
    public float LastLapTime = 0.0f;
    public float BestLapTime = 0.0f;
    public float CurrentLapTime = 0.0f;

    [Header("Working")]
    public float offRoadTimer = 0.0f;           // # of seconds since the player was last on the road.
    public bool isAboveRoad = false;

    // Working
    private Racetrack racetrackInstance;
    private bool isNegativeLap;
    private int iterationCounter;
    private int overallLapDelta;

    private Racetrack GetRacetrack()
    {
        // Racetrack explicitly specified
        if (racetrack != null)
            return racetrack;

        // Otherwise find and return first racetrack instance in scene
        if (racetrackInstance == null)
            racetrackInstance = Racetrack.AllRacetracks.FirstOrDefault();
        return racetrackInstance;
    }

    void Start()
    {
        Debug.LogWarning("RacetrackProgressTracker has been deprecated in favor of RacetrackCarTracker.");

        carBody = GetComponent<Rigidbody>();

        // Default finish line racetrack to current racetrack
        if (this.FinishLineRacetrack == null)
            this.FinishLineRacetrack = this.GetRacetrack();
    }

    void FixedUpdate()
    {
        // Assume off road until proven otherwise
        this.isAboveRoad = false;
        this.iterationCounter = 0;
        // Note: It is possible to create a looped racetrack that's all "jump". In which case the progress tracker 
        // can enter an infinite loop, skipping over the "jump" looking for the next solid piece of track.
        // Therefore we count iterations to detect and break infinite loops.
        this.overallLapDelta = 0;

        // Search along racetrack 
        var track = GetRacetrack();
        if (track != null)
        {
            try
            {
                if (CanGoBackwards)
                    SearchRacetrack(track, currentCurve, false, CurveSearchAhead, 0, 0);
                SearchRacetrack(track, currentCurve, true, CurveSearchAhead, 0, 0);
            }
            catch (SearchTerminatedException ex)
            {
                Debug.Log(ex.Message);
            }
        }

        // Off-road timer logic
        if (isAboveRoad || !AutoReset)
        {
            offRoadTimer = 0.0f;
        }
        else
        {
            offRoadTimer += Time.fixedDeltaTime;
            if (offRoadTimer > OffRoadTimeout)
                StartCoroutine(PutCarOnRoadCoroutine());
        }

        // Update lap timer
        CurrentLapTime += Time.fixedDeltaTime;

        // Lap completion
        if (overallLapDelta > 0)
            this.LapCompleted();
        else if (overallLapDelta < 0)
            this.BackwardLapCompleted();
    }

    /// <summary>
    /// Coroutine to put the car back on the road.
    /// Default implementation puts the car there instantly, but could be overridden in a 
    /// subclass to perform an animation.
    /// </summary>
    public virtual IEnumerator PutCarOnRoadCoroutine()
    {
        return RacetrackCoroutineUtil.Do(() => PutCarOnRoad());
    }

    /// <summary>
    /// Place the player car back on the road.
    /// Player is positioned above the last curve that they drove on that is flagged as "CanRespawn"
    /// </summary>
    public void PutCarOnRoad()
    {
        // Find racetrack and curve runtime information
        var track = GetRacetrack();
        if (track == null)
        {
            Debug.LogError("Racetrack instance not found. Cannot place car on track.");
            return;
        }
        var curveInfos = track.CurveInfos;
        if (curveInfos == null)
        {
            Debug.LogError("Racetrack curves have not been generated. Cannot place car on track.");
            return;
        }
        if (curveInfos.Length == 0)
        {
            Debug.LogError("Racetrack has no curves. Cannot place car on track.");
            return;
        }

        if (currentCurve < 0) currentCurve = 0;
        if (currentCurve >= curveInfos.Length) currentCurve = curveInfos.Length - 1;

        // Search backwards from current curve for a respawnable curve. Don't go back past 
        // the start of the track though (otherwise player could clock up an extra lap).
        int curveIndex = currentCurve;
        while (curveIndex > 0 && !curveInfos[curveIndex].CanRespawn)
            curveIndex--;

        // Position player at spawn point.
        // Spawn point is in track space, so must transform to get world space.
        var curveInfo = curveInfos[curveIndex];
        transform.position = track.transform.TransformPoint(curveInfo.RespawnPosition);
        transform.rotation = track.transform.rotation * curveInfo.RespawnRotation;

        // Kill all linear and angular velocity
        if (carBody != null)
        {
            carBody.linearVelocity = Vector3.zero;
            carBody.angularVelocity = Vector3.zero;
        }

        // Reset state
        offRoadTimer = 0.0f;
        currentCurve = curveIndex;
    }

    /// <summary>
    /// Get specific information about the car's postion and velocity relative to the racetrack.
    /// </summary>
    /// <remarks>
    /// Can be used for steering assistance, AI, determining which car is in the lead etc.
    /// </remarks>
    public CarState GetCarState()
    {
        var road = GetRacetrack();
        if (road == null || road.CurveInfos == null || !road.CurveInfos.Any() || carBody == null || !isAboveRoad)
            return new CarState { IsValid = false };

        var settings = road.GetEditorSettings();

        // Get curve index from progress tracker
        var state = new CarState { IsValid = true };
        int curveIndex = currentCurve;

        // Get curve information
        var curves = road.Curves;
        var infos = road.CurveInfos;
        if (curveIndex < 0 || curveIndex >= curves.Count)
        {
            Debug.LogError("Curve index " + curveIndex + " out of range. Must be 0 - " + (curves.Count - 1));
        }
        var curve = curves[curveIndex];
        var info = infos[curveIndex];

        // Calculate car position and direction in track space
        Matrix4x4 worldFromTrack = road.transform.localToWorldMatrix;
        Matrix4x4 trackFromWorld = worldFromTrack.inverse;
        Vector3 carPosTrack = trackFromWorld.MultiplyPoint(transform.position);
        Vector3 carDirTrack = trackFromWorld.MultiplyVector(transform.transform.TransformVector(Vector3.forward));
        Vector3 carVelTrack = trackFromWorld.MultiplyVector(carBody.linearVelocity);

        // Binary search for segment index
        float loZ = info.zOffset;
        float hiZ = info.zOffset + curve.Length;
        int lo = Mathf.FloorToInt(loZ / settings.SegmentLength);
        int hi = Mathf.FloorToInt(hiZ / settings.SegmentLength);

        // Allow for curve index to be too far ahead.
        // This is because curve index will often be supplied from the RacetrackProgressTracker component, 
        // which can be optimistic sometimes, depending on which curves generate geometry
        {
            float dp;
            int count = 0;
            do
            {
                var loSeg = road.Path.GetSegment(lo);
                dp = Vector3.Dot(carPosTrack - loSeg.Position, loSeg.PositionDelta);
                if (dp < 0.0f)
                {
                    hi = lo;
                    lo = lo - 50;
                    if (lo < 0)
                        lo = 0;
                }
                count++;
            } while (dp < 0.0f && count < 10);
        }

        while (hi > lo)
        {
            int mid = (lo + hi + 1) / 2;
            var midSeg = road.Path.GetSegment(mid);
            var dp = Vector3.Dot(carPosTrack - midSeg.Position, midSeg.PositionDelta);
            if (dp >= 0)
                lo = mid;
            else
                hi = mid - 1;
        }

        // Calculate car position and direction in segment space
        var seg = road.Path.GetSegment(lo);
        Matrix4x4 trackFromSeg = seg.GetSegmentToTrack();
        Matrix4x4 segFromTrack = trackFromSeg.inverse;

        Vector3 carPos = segFromTrack.MultiplyPoint(carPosTrack);
        Vector3 carDir = segFromTrack.MultiplyVector(carDirTrack);
        Vector3 carVel = segFromTrack.MultiplyVector(carVelTrack);
        float carAng = Mathf.Atan2(carDir.x, carDir.z) * Mathf.Rad2Deg;
        state.Segment = seg;
        state.SegmentIndex = lo;
        state.Position = carPos;
        state.Direction = carDir;
        state.Velocity = carVel;
        state.Angle = carAng;
        state.TrackFromSeg = trackFromSeg;
        state.SegFromTrack = segFromTrack;
        state.TrackDistance = lo * settings.SegmentLength + carPos.z;

        return state;
    }

    private void SearchJunction(RacetrackJunction j, Racetrack ignoreRacetrack, int remainingSteps, int delta, int lapDelta)
    {
        // Sanity check iterations
        if (++iterationCounter > 1000)
            throw new SearchTerminatedException("Infinite loop detected in RacetrackProgressTracker.");

        // Cast ray down from car towards junction
        var ray = new Ray(carBody.transform.TransformPoint(RayOffset), -j.transform.up);
        var hit = Physics.RaycastAll(ray)
            .OrderBy(h => h.distance)
            .Select(h => new
            {
                Surface = h.transform.GetComponent<RacetrackSurface>(),
                Junction = h.transform.GetComponent<RacetrackJunction>()
            })
            .FirstOrDefault(h => h.Surface != null || h.Junction != null);

        // Hit this junction?
        if (hit != null && hit.Junction == j)
        {
            isAboveRoad = true;
        }

        // Find connected racetracks
        var connectors = j.GetConnectors().Select(c => new {
            Connector = c,
            Racetrack = c.GetConnectedRacetrack()
        })
        .Where(c => c.Racetrack != null && c.Racetrack != ignoreRacetrack)
        .Select(c => new {
            c.Connector,
            c.Racetrack,
            IsForwards = c.Racetrack.StartConnector == c.Connector
        })
        .ToArray();

        // Continue search along connected racetracks

        // Backwards
        if (CanGoBackwards)
        {
            foreach (var connector in connectors.Where(c => !c.IsForwards))
            {
                SearchRacetrack(connector.Racetrack, connector.Racetrack.Curves.Count() - 1, false, remainingSteps, delta, lapDelta);
            }
        }

        // Forwards
        foreach (var connector in connectors.Where(c => c.IsForwards))
        {
            SearchRacetrack(connector.Racetrack, 0, true, remainingSteps, delta, lapDelta);
        }
    }

    private void SearchRacetrack(Racetrack t, int curveIndex, bool forward, int remainingSteps, int delta, int lapDelta)
    {
        var curves = t.Curves;

        if (!curves.Any())
            throw new SearchTerminatedException("Encountered racetrack with no curves!");

        while (curveIndex >= 0 && curveIndex < curves.Count)
        {
            // Sanity check iterations
            if (++iterationCounter > 1000)
                throw new SearchTerminatedException("Infinite loop detected in RacetrackProgressTracker.");

            // Detect "finish line" curve
            bool isAboveFinishLine = t == this.FinishLineRacetrack && curveIndex == FinishLineCurve;
            if (isAboveFinishLine)
            {
                if (delta > 0)
                    lapDelta = 1;       // Searching forward of car curve => Next lap
                else if (delta == 0)
                    lapDelta = 0;       // 0 implies car is already on the "finish line" curve. No lap change should be made.
            }

            // Skip jumps. Don't count them in "remainingSteps".
            if (!curves[curveIndex].IsJump)
            {
                // Ray cast from center of car towards curve
                var ray = new Ray(carBody.transform.TransformPoint(RayOffset), -t.CurveInfos[curveIndex].Normal);
                var hit = Physics.RaycastAll(ray)
                    .OrderBy(h => h.distance)
                    .Select(h => new
                    {
                        Surface = h.transform.GetComponent<RacetrackSurface>(),
                        Junction = h.transform.GetComponent<RacetrackJunction>()
                    })
                    .FirstOrDefault(h => h.Surface != null || h.Junction != null);

                // Hit this curve?
                if (hit != null && hit.Surface != null && hit.Surface.GetComponentInParent<Racetrack>() == t && hit.Surface.ContainsCurveIndex(curveIndex))
                {
                    racetrack = t;
                    currentCurve = curveIndex;
                    isAboveRoad = true;
                    overallLapDelta = lapDelta;         // Lock in lap change
                }

                // Search finished?
                if (--remainingSteps < 0)
                    return;
            }

            // Proceed to next curve
            curveIndex += forward ? 1 : -1;
            delta += forward ? 1 : -1;

            if (isAboveFinishLine && delta < 0)
                lapDelta = -1;
        }

        // Reached end of racetrack. 

        // Check connected junction (if one exists)
        var connector = forward ? t.EndConnector : t.StartConnector;
        var j = connector != null ? connector.GetComponentInParent<RacetrackJunction>() : null;
        if (j != null)
        {
            SearchJunction(j, t, remainingSteps, delta, lapDelta);
        }

        // Check start/end of racetrack if looped
        if (t.MeshOverrun == RacetrackMeshOverrunOption.Loop)
            SearchRacetrack(t, forward ? 0 : curves.Count - 1, forward, remainingSteps, delta, lapDelta);
    }

    /// <summary>
    /// Update state after lap completed
    /// </summary>
    private void LapCompleted()
    {
        if (!isNegativeLap)
        {
            lapCount++;

            // Update lap times
            LastLapTime = CurrentLapTime;
            CurrentLapTime = 0.0f;
            if (BestLapTime == 0.0f || LastLapTime < BestLapTime)
                BestLapTime = LastLapTime;
        }

        isNegativeLap = false;
    }

    private void BackwardLapCompleted()
    {
        isNegativeLap = true;
    }

    public struct CarState
    {
        /// <summary>
        /// Whether the state is valid.
        /// Can be invalid if the car is not currently above the racetrack.
        /// Other properties should be ignored when IsValid is false.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Segment the car is on
        /// </summary>
        public RacetrackSegment Segment { get; set; }

        /// <summary>
        /// Index of segment the car is on
        /// </summary>
        public int SegmentIndex { get; set; }

        /// <summary>
        /// Car position relative to segment
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Car direction relative to segment (Z axis = forward)
        /// </summary>
        public Vector3 Direction { get; set; }

        /// <summary>
        /// Car velocity relative to segment (Z axis = forward)
        /// </summary>
        public Vector3 Velocity { get; set; }

        /// <summary>
        /// Car angle relative to segment (0 = forward)
        /// </summary>
        public float Angle { get; set; }
        
        /// <summary>
        /// Segment space to track space transform
        /// </summary>
        public Matrix4x4 TrackFromSeg { get; set; }

        /// <summary>
        /// Track space to segment space transform
        /// </summary>
        public Matrix4x4 SegFromTrack { get; set; }

        /// <summary>
        /// Car distance down track
        /// </summary>
        public float TrackDistance { get; set; }
    }

    public class SearchTerminatedException : System.Exception
    {
        public SearchTerminatedException(string reason) : base("RacetrackProgressTracker search terminated: " + reason) { }
    }
}
