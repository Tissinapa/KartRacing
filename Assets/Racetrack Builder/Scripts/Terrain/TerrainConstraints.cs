using UnityEngine;

[DisallowMultipleComponent]
public class TerrainConstraints : MonoBehaviour
{
    [Tooltip("Whether to raise the terrain up to the level of the object, if necessary. This affects the Racetrack Terrain Modifier component. Terrain Mask Rect component(s) must be present to describe how the object interacts with the terrain.")]
    public bool RaiseTerrain = false;

    [Tooltip("Whether to lower the terrain underneath the object, if necessary. This affects the Racetrack Terrain Modifier component. Terrain Mask Rect component(s) must be present to describe how the object interacts with the terrain.")]
    public bool LowerTerrain = true;
}
