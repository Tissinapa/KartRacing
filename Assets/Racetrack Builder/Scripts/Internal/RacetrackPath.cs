using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A list of Racetrack Segments defining the path of the racetrack.
/// Generated from the racetrack curves.
/// </summary>
public class RacetrackPath
{
    public List<RacetrackSegment> Segments { get; private set; }

    public RacetrackPathParams Params { get; private set; }

    public float TotalLength
    {
        get { return Segments.Count * Params.SegmentLength; }
    }

    public RacetrackPath(List<RacetrackCurve> curves, RacetrackPathParams @params)
    {
        this.Params = @params;

        // Generate segments from curves
        this.Segments = this.GenerateSegmentsEnumerable(curves).ToList();

        // Calculate deltas
        for (int i = 0; i < Segments.Count - 1; i++)
        {
            Segments[i].PositionDelta = Segments[i + 1].Position - Segments[i].Position;
            Segments[i].DirectionDelta = Segments[i + 1].Direction - Segments[i].Direction;
            Segments[i].DirectionDelta.x = RacetrackUtil.LocalAngle(Segments[i].DirectionDelta.x);
            Segments[i].DirectionDelta.y = RacetrackUtil.LocalAngle(Segments[i].DirectionDelta.y);
            Segments[i].DirectionDelta.z = RacetrackUtil.LocalAngle(Segments[i].DirectionDelta.z);
            Segments[i].BankPivotXDelta = Segments[i + 1].BankPivotX - Segments[i].BankPivotX;
            Segments[i].WideningDelta = new RacetrackWidening(
                Segments[i + 1].Widening.Left - Segments[i].Widening.Left, 
                Segments[i + 1].Widening.Right - Segments[i].Widening.Right);
        }
    }

    /// <summary>
    /// Get segment by index. Generate "virtual" segments to handle array overrun.
    /// </summary>
    /// <param name="i">Index into segments array</param>
    /// <returns>Corresponding segment.</returns>
    public RacetrackSegment GetSegment(int i)
    {
        if (i < 0) return Segments[0];
        if (i < Segments.Count) return Segments[i];

        // It's likely meshes won't exactly add up to the Z length of the curves, so the last one will overhang.
        // Handle this
        switch (Params.Overrun)
        {
            case RacetrackMeshOverrunOption.Extrapolate:
                // We allow for this by generating a virtual segment extruded from the last segment in the list.
                var lastSeg = Segments.Last();
                return new RacetrackSegment
                {
                    Position = lastSeg.Position + lastSeg.GetSegmentToTrack(0.0f).MultiplyVector(Vector3.forward * lastSeg.Length * (i - (Segments.Count - 1))),
                    PositionDelta = lastSeg.PositionDelta,
                    Direction = lastSeg.Direction,
                    DirectionDelta = Vector3.zero,
                    BankPivotX = lastSeg.BankPivotX,
                    BankPivotXDelta = lastSeg.BankPivotXDelta,
                    Widening = lastSeg.Widening,
                    WideningDelta = RacetrackWidening.zero,
                    Length = lastSeg.Length,
                    Curve = lastSeg.Curve
                };

            case RacetrackMeshOverrunOption.Loop:
                var loopedSeg = Segments[i % (Segments.Count - 1)];

                // Create a copy translated down slightly.
                // If meshes line up exactly it can lead to ugly Z fighting.
                return new RacetrackSegment
                {
                    Position = loopedSeg.Position + new Vector3(0.0f, Params.LoopYOffset, 0.0f),
                    PositionDelta = loopedSeg.PositionDelta,
                    Direction = loopedSeg.Direction,
                    DirectionDelta = loopedSeg.DirectionDelta,
                    BankPivotX = loopedSeg.BankPivotX,
                    BankPivotXDelta = loopedSeg.BankPivotXDelta,
                    Widening = loopedSeg.Widening,
                    WideningDelta = loopedSeg.WideningDelta,
                    Length = loopedSeg.Length,
                    Curve = Segments.Last().Curve
                };


            default:
                throw new ArgumentOutOfRangeException();            // This should never happen
        }
    }

