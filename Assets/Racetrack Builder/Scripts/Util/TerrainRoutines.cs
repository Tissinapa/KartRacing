using UnityEngine;
using System.Linq;

public static class TerrainRoutines
{
    private static Matrix4x4 GetHeightmapFromObjectTransform(GameObject gameObject, Terrain terrain, float elevation)
    {
        var terrainData = terrain.terrainData;

        // Transform from racetrack space into heightmap space
        Matrix4x4 worldFromObj = gameObject.transform.localToWorldMatrix;
        Vector3 depthAdj = worldFromObj.MultiplyVector(Vector3.up).normalized * -elevation;
        Matrix4x4 depthAdjTransform = Matrix4x4.Translate(depthAdj);
        Matrix4x4 worldFromTerrain = terrain.transform.localToWorldMatrix;
        Matrix4x4 terrainFromWorld = worldFromTerrain.inverse;

        Matrix4x4 terrainFromHeightmap = Matrix4x4.Scale(terrainData.size);
        Matrix4x4 heightmapFromTerrain = terrainFromHeightmap.inverse;

        Matrix4x4 heightmapFromObj = heightmapFromTerrain * terrainFromWorld * depthAdjTransform * worldFromObj;

        return heightmapFromObj;
    }

    public static void ApplyRacetrackConstraints(
        Racetrack racetrack, Terrain terrain, float[,] minHeights, float [,] maxHeights, 
        float granularity, float racetrackWidth, float elevation)
    {
        var terrainData = terrain.terrainData;
        var res = terrainData.heightmapResolution;
        Matrix4x4 heightmapFromTrack = GetHeightmapFromObjectTransform(racetrack.gameObject, terrain, elevation);

        foreach (var segment in racetrack.Path.Segments)
        {
            bool raise = true;
            bool lower = true;
            if (segment.Curve != null)
            {
                raise = segment.Curve.RaiseTerrain;
                lower = segment.Curve.LowerTerrain;
            }

            if (!raise && !lower) continue;

            // Sample points along segment
            for (float z = 0; z < racetrack.SegmentLength; z += granularity)
            {
                var widening = segment.GetWidening(z);
                Matrix4x4 trackFromSeg = segment.GetSegmentToTrack(z);
                Matrix4x4 heightmapFromSeg = heightmapFromTrack * trackFromSeg;

                float left = -racetrackWidth / 2 - widening.Left;
                float right = racetrackWidth / 2 + widening.Right;
                for (float x = left; x < right; x += granularity)
                {
                    // Point in segment space
                    var segPt = new Vector3(x, 0, z);

                    // Point in heightmap space
                    var heightPt = heightmapFromSeg.MultiplyPoint(segPt);

                    // Heightmap coordinates
                    int hx = Mathf.RoundToInt(heightPt.x * (res - 1)), hy = Mathf.RoundToInt(heightPt.z * (res - 1));

                    // Update min/max constraints
                    if (hx >= 0 && hx < res && hy >= 0 && hy < res)
                    {
                        if (lower)
                            maxHeights[hy, hx] = Mathf.Min(maxHeights[hy, hx], heightPt.y);
                        if (raise)
                            minHeights[hy, hx] = Mathf.Max(minHeights[hy, hx], heightPt.y);
                    }
                }
            }
        }
    }

    public static void ApplyTerrainConstraints(
        TerrainConstraints constraint, Terrain terrain, float[,] minHeights, float[,] maxHeights,
        float granularity, float elevation)
    {
        if (!constraint.RaiseTerrain && !constraint.LowerTerrain)
            return;

        var masks = constraint.GetComponentsInChildren<TerrainMaskRect>();
        foreach (var mask in masks)
            ApplyTerrainMask(mask, terrain, minHeights, maxHeights, granularity, elevation, constraint.RaiseTerrain, constraint.LowerTerrain);
    }

    public static void ApplyTerrainMask(ITerrainMask mask, Terrain terrain, float[,] minHeights, float[,] maxHeights,
        float granularity, float elevation, bool raise, bool lower) 
    {
        var terrainData = terrain.terrainData;
        var res = terrainData.heightmapResolution;
        Matrix4x4 heightmapFromMask = GetHeightmapFromObjectTransform(mask.gameObject, terrain, elevation);

        foreach (var maskPt in mask.GetPoints(granularity)) { 

            // Point in heightmap space
            var heightPt = heightmapFromMask.MultiplyPoint(maskPt);

            // Heightmap coordinates
            int hx = Mathf.RoundToInt(heightPt.x * (res - 1)), hy = Mathf.RoundToInt(heightPt.z * (res - 1));

            // Lower heightmap to point
            if (hx >= 0 && hx < res && hy >= 0 && hy < res)
            {
                if (lower)
                    maxHeights[hy, hx] = Mathf.Min(maxHeights[hy, hx], heightPt.y);
                if (raise)
                    minHeights[hy, hx] = Mathf.Max(minHeights[hy, hx], heightPt.y);
            }
        }
    }

