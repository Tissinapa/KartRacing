using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks transformed meshes that have been saved to a project folder, so that they can automatically
/// be reused in a scene. Used in combination with RacetrackMeshManager.
/// </summary>
[CreateAssetMenu(fileName = "RacetrackMeshes", menuName = "Scriptable Objects/Racetrack Saved Meshes", order = 20)]
public class RacetrackSavedMeshes : ScriptableObject
{
    // Parameters
    public string SaveFolder = "Assets/Meshes/Racetrack Builder";

    [Header("Internal")]
    public int IndexGenerator;

    [Tooltip("Method used to compute mesh hashes. WARNING: Changing this will invalidate all saved meshes.")]
    public HashMethod HashMethod = HashMethod.Simple;           // Default hash method if loading from project saved by an older Racetrack Builder version. Must use same hashing mechanism, otherwise mesh references will become invalid.

    // Saved meshes
    public List<RacetrackMeshReferenceSaved> Meshes = new List<RacetrackMeshReferenceSaved>();

    void Reset()
    {        
        SaveFolder = "Assets/Meshes/Racetrack Builder";
        IndexGenerator = 0;
        HashMethod = HashMethod.MD5WithGapFix;                  // Default hash method in new components.
        Meshes.Clear();
    }

    public string GetNewAssetFilename()
    {
        var filename = string.Format("{0:00000000}.asset", this.IndexGenerator++);
        string folder = this.SaveFolder.Trim();
        if (!string.IsNullOrEmpty(folder) && !folder.EndsWith("/"))
            folder += "/";
        return folder + filename;
    }
}