    public RacetrackSegment GetSegmentAtZ(float z)
    {
        return GetSegment(Mathf.FloorToInt(z / Params.SegmentLength));
    }

    public RacetrackSegment GetSegmentAndOffset(float z, out float zOffset)
    {
        int index = Mathf.FloorToInt(z / Params.SegmentLength);
        zOffset = z - index * Params.SegmentLength;
        return GetSegment(index);
    }

    public void GetSegmentTransform(RacetrackSegment seg, Transform dstTransform)
    {
        // Set transform to position object at start of segment, with 
        // Y axis rotation set (but not X and Z).

        // Segment gives position and rotation in track space.
        var segmentToTrack = seg.GetSegmentToTrack(0.0f);
        var segPos = segmentToTrack.MultiplyPoint(Vector3.zero);
        var segForward = segmentToTrack.MultiplyVector(Vector3.forward);
        segForward.y = 0.0f;

        // Convert to world space
        var worldPos = Params.Transform.MultiplyPoint(segPos);
        var worldForward = Params.Transform.MultiplyVector(segForward);

        // Set transform
        dstTransform.position = worldPos;

        // Handle directly up/down case (e.g. in loops)
        if (Mathf.Abs(worldForward.x) < 0.0001f && Mathf.Abs(worldForward.z) < 0.0001f)
        {
            var segRight = segmentToTrack.MultiplyVector(Vector3.right);
            var worldRight = Params.Transform.MultiplyVector(segRight);
            worldForward = Vector3.Cross(worldRight, Vector3.up);
        }

        dstTransform.rotation = Quaternion.LookRotation(worldForward);
    }

