using System.Linq;
using UnityEngine;

/// <summary>
/// Connects the start or end of a racetrack to a point on an object.
/// Typically used to create junctions.
/// Can also be added to racetracks to connect them together.
/// Note: The connector component should be placed on an object centered at the connection point.
/// The local Z axis (blue arrow) should point inwards.
/// </summary>
public class RacetrackConnector : MonoBehaviour
{
    [Tooltip("The default racetrack mesh template to use when a new racetrack is created")]
    public RacetrackMeshTemplate MeshTemplate;

    public Racetrack GetConnectedRacetrack()
    {
        return Racetrack.AllRacetracks.FirstOrDefault(r => r.StartConnector == this || r.EndConnector == this);
    }
}
