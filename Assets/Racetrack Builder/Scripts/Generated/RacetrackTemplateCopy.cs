using UnityEngine;

/// <summary>
/// Attached to the object that is created from a mesh template.
/// </summary>
public class RacetrackTemplateCopy : MonoBehaviour {

    /// <summary>
    /// The mesh template this object was built from.
    /// </summary>
    public RacetrackMeshTemplate Template;

    /// <summary>
    /// Hash of RacetrackTemplateCopyParams data used to create this template copy.
    /// Can be used to determine if the copy can be re-used.
    /// A RacetrackTemplateCopyParams object will generate identical continuous meshes to this copy if:
    ///     * The Template is the same, AND
    ///     * The ParamHash is the same
    /// (barring hash collisions)
    /// </summary>
    public int ParamHash;

    /// <summary>
    /// Hash of the RacetrackTemplateCopyParams spacing group data used to create this template copy.
    /// Can be used to determine if the copy can be re-used.
    /// A RacetrackTemplateCopyParams object will generate an identical copy if:
    ///     * The Template is the same, AND
    ///     * The ParamHash is the same, AND
    ///     * The SpacingGroupsHash is the same
    /// (barring hash collisions)
    /// </summary>
    public int SpacingGroupsHash;
}