    /// <summary>
    /// Generate segments from track curves
    /// </summary>
    /// <returns>Enumerable of segments for all curves</returns>
    private IEnumerable<RacetrackSegment> GenerateSegmentsEnumerable(List<RacetrackCurve> curves)
    {
        // Walk along curve in track space.
        Vector3 pos = this.Params.StartCurvePosition;
        Vector3 dir = this.Params.StartCurveAngles;
        Vector3 segPosDelta = Vector3.zero;
        float bankPivotX = this.Params.StartBankPivotX;
        RacetrackWidening widening = this.Params.StartCurveWidening;
        Vector3 dirDelta = Vector3.zero;
        Vector3 posDelta = Vector3.forward * this.Params.SegmentLength;
        float bankPivotXDelta = 0.0f;
//        RacetrackWidening extensionDelta = RacetrackWidening.zero;
        for (int i = 0; i < curves.Count; i++)
        {
            var curve = curves[i];

            // Calculate direction at the end of the curve
            Vector3 endDir = new Vector3(curve.Angles.x, dir.y + curve.Angles.y, curve.Angles.z);

            // Create curve for interpolating Z angle
            var bankAngleInterpolation = curve.BankAngleInterpolation;
            if (bankAngleInterpolation == Racetrack1DInterpolationType.Inherit)
                bankAngleInterpolation = this.Params.BankAngleInterpolation;

            var zCurve = Curve1DUtils.GetCurve1D(
                new float[4] {
                    i - 2 >= 0 ? curves[i - 2].Angles.z : this.Params.StartCurveAngles.z,
                    i - 1 >= 0 ? curves[i - 1].Angles.z : this.Params.StartCurveAngles.z,
                    curve.Angles.z,
                    i + 1 < curves.Count ? curves[i + 1].Angles.z : this.Params.StartCurveAngles.z
                },
                bankAngleInterpolation);

            // Create curves for interpolating widening
            var wideningInterpolation = curve.WideningInterpolation;
            if (wideningInterpolation == Racetrack1DInterpolationType.Inherit)
                wideningInterpolation = this.Params.WideningInterpolation;

            var leftWideningCurve = Curve1DUtils.GetCurve1D(
                new float[4]
                {
                    i - 2 >= 0 ? curves[i - 2].Widening.Left : this.Params.StartCurveWidening.Left,
                    i - 1 >= 0 ? curves[i - 1].Widening.Left : this.Params.StartCurveWidening.Left,
                    curve.Widening.Left,
                    i + 1 < curves.Count ? curves[i + 1].Widening.Left : this.Params.StartCurveWidening.Left
                },
                wideningInterpolation);

            var rightWideningCurve = Curve1DUtils.GetCurve1D(
                new float[4]
                {
                    i - 2 >= 0 ? curves[i - 2].Widening.Right : this.Params.StartCurveWidening.Right,
                    i - 1 >= 0 ? curves[i - 1].Widening.Right : this.Params.StartCurveWidening.Right,
                    curve.Widening.Right,
                    i + 1 < curves.Count ? curves[i + 1].Widening.Right : this.Params.StartCurveWidening.Right
                },
                wideningInterpolation);

            switch (curve.Type)
            {
                case RacetrackCurveType.Arc:
                    {
                        // Find delta to add to curve each segment
                        dirDelta = new Vector3(
                            RacetrackUtil.LocalAngle(curve.Angles.x - dir.x),
                            curve.Angles.y,
                            0
                        ) / curve.Length * this.Params.SegmentLength;
                        bankPivotXDelta = (curve.BankPivotX - bankPivotX) / curve.Length * this.Params.SegmentLength;
//                        extensionDelta = (curve.Widening - widening) / curve.Length * this.Params.SegmentLength;

                        // Generate segments
                        for (float d = 0.0f; d < curve.Length; d += this.Params.SegmentLength)
                        {
                            segPosDelta = Matrix4x4.Rotate(Quaternion.Euler(dir)).MultiplyVector(posDelta);
                            dir.z = zCurve.GetPt(d / curve.Length);

                            var segment = new RacetrackSegment
                            {
                                Position = pos,
                                PositionDelta = segPosDelta,
                                Direction = dir,
                                DirectionDelta = dirDelta,
                                BankPivotX = bankPivotX,
                                BankPivotXDelta = bankPivotXDelta,
                                Widening = new RacetrackWidening(leftWideningCurve.GetPt(d / curve.Length), rightWideningCurve.GetPt(d / curve.Length)),
//                                WideningDelta = extensionDelta,
                                Length = this.Params.SegmentLength,
                                Curve = curve
                            };
                            yield return segment;

                            // Advance to start of next segment
                            pos += segPosDelta;
                            dir += dirDelta;
                            bankPivotX += bankPivotXDelta;
//                            widening += extensionDelta;
                        }
                    }

                    //hostServices.ObjectChanging(curve);
                    curve.EndPosition = pos;
                    dir.z = curve.Angles.z;
                    bankPivotX = curve.BankPivotX;
                    widening = curve.Widening;

                    break;

                case RacetrackCurveType.Bezier:
                    {
                        // Calculate end position
                        Vector3 endPos = curve.EndPosition;

                        // Calculate start and end tangental vectors
                        Vector3 startTangent = Matrix4x4.Rotate(Quaternion.Euler(dir)).MultiplyVector(Vector3.forward);
                        Vector3 endTangent = Matrix4x4.Rotate(Quaternion.Euler(endDir)).MultiplyVector(Vector3.forward);
                        float separationDist = (endPos - pos).magnitude;
                        float startTangentLength = separationDist * curve.StartControlPtDist;
                        float endTangentLength = separationDist * curve.EndControlPtDist;

                        // Create bezier curve from control points
                        Bezier bezier = new Bezier(
                            pos,
                            pos + startTangent * startTangentLength,
                            endPos - endTangent * endTangentLength,
                            endPos);

                        // Build distance lookup with t values for each segment length
                        var lookup = bezier.BuildDistanceLookup(0.0001f, this.Params.SegmentLength);
                        //hostServices.ObjectChanging(curve);
                        curve.Length = lookup.Count * this.Params.SegmentLength;
                        bankPivotXDelta = (curve.BankPivotX - bankPivotX) / curve.Length * this.Params.SegmentLength;
                        //extensionDelta = (curve.Widening - widening) / curve.Length * this.Params.SegmentLength;

                        // Generate curve segments along bezier
                        foreach (var t in lookup)
                        {
                            // Get position and tangent
                            pos = bezier.GetPt(t);
                            Vector3 tangent = bezier.GetTangent(t);
                            segPosDelta = tangent * this.Params.SegmentLength;

                            // Calculate corresponding euler angles
                            dir.y = Mathf.Atan2(segPosDelta.x, segPosDelta.z) * Mathf.Rad2Deg;
                            float xz = Mathf.Sqrt(segPosDelta.x * segPosDelta.x + segPosDelta.z * segPosDelta.z);
                            dir.x = -Mathf.Atan2(segPosDelta.y, xz) * Mathf.Rad2Deg;
                            dir.z = zCurve.GetPt(t);

                            var segment = new RacetrackSegment
                            {
                                Position = pos,
                                PositionDelta = segPosDelta,
                                Direction = dir,
                                DirectionDelta = Vector3.zero,  // Note: This is calculated by the calling code in a separate pass
                                BankPivotX = bankPivotX,
                                BankPivotXDelta = bankPivotXDelta,
                                Widening = new RacetrackWidening(leftWideningCurve.GetPt(t), rightWideningCurve.GetPt(t)),
                                //WideningDelta = extensionDelta,
                                Length = this.Params.SegmentLength,
                                Curve = curve
                            };
                            yield return segment;

                            // Advance to start of next segment
                            bankPivotX += bankPivotXDelta;
                            //widening += extensionDelta;
                        }

                        // Update dir to end-of-curve direction.
                        // Otherwise it will be the direction based on the last sampled bezier tangent,
                        // which may not exactly align.
                        dir = endDir;
                        pos = bezier.GetPt(1.0f);
                        bankPivotX = curve.BankPivotX;
                        widening = curve.Widening;
                    }
                    break;
            }
        }

        // Return final segment
        yield return new RacetrackSegment
        {
            Position = pos,
            PositionDelta = segPosDelta,
            Direction = dir,
            DirectionDelta = dirDelta,
            BankPivotX = bankPivotX,
            BankPivotXDelta = bankPivotXDelta,
            Widening = widening,
            //WideningDelta = extensionDelta,
            Length = this.Params.SegmentLength,
            Curve = curves.LastOrDefault()
        };
    }

}

