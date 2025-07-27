using UnityEngine;

/// <summary>
/// Services from the hosting environment.
/// Injected into Racetrack objects to provide access to editor functionality, particularly Undo tracking,
/// and secondary UV coordinate generation.
/// A cut down default implementation is used at runtime (with stub implementations)
/// </summary>
public interface IRacetrackHostServices
{
    /// <summary>
    /// Handle newly created object
    /// </summary>
    void ObjectCreated(UnityEngine.Object o);

    /// <summary>
    /// Destroy a game object
    /// </summary>
    void DestroyObject(UnityEngine.Object o);

    /// <summary>
    /// An object is (/may be) about to change
    /// </summary>
    void ObjectChanging(UnityEngine.Object o);

    /// <summary>
    /// Set transformation's parent
    /// </summary>
    void SetTransformParent(Transform transform, Transform parent);

    /// <summary>
    /// Generate secondary UV set.
    /// In editor this should call Unwrapping.GenerateSecondaryUVSet(mesh).
    /// Runtime implementation can simply do nothing.
    /// </summary>
    /// <param name="mesh">Mesh to update</param>
    void GenerateSecondaryUVSet(Mesh mesh);
}

/// <summary>
/// Host services global variable.
/// Defaults to runtime services. Editor objects should replace it with EditorRacetrackHostServices.Instance
/// </summary>
public static class RacetrackHostServices
{
    public static IRacetrackHostServices Instance = RuntimeRacetrackHostServices.Instance;
}