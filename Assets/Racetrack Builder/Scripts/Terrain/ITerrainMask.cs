using System.Collections.Generic;
using UnityEngine;

public interface ITerrainMask 
{
    // Get points (relative to game object) to apply heightmap constraints
    IEnumerable<Vector3> GetPoints(float granularity);

    // Get the game object.
    GameObject gameObject { get; }
}
