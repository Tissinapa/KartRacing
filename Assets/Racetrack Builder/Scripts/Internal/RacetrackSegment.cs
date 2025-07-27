using System;
using UnityEngine;

public class RacetrackSegment
{
    public Vector3 Position;
    public Vector3 PositionDelta;               // Added to position to get next segment's position
    public Vector3 Direction;                   // Direction as Euler angles
    public Vector3 DirectionDelta;              // Added to Direction to get next segment's direction (and used to lerp between them)
    public float BankPivotX;
    public float BankPivotXDelta;
    public RacetrackWidening Widening;          // Left/right side widening amount
    public RacetrackWidening WideningDelta;     // Added to Widening to get next segment's widening (and used to lerp between them)
    public float Length;                        // Copy of Racetrack.SegmentLength for convenience
    public RacetrackCurve Curve;                // Curve to which segment belongs

    /// <summary>
    /// Get matrix converting from segment space to racetrack space
    /// </summary>
    /// <param name="segZ">Z distance along segment. [0, Length]</param>
    /// <returns>A transformation matrix</returns>
    public Matrix4x4 GetSegmentToTrack(float segZ = 0.0f)
    {
        float f = segZ / Length;                                                            // Fractional distance along segment
        Vector3 adjDir = Direction + DirectionDelta * f;                                    // Adjust rotation based on distance down segment
        Vector3 adjPosition = Position + PositionDelta * f;                                 // Adjust origin based on distance down segment
        float bankPivotX = BankPivotX + BankPivotXDelta * f;

        // Basic logic is to:
        //  * Translate (bankPivotX,0,segz) into the origin, so that it becomes the center of rotation
        //  * Rotate along Z axis by interpolated bank amount
        //  * Translate (0,0,segz) into the origin, so that it becomes the center of rotation
        //  * Rotate by interpolated X axis and Y axis direction
        //  * Translate to interpolated position
        // (Matrices are multipied in reverse order)
        return Matrix4x4.Translate(adjPosition)
            * Matrix4x4.Rotate(Quaternion.Euler(adjDir.x, adjDir.y, 0.0f))
            * Matrix4x4.Translate(new Vector3(bankPivotX, 0.0f, 0.0f))
            * Matrix4x4.Rotate(Quaternion.Euler(0.0f, 0.0f, adjDir.z))
            * Matrix4x4.Translate(new Vector3(-bankPivotX, 0.0f, -segZ));
    }

    const float maxShearAngle = 89.0f;

    private float ClampShearAngle(float angle)
    {
        if (angle < -maxShearAngle)
            return -maxShearAngle;
        if (angle > maxShearAngle)
            return maxShearAngle;
        return angle;
    }

    public Matrix4x4 GetShearSegmentToTrack(float segZ = 0.0f)
    {
        float f = segZ / Length;                                                            // Fractional distance along segment
        Vector3 adjDir = Direction + DirectionDelta * f;                                    // Adjust rotation based on distance down segment
        Vector3 adjPosition = Position + PositionDelta * f;                                 // Adjust origin based on distance down segment
        float bankPivotX = BankPivotX + BankPivotXDelta * f;

        // Clamp X and Z angles

        // Create shear matrices
        adjDir.z = ClampShearAngle(adjDir.z);
        float tanZ = (float)Math.Tan(adjDir.z * Math.PI / 180.0f);
        var zShear = new Matrix4x4(
            new Vector4(1, tanZ, 0, 0),
            new Vector4(0,    1, 0, 0),
            new Vector4(0,    0, 1, 0),
            new Vector4(0,    0, 0, 1));
        adjDir.x = ClampShearAngle(adjDir.x);
        float tanX = (float)Math.Tan(adjDir.x * Math.PI / 180.0f);
        var xShear = new Matrix4x4(
            new Vector4(1,    0, 0, 0),
            new Vector4(0,    1, 0, 0),
            new Vector4(0, tanX, 1, 0),
            new Vector4(0,    0, 0, 1));

        // Basic logic is to:
        //  * Translate (bankPivotX,0,segz) into the origin, so that it becomes the center of rotation
        //  * Shear around Z axis by interpolated bank amount
        //  * Translate (0,0,segz) into the origin, so that it becomes the center of rotation
        //  * Rotate by interpolated X axis and Y axis direction
        //  * Translate to interpolated position
        // (Matrices are multipied in reverse order)
        return Matrix4x4.Translate(adjPosition)
            * Matrix4x4.Rotate(Quaternion.Euler(0, adjDir.y, 0.0f))
            * xShear
            * Matrix4x4.Translate(new Vector3(bankPivotX, 0.0f, 0.0f))
            * zShear
            * Matrix4x4.Translate(new Vector3(-bankPivotX, 0.0f, -segZ));
    }

    public RacetrackWidening GetWidening(float segZ = 0.0f)
    {
        float f = segZ / Length;                                                            // Fractional distance along segment
        return Widening + WideningDelta * f;
    }

    public void CalcHash(IHasher hash)
    {
        hash.RoundedFloat(Length)
            .RoundedFloat(RacetrackUtil.LocalAngle(Direction.x))
            .RoundedFloat(RacetrackUtil.LocalAngle(Direction.z))
            .RoundedFloat(BankPivotX)
            .RoundedFloat(BankPivotXDelta)
            .Vector3(DirectionDelta);
        Widening.CalcHash(hash);
        WideningDelta.CalcHash(hash);
    }
}
