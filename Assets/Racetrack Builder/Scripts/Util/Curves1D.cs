using UnityEngine;

public interface ICurve1D
{
    float GetPt(float t);
}

public class Bezier1D : ICurve1D
{
    private readonly float[] p;

    public Bezier1D(float p0, float p1, float p2, float p3)
    {
        this.p = new[] { p0, p1, p2, p3 };
    }

    public Bezier1D(float[] p)
    {
        this.p = p;
    }

    /// <summary>
    /// Get point at t, where t [0,1]
    /// </summary>
    public float GetPt(float t)
    {
        float mt = 1.0f - t;
        return mt * mt * mt * p[0] + 3.0f * mt * mt * t * p[1] + 3.0f * mt * t * t * p[2] + t * t * t * p[3];
    }
}

public class Linear1D : ICurve1D
{
    private readonly float p0;
    private readonly float p1;

    public Linear1D(float p0, float p1)
    {
        this.p0 = p0;
        this.p1 = p1;
    }

    public float GetPt(float t)
    {
        return (1.0f - t) * p0 + t * p1;
    }
}

public static class Curve1DUtils {
    /// <summary>
    /// Get curve from z[1] to z[2]
    /// </summary>
    /// <param name="z">
    /// Start and end of curve (z[1] and z[2]) 
    /// plus their preceding and following values (z[0] and z[3] respectively)
    /// </param>
    /// <param name="interpolation">Interpolation type to use</param>
    /// <remarks>z[0] and z[3] are supplied for smooth curve algorithms, e.g. bezier</remarks>
    /// <returns>An ICurve1D object implementing the curve</returns>
    public static ICurve1D GetCurve1D(float[] z, Racetrack1DInterpolationType interpolation)
    {
        switch (interpolation)
        {
            case Racetrack1DInterpolationType.Bezier:
            case Racetrack1DInterpolationType.BezierUnclamped:

                float controlPtDist = interpolation == Racetrack1DInterpolationType.Bezier ? 0.3333f : 0.1f;

                // Create a 1D cubic bezier with control points 1/3rd of the way down.
                float startControlPt = z[1] + (z[2] - z[0]) / 2.0f * 0.3333f;
                float endControlPt = z[2] - (z[3] - z[1]) / 2.0f * 0.3333f;

                // Ensure control points don't fall outside range spanned by points around their corresponding point
                if (interpolation != Racetrack1DInterpolationType.BezierUnclamped)
                {
                    startControlPt = Mathf.Clamp(startControlPt, Mathf.Min(z[0], z[1], z[2]), Mathf.Max(z[0], z[1], z[2]));
                    endControlPt = Mathf.Clamp(endControlPt, Mathf.Min(z[1], z[2], z[3]), Mathf.Max(z[1], z[2], z[3]));
                }

                return new Bezier1D(z[1], startControlPt, endControlPt, z[2]);

            default:
                // Otherwise just use linear interpolation
                return new Linear1D(z[1], z[2]);
        }
    }
}