/// <summary>
/// Parameters to generate a RacetrackPath (in addition to the RacetrackCurve objects)
/// </summary>
public struct RacetrackPathParams
{
    public Matrix4x4 Transform;
    public float SegmentLength;
    public Racetrack1DInterpolationType BankAngleInterpolation;
    public Racetrack1DInterpolationType WideningInterpolation;
    public Vector3 StartCurveAngles;
    public Vector3 StartCurvePosition;
    public float StartBankPivotX;
    public RacetrackWidening StartCurveWidening;
    public RacetrackMeshOverrunOption Overrun;
    public float LoopYOffset;
}

/// <summary>
/// A section of a racetrack path
/// </summary>
public class RacetrackPathSection {
    public RacetrackPath Path;
    public float StartZ;
    public float EndZ;

    public void CalcHash(IHasher hash)
    {
        float zInSegs = StartZ / Path.Params.SegmentLength;
        int segi = Mathf.FloorToInt(zInSegs);
        int count = Mathf.FloorToInt(EndZ / Path.Params.SegmentLength) - segi;
        hash.RoundedFloat(EndZ - StartZ).Int(count);

        if (hash.HashMethod != HashMethod.MD5)
        {
            // Include distance from segment boundary, as this affects the shape of
            // the mesh, and where it is placed in relation to the parent object.
            hash.RoundedFloat(zInSegs - Mathf.Floor(zInSegs));
        }

        for (int i = 0; i < count; i++)
            Path.GetSegment(segi + i).CalcHash(hash);
    }

    public void GetSegmentRange(out int start, out int end)
    {
        start = Mathf.FloorToInt(StartZ / Path.Params.SegmentLength);
        end   = Mathf.FloorToInt(EndZ / Path.Params.SegmentLength);
    }

    public IEnumerable<RacetrackSegment> GetSegments()
    {
        int start;
        int end;
        this.GetSegmentRange(out start, out end);
        for (int i = start; i < end; i++)
            yield return this.Path.GetSegment(i);
    }
}