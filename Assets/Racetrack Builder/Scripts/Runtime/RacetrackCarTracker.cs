using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]

// Tracks a car's (or any rigid body's) progress around a race track.
// This could be a single Racetrack object, or multiple Racetrack objects connected by junctions.
// Has logic 
public class RacetrackCarTracker : MonoBehaviour
{
    // Components
    private Rigidbody carBody;

    [Header("Current position")]
    [Tooltip("The Racetrack object the car is on. Must be explicitly set if the scene contains more than one.")]
    public Racetrack racetrack;

    [Tooltip("Index of the curve the car is on (0 origin).")]
    public int currentCurve = 0;

    [Header("Parameters")]
    [Tooltip("Number of curves to search ahead from last known car position. Must be at least one. Larger numbers will allow the car to skip corners. Note: 'isJump' curves are not counted.")]
    public int CurveSearchAhead = 1;

    [Tooltip("Number of seconds off road before the car is automatically placed back on the road, if 'Auto Reset' is enabled. Be sure to allow for necessary air-time if your track has jumps.")]
    public float OffRoadTimeout = 10.0f;

    [Tooltip("Automatically place car back on road. See 'Off Road Timeout'.")]
    public bool AutoReset = true;

    [Tooltip("Whether the car is allowed to go backwards. If not the car will be considered 'off road' when moving backwards, and 'Auto Reset' logic will apply (if enabled)")]
    public bool CanGoBackwards = false;

    [Tooltip("Offset of the ray used to test whether the track is underneath the car.")]
    public Vector3 RayOffset = Vector3.zero;

    [Header("Lap counting")]

    [Tooltip("Racetrack object containing the 'finish line' curve. Used to detect when a lap has been completed (see 'Finish Line Curve'). If null, will default to 'Racetrack' property value")]
    public Racetrack FinishLineRacetrack;

    [Tooltip("Index of the 'finish line' curve (0 origin). Used to detect when a lap has been completed (see 'Finish Line Racetrack')")]
    public int FinishLineCurve;

    public int lapCount = 0;
    public float LastLapTime = 0.0f;
    public float BestLapTime = 0.0f;
    public float CurrentLapTime = 0.0f;

    [Header("Working")]
    public float offRoadTimer = 0.0f;           // # of seconds since the player was last on the road.
    public bool isAboveRoad = false;

    // Working
    private List<CurveOrJunction> visitedSet = new List<CurveOrJunction>();
    private List<CurveOrJunction> currentSet = new List<CurveOrJunction>();
    private bool isNegativeLap;

    void Start()
    {
        carBody = GetComponent<Rigidbody>();

        // If 'racetrack' has not been specified and the scene contains a single Racetrack
        // object, then use it.
        if (this.racetrack == null)
        {
            if (!Racetrack.AllRacetracks.Any())
            {
                Debug.LogError("Scene has no racetracks. RacetrackCarTracker cannot operate.");
            }
            else if (Racetrack.AllRacetracks.Count > 1)
            {
                Debug.LogError("Scene contains multiple racetracks. Please set the Racetrack property of RacetrackCarTracker to the appropriate Racetrack object.");
            }
            else
            {
                this.racetrack = Racetrack.AllRacetracks.Single();
            }
        }

        // Set default for finish line racetrack
        if (this.FinishLineRacetrack == null)
        {
            this.FinishLineRacetrack = this.racetrack;
        }
    }

