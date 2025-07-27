using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class RacetrackRelative : MonoBehaviour
{
    [Tooltip("Position relative to racetrack. Z is the distance along the track. X and Y are relative to the corresponding cross-section.")]
    public Vector3 position = Vector3.zero;

    [Tooltip("Rotation relative to the track.")]
    public Quaternion rotation = Quaternion.identity;

    // Position object on racetrack at racetrack relative "position" and "rotation"
    public void PositionOnRacetrack()
    {
        var curve = GetComponentInParent<RacetrackCurve>();
        if (curve != null)
        {
            // Object is under curve in scene hierarchy

            // Clamp Z
            position.z = Mathf.Clamp(position.z, 0.0f, curve.Length);

            // Position relative to curve
            // Get curve Z distance
            var curves = curve.Track?.Curves;
            if (curves == null || curves.Count <= curve.Index) return;
            float curveZOffset = curves.Take(curve.Index).Sum(c => c.Length);

            // Position at corresponding point on track
            var racetrackPos = position + new Vector3(0.0f, 0.0f, curveZOffset);
            RacetrackUtil.PositionObjectOnRacetrack(gameObject, curve.Track, racetrackPos, rotation);
        }
        else
        {
            var racetrack = GetComponentInParent<Racetrack>();
            if (racetrack != null)
            {
                // Object is under racetrack in scene hierarchy

                // Clamp Z
                position.z = Mathf.Clamp(position.z, 0.0f, racetrack.Path.TotalLength);

                // Position relative to racetrack
                RacetrackUtil.PositionObjectOnRacetrack(gameObject, racetrack, position, rotation);
            }
        }
    }
}
