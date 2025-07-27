using UnityEngine;

public class RacetrackGroup : MonoBehaviour, IHasEditorSettings
{
    [Header("Mesh warping")]
    [Tooltip("Curves are converted into a sequence of small straight 'segments' of this length.")]
    public float SegmentLength = 0.25f;

    [Tooltip("How to interpolate between curve bank (Z) angles")]
    public Racetrack1DInterpolationType BankAngleInterpolation = Racetrack1DInterpolationType.Bezier;

    [Tooltip("How to interpolate between 'widening' values")]
    public Racetrack1DInterpolationType WideningInterpolation;

    [Tooltip("Automatically remove internal faces between consecutive mesh templates of the same type")]
    public bool RemoveInternalFaces = true;

    [Header("Respawning")]
    [Tooltip("Height above road for car respawn points. Used by RacetrackProgressTracker")]
    public float RespawnHeight = 0.75f;
    public float RespawnZOffset = 2.0f;

    [Tooltip("Display rotate and translate handles for the curve shape (translate handles for Bezier curves only)")]
    public bool ShowManipulationHandles = true;

    [Tooltip("Show buttons (and other UI) in the main editor window")]
    public bool ShowOnScreenButtons = true;

    [Tooltip("Automatically update track after (some) curve changes")]
    public bool AutoUpdate = true;

    public bool MoveStartToOrigin = true;
    public CopyForPrefabAlignType AlignStart = CopyForPrefabAlignType.AllAxes;
    public bool CreateStartMarker = false;
    public bool CreateEndMarker = true;

    public void Reset()
    {
        SegmentLength = 0.25f;
        BankAngleInterpolation = Racetrack1DInterpolationType.Bezier;
        WideningInterpolation = Racetrack1DInterpolationType.Linear;
        RemoveInternalFaces = true;
        RespawnHeight = 0.75f;
        RespawnZOffset = 2.0f;
        ShowManipulationHandles = true;
        ShowOnScreenButtons = true;
        AutoUpdate = true;
        MoveStartToOrigin = true;
        AlignStart = CopyForPrefabAlignType.AllAxes;
        CreateStartMarker = false;
        CreateEndMarker = true;
    }

#region IHasEditorSettings

    public EditorSettings GetEditorSettings()
    {
        return new EditorSettings
        {
            SegmentLength = this.SegmentLength,
            BankAngleInterpolation = this.BankAngleInterpolation,
            WideningInterpolation = this.WideningInterpolation,
            RemoveInternalFaces = this.RemoveInternalFaces,
            RespawnHeight = this.RespawnHeight,
            RespawnZOffset = this.RespawnZOffset,
            AutoUpdate = this.AutoUpdate,
            ShowManipulationHandles = this.ShowManipulationHandles,
            ShowOnScreenButtons = this.ShowOnScreenButtons,
            MoveStartToOrigin = this.MoveStartToOrigin,
            AlignStart = this.AlignStart,
            CreateStartMarker = this.CreateStartMarker,
            CreateEndMarker = this.CreateEndMarker
        };
    }

    #endregion
}
