using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class RacetrackMeshReferenceBase
{
    // Effective key
    // If all these match we assume the generated meshes will be identical.
    public Mesh BaseMesh;
    public int TemplateCopyHash;
    public int TransformHash;               // Hash of mesh->template transformation matrix            

    // Transformed mesh
    public Mesh Mesh;
}

[Serializable]
public class RacetrackMeshReference : RacetrackMeshReferenceBase
{
    // Reference count. For debugging, and cleaning up unreferenced meshes.
    public int RefCount;    

    // Whether mesh should be saved into asset folder.
    // Racetrack builder will try to take a reasonable guess.
    // User can override before clicking "Save scene meshes".
    public bool SelectForSave;

    [NonSerialized]
    public HashSet<RacetrackMeshTemplate> MeshTemplates;

    [NonSerialized]
    public string BaseMeshName;
}

[Serializable]
public class RacetrackMeshReferenceSaved : RacetrackMeshReferenceBase
{
}