public struct EditorSettings
{
    // Track generation parameters
    public float SegmentLength;
    public Racetrack1DInterpolationType BankAngleInterpolation;
    public Racetrack1DInterpolationType WideningInterpolation;
    public bool RemoveInternalFaces;

    // Respawning
    public float RespawnHeight;
    public float RespawnZOffset;

    // Editor behaviour
    public bool ShowManipulationHandles;
    public bool ShowOnScreenButtons;
    public bool AutoUpdate;

    // Copy for prefab function
    public bool MoveStartToOrigin;
    public CopyForPrefabAlignType AlignStart;
    public bool CreateStartMarker;
    public bool CreateEndMarker;
}

public enum CopyForPrefabAlignType
{
    AllAxes,
    YAxis,
    No
}

public interface IHasEditorSettings
{
    EditorSettings GetEditorSettings();
}