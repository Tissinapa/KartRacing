using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Helper object to remove internal faces between consecutive mesh templates of the same type
/// </summary>
public class RacetrackMeshInternalFaceRemover
{
    // Parameters
    private readonly Vector3[] vertices;
    private readonly Matrix4x4 transform;
    private readonly bool removeStartFaces;
    private readonly bool removeEndFaces;
    private readonly float startZCutoff;
    private readonly float endZCutoff;

    // Working
    private readonly Vector3[] triVerts = new Vector3[3];

    /// <summary>
    /// Create a face remover fro the given mesh instance
    /// </summary>
    /// <param name="vertices">Mesh vertices</param>
    /// <param name="transform">Transformation to apply to vertices (in order to calculate effective Z coordinate)</param>
    /// <param name="removeStartFaces">Whether to remove faces from the start of the mesh</param>
    /// <param name="removeEndFaces">Whether to remove faces from the end of the mesh</param>
    /// <param name="startZCutoff">A face is considered an internal start face if all vertex Z coordinates are before this value</param>
    /// <param name="endZCutoff">A face is considered an internal end face if all vertex Z coordinates are after this value</param>
    public RacetrackMeshInternalFaceRemover(Vector3[] vertices, Matrix4x4 transform, bool removeStartFaces, bool removeEndFaces, float startZCutoff, float endZCutoff)
    {
        this.vertices = vertices;
        this.transform = transform;
        this.removeStartFaces = removeStartFaces;
        this.removeEndFaces = removeEndFaces;
        this.startZCutoff = startZCutoff;
        this.endZCutoff = endZCutoff;
    }

    public int[] Apply(int[] triangles)
    {
        // Build array of filtered triangles
        var filteredTriangles = new int[triangles.Length];
        int count = 0;
        for (int current = 0; current < triangles.Length; current += 3)
        {
            // Find triangle vertices
            triVerts[0] = transform.MultiplyPoint(vertices[triangles[current]]);
            triVerts[1] = transform.MultiplyPoint(vertices[triangles[current + 1]]);
            triVerts[2] = transform.MultiplyPoint(vertices[triangles[current + 2]]);

            // Determine whether to remove triangle
            bool remove = false;
            if (removeStartFaces)
                remove = remove || triVerts.All(v => v.z <= startZCutoff);      // Remove if close to start
            if (removeEndFaces)
                remove = remove || triVerts.All(v => v.z >= endZCutoff);        // Remove if close to end

            // Copy triangle if not removed
            if (!remove)
            {
                filteredTriangles[count] = triangles[current];
                filteredTriangles[count + 1] = triangles[current + 1];
                filteredTriangles[count + 2] = triangles[current + 2];
                count += 3;
            }
        }

        // Return filtered array if any removed
        if (count != triangles.Length)
        {
            Array.Resize(ref filteredTriangles, count);     // Truncate array to length
            return filteredTriangles;
        }

        // Otherwise return original array
        return triangles;
    }
}
