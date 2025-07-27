using System;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor implementation of IRacetrackHostServices.
/// Performs actions with undo support.
/// </summary>
public sealed class RacetrackEditorServices : IRacetrackHostServices
{
    public static readonly RacetrackEditorServices Instance = new RacetrackEditorServices();

    private RacetrackEditorServices() { }

    public void DestroyObject(UnityEngine.Object o)
    {
        Undo.DestroyObjectImmediate(o);
    }

    public void ObjectChanging(UnityEngine.Object o)
    {
        UndoHelper.Instance.RecordObject(o);
    }

    public void ObjectCreated(UnityEngine.Object o)
    {
        UndoHelper.Instance.RegisterCreatedObjectUndo(o);
    }

    public void SetTransformParent(Transform transform, Transform parent)
    {
        UndoHelper.Instance.SetTransformParent(transform, parent);
    }

    public void GenerateSecondaryUVSet(Mesh mesh)
    {
        Unwrapping.GenerateSecondaryUVSet(mesh);
    }
}

/// <summary>
/// Helper object for handling undo scopes
/// </summary>
public sealed class UndoHelper
{
    public static readonly UndoHelper Instance = new UndoHelper();

    private string undoName = "";

    private UndoHelper() { }

    public string UndoName
    {
        get { return undoName != "" ? undoName : "Action"; }
    }

    /// <summary>
    /// Attempt to begin an undo operation
    /// </summary>
    /// <param name="name">Name to use</param>
    /// <returns></returns>
    public bool BeginUndo(string name)
    {
        if (undoName == "")
        {
            undoName = name;
            return true;
        }

        return false;
    }

    public void EndUndo()
    {
        undoName = "";
    }

    public void RecordObject(UnityEngine.Object o)
    {
        Undo.RecordObject(o, UndoName);
    }

    public void RegisterCreatedObjectUndo(UnityEngine.Object o)
    {
        Undo.RegisterCreatedObjectUndo(o, UndoName);
    }

    internal void SetTransformParent(Transform transform, Transform parent)
    {
        Undo.SetTransformParent(transform, parent, UndoName);
    }
}

/// <summary>
/// Represents the lifetime of an undo "scope", using the disposable pattern.
/// </summary>
public struct ScopedUndo : IDisposable
{
    private bool isOuterScope;

    public ScopedUndo(string name)
    {
        isOuterScope = UndoHelper.Instance.BeginUndo(name);
    }

    public void Dispose()
    {
        if (isOuterScope)
            UndoHelper.Instance.EndUndo();
    }

    public void RecordObject(UnityEngine.Object o)
    {
        UndoHelper.Instance.RecordObject(o);
    }

    public void RegisterCreatedObjectUndo(UnityEngine.Object o)
    {
        UndoHelper.Instance.RegisterCreatedObjectUndo(o);
    }

    internal void SetTransformParent(Transform transform, Transform newParent)
    {
        UndoHelper.Instance.SetTransformParent(transform, newParent);
    }
}