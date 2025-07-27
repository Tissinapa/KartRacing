using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Misc helper functions
/// </summary>
public static class RacetrackUtil {

    /// <summary>
    /// Returns the corresponding angle in the -180 to 180 degree range.
    /// </summary>
    /// <param name="angle">Angle in degrees</param>
    public static float LocalAngle(float angle)
    {
        angle -= Mathf.Floor(angle / 360.0f) * 360.0f;
        if (angle > 180.0f)
            angle -= 360.0f;
        return angle;
    }

    public static Vector3 ToVector3(Vector4 v)
    {
        return new Vector3(v.x, v.y, v.z);
    }

    public static Vector4 ToVector4(Vector3 v, float w = 0.0f)
    {
        return new Vector4(v.x, v.y, v.z, w);
    }

    public static T FindEffectiveComponent<T>(Component searchFrom, Component searchTo) where T : Component
    {
        // Search up parent chain from "searchFrom"
        for (Transform t = searchFrom.transform; t != null; t = t.parent)
        {
            // Found component?
            var component = t.GetComponent<T>();
            if (component != null)
                return component;

            // Reached searchTo ancestor?
            if (t.gameObject == searchTo.gameObject)
                return null;
        }

        // Reaching here implies searchTo is not an ancestor of searchFrom.
        return null;
    }

    public static float SnapToNearest(float value, float snap)
    {
        return Mathf.Round(value / snap) * snap;
    }

    public static Matrix4x4 GetAncestorFromDescendentMatrix(Component ancestor, Component descendent)
    {
        return ancestor.transform.localToWorldMatrix.inverse * descendent.transform.localToWorldMatrix;
    }

    public static int FindIndex<T>(IEnumerable<T> items, T item) where T: class
    {
        int index = 0;
        foreach (var i in items)
        {
            if (i == item)
                return index;
            index++;
        }

        // Item not found
        return -1;
    }

    public static int FindIndex<T>(IEnumerable<T> items, Func<T, bool> predicate)
    {
        int index = 0;
        foreach (var i in items)
        {
            if (predicate(i))
                return index;
            index++;
        }

        // Item not found
        return -1;
    }

    public static bool AreSame(float a, float b, float tolerance = 0.0001f)
    {
        return Mathf.Abs(a - b) <= tolerance;
    }

    public static bool AreSame(Vector3 a, Vector3 b, float tolerance = 0.0001f)
    {
        return AreSame(a.x, b.x, tolerance)
            && AreSame(a.y, b.y, tolerance)
            && AreSame(a.z, b.z, tolerance);
    }

    public static float RoundToNearest(float value, float granularity)
    {
        return Mathf.Round(value / granularity) * granularity;
    }

    public static float RoundedFloat(float value)
    {
        return RoundToNearest(value, 1.0f / 8192.0f);
    }

    public static Matrix4x4 AlignYAxisToWorldY(Matrix4x4 matrix)
    {
        // Disassemble matrix
        Vector3 basisX = RacetrackUtil.ToVector3(matrix.GetColumn(0));
        Vector3 basisY = RacetrackUtil.ToVector3(matrix.GetColumn(1));
        Vector3 basisZ = RacetrackUtil.ToVector3(matrix.GetColumn(2));

        // Align Y vector with global Y axis
        basisY = Vector3.up * basisY.magnitude;

        // Cross product to get X and Z (assuming matrix is orthogonal)
        if (Vector3.Cross(basisY, basisZ).magnitude > 0.001f)
        {
            basisX = Vector3.Cross(basisY, basisZ).normalized * basisX.magnitude;
            basisZ = Vector3.Cross(basisX, basisY).normalized * basisZ.magnitude;
        }
        else
        {
            // Can happen if basisZ was aligned to Y axis.
            // Cross product with basisX first instead.
            basisZ = Vector3.Cross(basisX, basisY).normalized * basisZ.magnitude;
            basisX = Vector3.Cross(basisY, basisZ).normalized * basisX.magnitude;
        }

        // Recompose matrix
        matrix.SetColumn(0, RacetrackUtil.ToVector4(basisX));
        matrix.SetColumn(1, RacetrackUtil.ToVector4(basisY));
        matrix.SetColumn(2, RacetrackUtil.ToVector4(basisZ));

        return matrix;
    }

    public static Matrix4x4 ClearRotation(Matrix4x4 matrix)
    {
        // Get scale factors from basis vectors
        float xScale = matrix.GetColumn(0).magnitude;
        float yScale = matrix.GetColumn(1).magnitude;
        float zScale = matrix.GetColumn(2).magnitude;

        // Recompose matrix
        matrix.SetColumn(0, new Vector4(xScale, 0, 0));
        matrix.SetColumn(1, new Vector4(0, yScale, 0));
        matrix.SetColumn(2, new Vector4(0, 0, zScale));

        return matrix;
    }

    public static bool AreEqual(Quaternion a, Quaternion b)
    {
        return 1.0f - Quaternion.Dot(a, b) < 0.0001f;
    }

    public static void PositionObjectOnRacetrack(GameObject obj, Racetrack racetrack, Vector3 position, Quaternion rotation)
    {
        // Convert z to segment and offset
        float distance = Mathf.Clamp(position.z, 0.0f, racetrack.Path.TotalLength);
        float segmentZOffset;
        RacetrackSegment segment = racetrack.Path.GetSegmentAndOffset(distance, out segmentZOffset);

        // Get transformation for segment
        Matrix4x4 trackFromSegment = segment.GetSegmentToTrack(segmentZOffset);
        Matrix4x4 worldFromTrack = racetrack.transform.localToWorldMatrix;
        Matrix4x4 worldFromSegment = worldFromTrack * trackFromSegment;

        // Segment space position and orientation
        Vector3 segPos = new Vector3(position.x, position.y, segmentZOffset);
        Vector3 segForward = rotation * Vector3.forward;
        Vector3 segUp = rotation * Vector3.up;

        // Convert to world space
        Vector3 worldPos = worldFromSegment.MultiplyPoint(segPos);
        Vector3 worldForward = worldFromSegment.MultiplyVector(segForward);
        Vector3 worldUp = worldFromSegment.MultiplyVector(segUp);

        // Position object in world space
        obj.transform.position = worldPos;
        obj.transform.rotation = Quaternion.LookRotation(worldForward, worldUp);
    }
}