    void FixedUpdate()
    {
        // Assume off road until proven otherwise
        this.isAboveRoad = false;
        int lapDelta = 0;

        if (this.racetrack == null)
        {
            // Appropriate error should have already been logged in Start()
            return;
        }

        // Search curves/junctions around current curve, using 
        // a flood-fill approach.
        this.visitedSet.Clear();
        this.currentSet.Clear();
        this.currentSet.Add(new CurveOrJunction { curve = this.racetrack.Curves[this.currentCurve] });

        // Perform search iterations
        for (int i = 0; i <= this.CurveSearchAhead; i++)
        {
            // Check if car above any item in current set
            foreach (var item in this.currentSet)
            {
                if (item.curve != null)
                {
                    if (this.IsAboveCurve(item.curve))
                    {
                        racetrack = item.curve.Track;
                        currentCurve = item.curve.Index;
                        lapDelta = item.lapDelta;
                        isAboveRoad = true;
                        break;
                    }
                }
                else
                {
                    if (this.IsAboveJunction(item.junction))
                    {
                        isAboveRoad = true;
                        break;
                    }
                }
            }

            // Stop searching if found above road
            // Skip flood fill logic on last iteration
            if (this.isAboveRoad || i >= this.CurveSearchAhead)
                break;

            // Add set to visited
            this.visitedSet.AddRange(this.currentSet);

            // Add items adjacent to current set
            var adjacentSet = this.currentSet
                .SelectMany(this.GetAdjacentItems)
                .Where(item => !this.visitedSet.Contains(item))
                .ToList();

            // Also add any curves adjacent to junctions in the adjacent set
            var adjacentToJunctions = adjacentSet
                .Where(item => item.junction != null)
                .SelectMany(item => this.GetAdjacentToJunctionItems(item))
                .Where(item => !adjacentSet.Contains(item) && !visitedSet.Contains(item))
                .ToList();            

            // Replace current set with adjacent
            this.currentSet.Clear();
            this.currentSet.AddRange(adjacentSet);
            this.currentSet.AddRange(adjacentToJunctions);
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
                this.PutCarOnRoad();
        }

        // Update lap timer
        CurrentLapTime += Time.fixedDeltaTime;

        // Lap completion
        if (lapDelta > 0)
            this.LapCompleted();
        else if (lapDelta < 0)
            this.BackwardLapCompleted();
    }

    private bool IsAboveCurve(RacetrackCurve curve)
    {
        // Ray cast from center of car towards curve
        var ray = new Ray(
            carBody.transform.TransformPoint(this.RayOffset), 
            -curve.Track.CurveInfos[curve.Index].Normal);

        // Look for first hit and look for RacetrackSurface component
        var hit = Physics.RaycastAll(ray)
            .OrderBy(h => h.distance)
            .Select(h => h.transform.GetComponent<RacetrackSurface>())
            .FirstOrDefault();

        // Compare hit surface (if any) to curve
        return hit != null
            && hit.GetComponentInParent<Racetrack>() == curve.Track
            && hit.ContainsCurveIndex(curve.Index);
    }

    private bool IsAboveJunction(RacetrackJunction junction)
    {
        // Ray cast from center of car towards junction
        var ray = new Ray(
            carBody.transform.TransformPoint(this.RayOffset),
            -junction.transform.up);

        // Look for first hit and look for RacetrackJunction component
        var hit = Physics.RaycastAll(ray)
            .OrderBy(h => h.distance)
            .Select(h => h.transform.GetComponent<RacetrackJunction>())
            .FirstOrDefault();

        // Compare hit junction with junction
        return hit == junction;
    }

    private IEnumerable<CurveOrJunction> GetAdjacentItems(CurveOrJunction item)
    {
        return item.curve != null
            ? this.GetAdjacentToCurveItems(item)
            : this.GetAdjacentToJunctionItems(item);
    }

    private IEnumerable<CurveOrJunction> GetAdjacentToCurveItems(CurveOrJunction item)
    {
        // Search forward along racetrack
        var forward = SearchRacetrack(item, 1);
        if (forward != null)
            yield return forward;


        // Search backwards along racetrack
        if (this.CanGoBackwards)
        {
            var backward = SearchRacetrack(item, -1);
            if (backward != null)
                yield return backward;
        }
    }

