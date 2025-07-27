using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to an object to indicate it is a racetrack "template".
/// Template objects provide the meshes that will be wrapped around the race track curves
/// such as the road surface (see RacetrackContinuous). They can also provide objects to 
/// be periodically repeated such as support poles (see RacetrackSpacingGroup and RacetrackSpaced).
/// </summary>
public class RacetrackMeshTemplate : MonoBehaviour
{
    [Tooltip("Automatically calculate Min and Max Z from main mesh")]
    public bool AutoMinMaxZ = true;

    [Tooltip("Minimum Z value")]
    public float MinZ = -1.0f;

    [Tooltip("Maximum Z value")]
    public float MaxZ =  1.0f;

    [Tooltip("Type of transformation used to bank the racetrack")]
    public RacetrackTransformType XZAxisTransform = RacetrackTransformType.Rotate;

    /// <summary>
    /// Search for subtrees with a specific component
    /// </summary>
    /// <typeparam name="T">Type of component to find</typeparam>
    /// <returns>Enumerable of subtrees</returns>
    public IEnumerable<T> FindSubtrees<T>(bool activeOnly) where T: MonoBehaviour
    {
        return FindSubtrees<T>(gameObject, activeOnly);
    }

    /// <summary>
    /// Get template space from subtree space transformation matrix
    /// </summary>
    /// <param name="subtree">Component in the subtree object</param>
    /// <returns>Corresponding transformation matrix</returns>
    public Matrix4x4 GetTemplateFromSubtreeMatrix(Component subtree)
    {
        // Note: Rotation and transformation of this object is effectively cancelled out.
        // However we multiply back in the scale factor, as this allows mesh templates to
        // be scaled easily which is useful.
        return Matrix4x4.Scale(transform.lossyScale) * RacetrackUtil.GetAncestorFromDescendentMatrix(this, subtree);
    }

    /// <summary>
    /// Search for subtrees with a specific component
    /// </summary>
    /// <typeparam name="T">Type of component to find</typeparam>
    /// <param name="o">Object to search from</param>
    /// <returns>Enumerable of subtrees</returns>
    private IEnumerable<T> FindSubtrees<T>(GameObject o, bool activeOnly) where T: MonoBehaviour
    {
        var component = o.GetComponent<T>();
        if (component != null && (!activeOnly || IsActiveInTemplate(o)))
        {
            yield return component;
        }
        else
        {
            // Recurse children
            for (int i = 0; i < o.transform.childCount; i++)
                foreach (var s in FindSubtrees<T>(o.transform.GetChild(i).gameObject, activeOnly))
                    yield return s;
        }
    }

    /// <summary>
    /// Check if object is active in the mesh template.
    /// </summary>
    /// <param name="o">Game object to check</param>
    /// <returns>True if gameobject is active and all parent objects are active</returns>
    /// <remarks>
    /// This is similar to "o.activeInHierarchy" except that it only checks parents up to
    /// (and including) the mesh template root object.
    /// In particular it can return "true" for objects in mesh templates whose prefabs
    /// have not been instantiated in the scene (whereas "activeInHeirarchy" always returns 
    /// "false" in this scenario).
    /// </remarks>
    private bool IsActiveInTemplate(GameObject o)
    {
        while (o != null)
        {
            // Stop when inactive object found
            if (!o.activeSelf)
            {
                return false;
            }

            // Stop after template root object
            if (o == gameObject)
            {
                return true;
            }

            o = o.transform.parent.gameObject;
        }

        // Reached the scene root
        return true;
    }
}

public enum RacetrackTransformType
{
    Rotate,
    Shear
}