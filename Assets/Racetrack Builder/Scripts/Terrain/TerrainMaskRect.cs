using System.Collections.Generic;
using UnityEngine;

// A rectangular mask for defining how a Terrain interacts with an object.
public class TerrainMaskRect : MonoBehaviour, ITerrainMask
{
    [Min(0.0f)]
    public float Length = 10;

    [Min(0.0f)]
    public float Width = 5;

    public IEnumerable<Vector3> GetPoints(float granularity)
    {
        if (granularity > 0.0f)
        {
            for (float z = 0; z <= Length; z += granularity)
            {
                for (float x = -Width / 2; x <= Width / 2; x += granularity)
                {
                    yield return new Vector3(x, 0.0f, z);
                }
            }
        }
    }
}