    private IEnumerable<CurveOrJunction> GetAdjacentToJunctionItems(CurveOrJunction item)
    {
        // Find racetracks connected to junction.
        // Find corresponding curve of racetrack.
        return from connector in item.junction.GetConnectors()
                    let racetrack = connector.GetConnectedRacetrack()
                    let isStart = racetrack.StartConnector == connector
                    where isStart || this.CanGoBackwards
                    select MakeItem(isStart ? racetrack.Curves.First() : racetrack.Curves.Last(), item, isStart);
    }

    private CurveOrJunction SearchRacetrack(CurveOrJunction item, int dir)
    {
        // Search forward or backward along racetrack
        var track = item.curve.Track;
        var fromIndex = item.curve.Index;
        var curves = track.Curves;
        int i = fromIndex + dir;
        while (true)
        {
            // Prevent infinite loops
            if (i == fromIndex)
                return null;

            // Reached end of track?
            if (i >= curves.Count || i < 0)
            {
                if (track.MeshOverrun == RacetrackMeshOverrunOption.Loop)
                {
                    // Loop around
                    i = dir > 0 ? 0 : curves.Count - 1;
                }
                else
                {
                    // Add junction (if any)
                    var connector = dir > 0 ? track.EndConnector : track.StartConnector;
                    if (connector != null)
                    {
                        var junction = connector.GetComponentInParent<RacetrackJunction>();
                        if (junction != null)
                            return MakeItem(junction, item, dir > 0);
                    }

                    // Stop searching
                    return null;
                }
            }
            else
            {
                // Look for non-jump
                var c = curves[i];
                if (!c.IsJump)
                {
                    // Add curve
                    return MakeItem(c, item, dir > 0);
                }
                else
                {
                    // Search for next curve
                    i += dir;
                }
            }
        }
    }

    /// <summary>
    /// Place the player car back on the road.
    /// Player is positioned above the last curve that they drove on that is flagged as "CanRespawn"
    /// </summary>
    public void PutCarOnRoad()
    {
        // Find racetrack and curve runtime information
        var track = racetrack ?? Racetrack.AllRacetracks.SingleOrDefault();
        if (track == null)
        {
            Debug.LogError("Racetrack instance not found. Cannot place car on track.");
            return;
        }
        if (track.CurveInfos == null)
            RacetrackBuilder.CalculateRuntimeInfo(track);
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

    private CurveOrJunction MakeItem(RacetrackCurve curve, RacetrackJunction junction, CurveOrJunction prevItem, bool isForwards) 
    {
        var lapDelta = prevItem != null ? prevItem.lapDelta : 0;
        if (isForwards && this.IsFinishLineCurve(curve))
            lapDelta += 1;
        if (!isForwards && prevItem != null && this.IsFinishLineCurve(prevItem.curve))
            lapDelta -= 1;
        return new CurveOrJunction
        {
            curve = curve,
            junction = junction,
            lapDelta = lapDelta
        };
    }

    private CurveOrJunction MakeItem(RacetrackCurve curve, CurveOrJunction prevItem, bool isForwards)
    {
        return MakeItem(curve, null, prevItem, isForwards);
    }

    private CurveOrJunction MakeItem(RacetrackJunction junction, CurveOrJunction prevItem, bool isForwards)
    {
        return MakeItem(null, junction, prevItem, isForwards);
    }

    private bool IsFinishLineCurve(RacetrackCurve curve)
    {
        return curve != null && 
            this.FinishLineRacetrack != null &&
            curve.Track == this.FinishLineRacetrack &&
            curve.Index == this.FinishLineCurve;
    }

    private class CurveOrJunction
    {
        public RacetrackCurve curve;
        public RacetrackJunction junction;
        public int lapDelta;

        public override bool Equals(object obj)
        {
            var item = obj as CurveOrJunction;
            return item != null &&
                item.curve == this.curve &&
                item.junction == this.junction &&
                item.lapDelta == this.lapDelta;
        }

        public override int GetHashCode()
        {
            // Use default implementaton using member reference equality.
            return base.GetHashCode();
        }
    }
}
