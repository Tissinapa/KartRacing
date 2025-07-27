using UnityEngine;

/// <summary>
/// Attached to object that is created when a spaced object is copied.
/// Mainly used to identify them so that they can be deleted when reusing a 
/// RacetrackTemplateCopy's continuous meshes but recreating its spaced objects.
/// </summary>
public class RacetrackSpacedCopy : MonoBehaviour
{
    public int SpacingGroupIndex;
}