    public static void GetTerrainModifications(GameObject rootObject, Terrain terrain,
        float granularity, float racetrackWidth, float elevation, int smooth, out float[,] heights, out float[,] updatedHeights)
    {
        var terrainData = terrain.terrainData;
        var res = terrainData.heightmapResolution;

        // Create min/max constraints
        var minHeights = new float[res, res];
        var maxHeights = new float[res, res];
        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                minHeights[y, x] = 0.0f;
                maxHeights[y, x] = 1.0f;
            }
        }

        // Apply racetracks
        var racetracks = rootObject != null
            ? rootObject.GetComponentsInChildren<Racetrack>()
            : GameObject.FindObjectsOfType<Racetrack>();
        foreach (var racetrack in racetracks)
            ApplyRacetrackConstraints(racetrack, terrain, minHeights, maxHeights, granularity, racetrackWidth, elevation);

        // Apply terrain constraints
        var constraints = rootObject != null
            ? rootObject.GetComponentsInChildren<TerrainConstraints>()
            : GameObject.FindObjectsOfType<TerrainConstraints>();
        foreach (var constraint in constraints)
            ApplyTerrainConstraints(constraint, terrain, minHeights, maxHeights, granularity, elevation);

        // Fix min/max conflicts. Essentially maximum height takes precedence.
        for (int x = 0; x < res; x++)
            for (int y = 0; y < res; y++)
                if (maxHeights[y, x] < minHeights[y, x])
                    minHeights[y, x] = maxHeights[y, x];

        // Compare actual heights to min/max constraints. Calculate adjustments.
        heights = terrainData.GetHeights(0, 0, res, res);
        var adj = new float[res, res];
        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                float h = heights[y, x];
                float min = minHeights[y, x];
                float max = maxHeights[y, x];
                adj[y, x] = Mathf.Clamp(h, min, max) - h;
            }
        }

        // Smooth adjustments
        for (int i = 0; i < smooth; i++)
            adj = SmoothHeightAdjustments(heights, adj, minHeights, maxHeights, res);

        // Calculate updated heightmap
        updatedHeights = new float[res, res];
        for (int x = 0; x < res; x++)
            for (int y = 0; y < res; y++)
                updatedHeights[y, x] = Mathf.Clamp01(heights[y, x] + adj[y, x]);
    }

    static readonly float[,] smoothingWeights = new float[,]
    {
        { 1, 2, 1 },
        { 2, 0, 2 },
        { 1, 2, 1 }
    };

    private static float[,] SmoothHeightAdjustments(float[,] heights, float[,] adj, float[,] minHeights, float[,] maxHeights, int res)
    {
        // Allocate new result
        var result = new float[res, res];

        // Calculate each value
        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                // Search neighbouring cells
                float sum = 0.0f;
                float divisor = 0.0f;
                for (int wx = 0; wx < 3; wx++)
                {
                    for (int wy = 0; wy < 3; wy++)
                    {
                        int sx = x + wx - 1;
                        int sy = y + wy - 1;
                        if (sx >= 0 && sx < res && sy >= 0 && sy < res)
                        {
                            float weight = smoothingWeights[wy, wx];
                            sum += adj[sy, sx] * weight;
                            divisor += weight;
                        }
                    }
                }

                // Calculate smoothed adjustment, and clamp
                float a = sum / divisor;            // Smoothed adjustment
                float h = heights[y, x];            // Original height
                float hsmoothed = Mathf.Clamp(h + a, minHeights[y, x], maxHeights[y, x]);

                result[y, x] = hsmoothed - h;
            }
        }

        return result;
    }

    public static void ModifyTerrain(
        GameObject rootObject, Terrain terrain,
        float granularity, float width, float depth, int smooth)
    {
        // Get modifications to apply
        float[,] heights, updatedHeights;
        GetTerrainModifications(rootObject, terrain, granularity, width, depth, smooth, out heights, out updatedHeights);

        // Set heightmap
        terrain.terrainData.SetHeights(0, 0, updatedHeights);
        terrain.Flush();
    }
}
