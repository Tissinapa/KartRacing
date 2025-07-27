using System.Linq;
using UnityEngine;

/// <summary>
/// Placed on racetrack "junction objects", which are Unity objects with one or more RacetrackConnector
/// connection points. 
/// This component is mainly for convenience in the editor.
/// </summary>
public class RacetrackJunction : MonoBehaviour
{
    public RacetrackConnector[] GetConnectors()
    {
        return GetComponentsInChildren<RacetrackConnector>();
    }

    /// <summary>
    /// Position junction so that connector aligns with start/end of racetrack
    /// </summary>
    /// <param name="connector">Connector to align (should be a child of this object)</param>
    /// <param name="racetrack">Racetrack to align to</param>
    /// <param name="start">Whether to align to the start (true) or end (false) of the racetrack</param>
    public void AlignConnectorToRacetrack(RacetrackConnector connector, Racetrack racetrack, bool start)
    {
        // Find racetrack path and junction
        var path = racetrack.Path;
        if (!path.Segments.Any())
            return;

        var settings = racetrack.GetEditorSettings();

        // Find segment
        float z;
        RacetrackSegment segment;
        if (start)
        {
            segment = path.Segments.First();
            z = 0.0f;
        }
        else
        {
            segment = path.Segments.Last();
            z = settings.SegmentLength;
        }

        // Find segment to world transformation
        Matrix4x4 racetrackFromSegment = segment.GetSegmentToTrack(z);
        if (start)
            racetrackFromSegment *= Matrix4x4.Rotate(Quaternion.Euler(0, 180.0f, 0));
        Matrix4x4 worldFromRacetrack = racetrack.transform.localToWorldMatrix;
        Matrix4x4 worldFromSegment = worldFromRacetrack * racetrackFromSegment;

        // Need connector space from junction space transform
        Matrix4x4 worldFromJunction = this.transform.localToWorldMatrix;
        Matrix4x4 worldFromConnector = connector.transform.localToWorldMatrix;
        Matrix4x4 connectorFromWorld = worldFromConnector.inverse;
        Matrix4x4 connectorFromJunction = connectorFromWorld * worldFromJunction;

        // Want to position connector at segment position.
        // I.e. worldFromConnector = worldFromSegment.
        // Except that we must preserve the original scale.
        Vector3 connectorScale = worldFromConnector.lossyScale;
        Vector3 segmentScale = worldFromSegment.lossyScale;
        Vector3 scaleAdj = new Vector3(connectorScale.x / segmentScale.x, connectorScale.y / segmentScale.y, connectorScale.z / segmentScale.z);
        Matrix4x4 newWorldFromConnector = worldFromSegment * Matrix4x4.Scale(scaleAdj);        

        // Calculate the required junction transform
        Matrix4x4 newWorldFromJunction = newWorldFromConnector * connectorFromJunction;

        // Position junction
        this.transform.position = newWorldFromJunction.GetColumn(3);
        this.transform.rotation = newWorldFromJunction.rotation;
    }
}
