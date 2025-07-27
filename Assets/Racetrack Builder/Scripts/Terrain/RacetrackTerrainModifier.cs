using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Terrain))]
[ExecuteInEditMode]
public class RacetrackTerrainModifier : MonoBehaviour
{
    private const int MaxUndos = 10;

    [Tooltip("The root object to search for racetracks and junctions to fit the terrain to. If not set, will search the whole scene.")]
    public GameObject rootObject;

    [Tooltip("Distance between height samples when calculating the track or junction height. Smaller values will cause terrain update to take longer.")]
    [Min(0.01f)]
    public float granularity = 0.1f;

    [Tooltip("How high above the terrain the racetrack or junction surfaces should be elevated")]
    public float elevation = 0.1f;

    [Tooltip("Effective width to use when adjusting terrain to racetrack")]
    [Min(0)]
    public float racetrackWidth = 16.0f;

    [Tooltip("Number of smoothing passes to apply")]
    [Range(0, 200)]    
    public int smoothingPasses = 20;

    // Undo/redo logic

    // Changes to undoCounter are captured by Unity's undo/redo method.
    [HideInInspector]
    public int undoCounter;

    // prevUndoCounter is used to detect when undoCounter has changed,
    // meaning Unity has performed an undo/redo.
    private int prevUndoCounter;
    private readonly List<RacetrackTerrainUndoEntry> UndoStack = new List<RacetrackTerrainUndoEntry>();     // Must use list, as may need to remove elements from the "bottom" of the stack, if stack gets too large.
    private readonly Stack<RacetrackTerrainUndoEntry> RedoStack = new Stack<RacetrackTerrainUndoEntry>();

    private void Awake()
    {
        prevUndoCounter = undoCounter;
    }

    void Update()
    {
        if (!Application.isPlaying && undoCounter != prevUndoCounter)
        {
            prevUndoCounter = undoCounter;
            CheckUndoRedo();
        }    
    }

    public void ModifyTerrain()
    {
        var terrain = GetComponent<Terrain>();

        // Get modifications to apply
        float[,] heights, updatedHeights;
        TerrainRoutines.GetTerrainModifications(rootObject, terrain, granularity, racetrackWidth, elevation, smoothingPasses, out heights, out updatedHeights);

        // Set heightmap
        terrain.terrainData.SetHeights(0, 0, updatedHeights);
        terrain.Flush();

        // Write undo entry
        WriteUndoEntry(heights, updatedHeights);
    }

    private void WriteUndoEntry(float[,] heights, float[,] updatedHeights)
    {
        var modification = new RacetrackTerrainUndoEntry
        {
            counterBefore = undoCounter,
            heightsBefore = heights,
            counterAfter = undoCounter + 1,
            heightsAfter = updatedHeights
        };
        UndoStack.Add(modification);

        // Limit # of undo entries, as memory isn't infinite.
        if (UndoStack.Count > MaxUndos)
            UndoStack.RemoveAt(0);

        RedoStack.Clear();
        undoCounter++;
        prevUndoCounter = undoCounter;
    }

    private void CheckUndoRedo()
    {
        if (UndoStack.Count > 0 && UndoStack[UndoStack.Count - 1].counterBefore == undoCounter)
        {
            var modification = UndoStack[UndoStack.Count - 1];
            UndoStack.RemoveAt(UndoStack.Count - 1);
            var terrain = GetComponent<Terrain>();
            terrain.terrainData.SetHeights(0, 0, modification.heightsBefore);
            terrain.Flush();
            RedoStack.Push(modification);
        }
        else if (RedoStack.Count > 0 && RedoStack.Peek().counterAfter == undoCounter)
        {
            var modification = RedoStack.Pop();
            var terrain = GetComponent<Terrain>();
            terrain.terrainData.SetHeights(0, 0, modification.heightsAfter);
            terrain.Flush();
            UndoStack.Add(modification);
        }
        else
        {
            Debug.Log("Cannot undo/redo RacetrackTerrainModifier change. Undo/redo data may have been freed.");
        }
    }

    private class RacetrackTerrainUndoEntry
    {
        public int counterBefore;
        public int counterAfter;
        public float[,] heightsBefore;
        public float[,] heightsAfter;
    }
}
