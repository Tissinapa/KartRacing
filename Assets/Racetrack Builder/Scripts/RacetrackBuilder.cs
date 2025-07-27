using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RacetrackBuilder
{
    private const int MaxSpacingGroups = 16;

    public static void Build(Racetrack track, bool generateSecondaryUVs = false)
    {
        var settings = track.GetEditorSettings();
        Build(
            track.Curves,
            track.Path,
            track.transform.localToWorldMatrix,
            settings.RemoveInternalFaces,
            generateSecondaryUVs,
            RacetrackMeshInfoCache.Instance);
    }

    public static void Build(
        List<RacetrackCurve> curves,
        RacetrackPath path,
        Matrix4x4 worldFromTrack,
        bool removeInternalFaces,
        bool generateSecondaryUVs,
        RacetrackMeshInfoCache meshCache)
    {
        var savedServices = RacetrackHostServices.Instance;
        var hashMethod = RacetrackMeshManager.GetHashMethod();
        try
        {
            RacetrackHostServices.Instance = RuntimeRacetrackHostServices.Instance;
            PositionCurves(curves, path);

            // Delete and recreate all mesh template copies
            var templateCopyParams = GetTemplateCopyParams(curves, path, meshCache, removeInternalFaces).ToList();
            DeleteTemplateCopies(curves);
            foreach (var p in templateCopyParams)
                CreateMeshTemplateCopy(p, meshCache, generateSecondaryUVs, worldFromTrack, hashMethod);

            // Delete unused mesh copies
            var meshManager = GameObject.FindObjectOfType<RacetrackMeshManager>();
            if (meshManager != null)
                meshManager.GarbageCollect();
        }
        finally
        {
            RacetrackHostServices.Instance = savedServices;
        }
    }

    public static void Update(Racetrack track)
    {
        RacetrackHostServices.Instance = RuntimeRacetrackHostServices.Instance;
        var settings = track.GetEditorSettings();
        Update(
            track.Curves,
            track.Path,
            track.transform.localToWorldMatrix,
            settings.RemoveInternalFaces,
            RacetrackMeshInfoCache.Instance,
            RacetrackMeshManager.GetHashMethod(),
            out string msg);
        track.LastUpdateMsg = msg;
    }

    public static void Update(
        List<RacetrackCurve> curves,
        RacetrackPath path,
        Matrix4x4 worldFromTrack,
        bool removeInternalFaces,
        RacetrackMeshInfoCache meshCache,
        HashMethod hashMethod,
        out string msg)
    {
        var savedServices = RacetrackHostServices.Instance;
        try
        {
            RacetrackHostServices.Instance = RuntimeRacetrackHostServices.Instance;
            PositionCurves(curves, path);
            var templateCopyParams = GetTemplateCopyParams(curves, path, meshCache, removeInternalFaces).ToList();

            // Create list of existing template copies
            var existing = curves
                .SelectMany(c => c.gameObject.GetComponentsInChildren<RacetrackTemplateCopy>())
                .ToList();

            int createCount = 0;
            int reuseCount = 0;
            int reuseMeshCount = 0;
            foreach (var p in templateCopyParams)
            {
                // Calculate hashes for meshes and spacing groups
                int hash = p.CalcHash(CreateHasher(hashMethod));
                int spacingGroupsHash = p.CalcSpacingGroupsHash(CreateHasher(hashMethod));

                // Look for matching existing template copy.
                // A copy can be reused if:
                //  * It has the same template, AND
                //  * It has the same ParamHash
                // It can be re-used without modification if it also has the same SpacingGroupHash.
                // Otherwise the continuous meshes can be reused, but the spaced objects need to be 
                // regenerated (which is still a lot faster than recreating the whole template copy).
                RacetrackTemplateCopy match = null;
                bool spacedObjectsMatch = false;
                foreach (var copy in existing)
                {
                    // Can reuse this copy?
                    if (copy.Template == p.Template && copy.ParamHash == hash)
                    {
                        // Found exact match (including spaced objects)
                        if (copy.SpacingGroupsHash == spacingGroupsHash)
                        {
                            // Use this copy, and terminate the search
                            match = copy;
                            spacedObjectsMatch = true;
                            break;
                        }

                        // Otherwise keep this as the best match so far
                        if (match == null)
                            match = copy;
                    }
                }

                // Reuse existing template copy if possible
                if (match != null)
                {
                    // Move to correct position, and place underneath corresponding curve
                    RacetrackHostServices.Instance.ObjectChanging(match.gameObject);
                    var seg = p.PathSection.Path.GetSegmentAtZ(p.PathSection.StartZ);
                    match.gameObject.transform.parent = p.Curve.transform;
                    p.PathSection.Path.GetSegmentTransform(seg, match.gameObject.transform);

                    // Update curve range in RacetrackSurface components
                    var surfaces = match.gameObject.GetComponentsInChildren<RacetrackSurface>();
                    if (surfaces.Any())
                    {
                        var curveRange = GetCurveRange(p.PathSection);
                        foreach (var surface in surfaces)
                        {
                            surface.StartCurveIndex = curveRange.StartIndex;
                            surface.EndCurveIndex = curveRange.EndIndex;
                        }
                    }

                    // Recreate spaced objects if necessary
                    if (!spacedObjectsMatch)
                    {
                        // Remove previous spaced object copies
                        var existingSpaced = match.gameObject.GetComponentsInChildren<RacetrackSpacedCopy>();
                        foreach (var e in existingSpaced)
                            RacetrackHostServices.Instance.DestroyObject(e.gameObject);

                        // Create new spaced objects
                        CreateMeshTemplateCopySpacedItems(match.gameObject, p, worldFromTrack);
                        match.SpacingGroupsHash = spacingGroupsHash;

                        reuseMeshCount++;
                    }
                    else
                        reuseCount++;

                    // Remove from existing set
                    existing.Remove(match);
                }
                else
                {
                    CreateMeshTemplateCopy(p, meshCache, false, worldFromTrack, hash, spacingGroupsHash);

                    createCount++;
                }
            }

            // Delete any existing template copies that have not been reused
            foreach (var e in existing)
                RacetrackHostServices.Instance.DestroyObject(e.gameObject);

            msg = string.Format("{0} template copies created, {1} unmodified, {2} spaced items only", createCount, reuseCount, reuseMeshCount);

            // Delete unused mesh copies
            var meshManager = GameObject.FindObjectOfType<RacetrackMeshManager>();
            if (meshManager != null)
                meshManager.GarbageCollect();
        }
        finally
        {
            RacetrackHostServices.Instance = savedServices;
        }
    }

    /// <summary>
    /// Calculate curve runtime information array for racetrack
    /// </summary>
    public static void CalculateRuntimeInfo(Racetrack track)
    {
        var settings = track.GetEditorSettings();
        track.CurveInfos = CalculateRuntimeInfo(track.Curves, track.Path, settings.RespawnZOffset, settings.RespawnHeight).ToArray();
    }

    public static List<Racetrack.CurveRuntimeInfo> CalculateRuntimeInfo(List<RacetrackCurve> curves, RacetrackPath path, float respawnZOffset, float respawnHeight)
    {
        var infos = new List<Racetrack.CurveRuntimeInfo>();
        float curveZOffset = 0.0f;
        foreach (var curve in curves)
        {
            int segIndex = Mathf.FloorToInt(curveZOffset / path.Params.SegmentLength);

            // Calculate curve runtime info
            var info = new Racetrack.CurveRuntimeInfo();
            info.IsJump = curve.IsJump;
            info.CanRespawn = curve.CanRespawn;
            info.zOffset = curveZOffset;

            // Calculate normal in track space
            int endSegIndex = Mathf.FloorToInt((curveZOffset + curve.Length) / path.Params.SegmentLength);
            int midSegIndex = (segIndex + endSegIndex) / 2;
            info.Normal = path.GetSegment(midSegIndex).GetSegmentToTrack().MultiplyVector(Vector3.up);

            // Calculate respawn point and direction vectors in track space
            int respawnSeg = Math.Min(segIndex + Mathf.CeilToInt(respawnZOffset / path.Params.SegmentLength), midSegIndex);
            Matrix4x4 respawnTransform = path.GetSegment(respawnSeg).GetSegmentToTrack();
            info.RespawnPosition = respawnTransform.MultiplyPoint(Vector3.up * respawnHeight);
            info.RespawnRotation = Quaternion.LookRotation(
                respawnTransform.MultiplyVector(Vector3.forward).normalized,
                respawnTransform.MultiplyVector(Vector3.up).normalized);

            infos.Add(info);
            curveZOffset += curve.Length;
        }

        return infos;
    }

    /// <summary>
    /// Position curves along racetrack path
    /// </summary>
    public static void PositionCurves(List<RacetrackCurve> curves, RacetrackPath path)
    {
        var hostServices = RacetrackHostServices.Instance;

        // Position curve objects at the start of their curves
        float curveZOffset = 0.0f;
        int index = 0;
        foreach (var curve in curves)
        {
            // Position curve at first segment
            int segIndex = Mathf.FloorToInt(curveZOffset / path.Params.SegmentLength);
            var seg = path.GetSegment(segIndex);
            hostServices.ObjectChanging(curve.transform);
            path.GetSegmentTransform(seg, curve.transform);
            hostServices.ObjectChanging(curve);
            curve.Index = index;

            // Move on to next curve
            index++;
            curveZOffset += curve.Length;
        }
    }

    /// <summary>
    /// Get parameters for racetrack mesh template copy objects
    /// </summary>
    public static IEnumerable<RacetrackTemplateCopyParams> GetTemplateCopyParams(
        List<RacetrackCurve> curves, 
        RacetrackPath path, 
        RacetrackMeshInfoCache meshCache,
        bool removeInternalFaces)
    {
        // Initial state at start of racetrack
        float meshZOffset = 0.0f;
        float curveZOffset = 0.0f;
        int curveIndex = 0;
        float totalLength = path.TotalLength;
        RacetrackMeshTemplate template = null;
        RacetrackMeshInfoCache.TemplateInfo templateInfo = null;

        RacetrackSpacingGroupState[] spacingGroups = new RacetrackSpacingGroupState[MaxSpacingGroups];
        for (int i = 0; i < MaxSpacingGroups; i++)
            spacingGroups[i] = new RacetrackSpacingGroupState { IsActive = false };

        while (curveIndex < curves.Count)
        {
            var curve = curves[curveIndex];

            // Template switches
            if (curve.Template != null && curve.Template != template)
            {
                template = curve.Template;
                templateInfo = meshCache.GetTemplateInfo(template);
                ActivateTemplateSpacingGroups(template, spacingGroups, meshZOffset, path.Params.SegmentLength);
            }

            // Skip over jumps
            if (curve.IsJump || template == null)
            {
                // Skip to next curve
                curveIndex++;
                curveZOffset += curve.Length;

                // Align meshes and groups to start of next curve
                meshZOffset = curveZOffset;
                foreach (var group in spacingGroups)
                    if (group.IsActive)
                        group.ZOffset = curveZOffset;

                // Don't generate any template copies for this curve
                continue;
            }

            // Mesh alignment logic.
            // Search forward to find next mesh alignment point.
            // Calculate: 
            //      Curve Z offset
            //      Template copy Z offset
            // Use to determine z scale in order to align range.
            int endCurveIndex = curveIndex;
            float endMeshZOffset = meshZOffset;
            float endCurveZOffset = curveZOffset;
            RacetrackMeshTemplate endTemplate = template;
            RacetrackMeshInfoCache.TemplateInfo endTemplateInfo = templateInfo;
            do
            {
                // Step over curve
                RacetrackCurve endCurve = curves[endCurveIndex];

                // Template switches
                if (endCurve.Template != null && endCurve.Template != endTemplate)
                {
                    endTemplate = endCurve.Template;
                    endTemplateInfo = meshCache.GetTemplateInfo(endTemplate);
                }

                // Find end of mesh template
                endCurveZOffset += endCurve.Length;
                float endTemplateLength = endTemplateInfo.Length;
                if (endTemplateLength == 0.0f)
                    endTemplateLength = 1.0f;                          // Prevent invalid templates causing infinite loops!
                while (endMeshZOffset < endCurveZOffset)
                    endMeshZOffset += endTemplateLength;

                endCurveIndex++;
            } while (endCurveIndex < curves.Count                   // Stop at end of track
                && !curves[endCurveIndex - 1].AlignMeshesToEnd      // Stop after explicitly aligned curve
                && !curves[endCurveIndex].IsJump);                  // Stop before jump

            // Calculate z scale
            float zScale;
            bool isAligned = curves[endCurveIndex - 1].AlignMeshesToEnd;
            if (isAligned)
            {
                // Adjust scale so that template copies align to the end of the curve.
                float curveRange = endCurveZOffset - curveZOffset;
                float templateRange = endMeshZOffset - meshZOffset;
                zScale = curveRange / templateRange;
            }
            else
                zScale = 1.0f;      // Range is not aligned. No scaling.

            // Lay out mesh template copies for range
            float startMeshZOffset = meshZOffset;
            while (curveIndex < endCurveIndex)
            {
                curve = curves[curveIndex];

                // Determine whether start and end faces are visible for curve.
                // Used for internal face removal logic
                bool curveStartFaceVis = curveIndex == 0                                // Start of racetrack
                                      || curve.Template != null                         // Template change
                                      || curves[curve.Index - 1].IsJump;                // Previous curve was a jump

                bool curveEndFaceVis   = curveIndex == curves.Count - 1                 // End of racetrack
                                      || curves[curveIndex + 1].Template != null        // Template change
                                      || curves[curveIndex + 1].IsJump;                 // Next curve is jump
                
                // Template switches
                if (curve.Template != null && curve.Template != template)
                {
                    template = curve.Template;
                    templateInfo = meshCache.GetTemplateInfo(template);
                    ActivateTemplateSpacingGroups(template, spacingGroups, meshZOffset, path.Params.SegmentLength);
                }

                // Lay out mesh templates for curve.
                float nextCurveZOffset = curveZOffset + curve.Length;
                bool isFirstMeshInCurve = true;
                while (meshZOffset < nextCurveZOffset)
                {
                    bool isLastMeshInCurve = meshZOffset + templateInfo.Length >= nextCurveZOffset;

                    // Unscaled start/end
                    float startZ = meshZOffset;
                    float endZ   = meshZOffset + templateInfo.Length;

                    // Scaled start/end
                    float scaledStartZ = startMeshZOffset + (startZ - startMeshZOffset) * zScale;
                    float scaledEndZ   = startMeshZOffset + (endZ   - startMeshZOffset) * zScale;

                    // Internal face removal
                    bool removeStartFaces = removeInternalFaces && (!isFirstMeshInCurve || !curveStartFaceVis);
                    bool removeEndFaces   = removeInternalFaces && (!isLastMeshInCurve  || !curveEndFaceVis);

                    // Can override at curve level
                    if (curve.RemoveStartInternalFaces != RemoveInternalFacesOption.Auto)
                        removeStartFaces = curve.RemoveStartInternalFaces == RemoveInternalFacesOption.Yes;
                    if (curve.RemoveEndInternalFaces != RemoveInternalFacesOption.Auto)
                        removeEndFaces = curve.RemoveEndInternalFaces == RemoveInternalFacesOption.Yes;

                    // Emit parameters
                    yield return new RacetrackTemplateCopyParams
                    {
                        Curve = curve,
                        PathSection = new RacetrackPathSection { Path = path, StartZ = scaledStartZ, EndZ = scaledEndZ },
                        ZScale = zScale,
                        Template = template,
                        TemplateInfo = templateInfo,
                        SpacingGroupStates = spacingGroups.Select(g => g.Clone()).ToArray(),
                        RemoveStartFaces = removeStartFaces,
                        RemoveEndFaces   = removeEndFaces
                    };
                    meshZOffset += templateInfo.Length;
                    isFirstMeshInCurve = false;

                    // Advance spacing groups
                    foreach (var group in spacingGroups)
                        if (group.IsActive)
                            while (group.ZOffset + group.SpacingBefore < scaledEndZ)
                                group.ZOffset += group.Spacing;
                }

                // Next curve
                curveIndex++;
                curveZOffset = nextCurveZOffset;
            }

            if (isAligned)
                meshZOffset = curveZOffset;            
        }
    }

    public static void DeleteTemplateCopies(List<RacetrackCurve> curves)
    {
        // Find generated meshes.
        // These have a RacetrackTemplateCopy component
        var children = curves
            .SelectMany(c => c.gameObject.GetComponentsInChildren<RacetrackTemplateCopy>())
            .ToList();

        // Delete them
        foreach (var child in children)
            RacetrackHostServices.Instance.DestroyObject(child.gameObject);
    }

    public static GameObject CreateMeshTemplateCopy(
    RacetrackTemplateCopyParams p,
    RacetrackMeshInfoCache meshCache,
    bool generateSecondaryUVs,
    Matrix4x4 worldFromTrack,
    HashMethod hashMethod)
    {
        return CreateMeshTemplateCopy(p, meshCache, generateSecondaryUVs, worldFromTrack, p.CalcHash(CreateHasher(hashMethod)), p.CalcSpacingGroupsHash(CreateHasher(hashMethod)));
    }

    public static GameObject CreateMeshTemplateCopy(
        RacetrackTemplateCopyParams p, 
        RacetrackMeshInfoCache meshCache, 
        bool generateSecondaryUVs, 
        Matrix4x4 worldFromTrack,
        int hash,
        int spacingGroupsHash)
    {
        var seg = p.PathSection.Path.GetSegmentAtZ(p.PathSection.StartZ);

        // Create template copy object
        var templateCopy = new GameObject();
        RacetrackHostServices.Instance.ObjectCreated(templateCopy);
        templateCopy.transform.parent = p.Curve.transform;
        templateCopy.isStatic = p.Curve.gameObject.isStatic;
        templateCopy.name = p.Curve.name + " > " + p.Template.name;
        p.PathSection.Path.GetSegmentTransform(seg, templateCopy.transform);

        // Add template copy component
        var copyComponent = templateCopy.AddComponent<RacetrackTemplateCopy>();
        copyComponent.Template = p.Template;
        copyComponent.ParamHash = hash;
        copyComponent.SpacingGroupsHash = spacingGroupsHash;

        // If mesh manager exists in scene, use it to reuse duplicate meshes
        var meshManager = GameObject.FindObjectOfType<RacetrackMeshManager>();

        // Pass 1: Generate continuous meshes
        var continuous = p.Template.FindSubtrees<RacetrackContinuous>(true);
        int meshIndex = 0;
        foreach (var subtree in continuous)
        {
            // Duplicate subtree
            var subtreeCopy = GameObject.Instantiate(subtree);
            subtreeCopy.transform.parent = templateCopy.transform;
            subtreeCopy.gameObject.isStatic = templateCopy.isStatic;
            subtreeCopy.name += " Continuous";

            // Set object transform to first segment in sub-path
            p.PathSection.Path.GetSegmentTransform(seg, subtreeCopy.transform);

            // Need to take into account relative position of continuous subtree within template object
            Matrix4x4 templateFromSubtree = p.Template.GetTemplateFromSubtreeMatrix(subtree);

            // Clone and warp displayed meshes
            // Use mesh filter from original mesh template to calculate the templateFromMesh
            // transform, so that it comes out identical for the same mesh each time.
            // (Using the cloned mesh filter produces essentially the same result, but minor
            // floating point rounding errors can cause Racetrack Builder to sometimes treat 
            // them as different, preventing it from re-using the same mesh.)
            var origMeshFilters = subtree.GetComponentsInChildren<MeshFilter>();
            var meshFilters = subtreeCopy.GetComponentsInChildren<MeshFilter>();
            bool isFirstMesh = true;
            for (int i = 0; i < meshFilters.Length; i++) {
                var mf = meshFilters[i];
                var omf = origMeshFilters[i];
                Matrix4x4 subtreeFromMesh = RacetrackUtil.GetAncestorFromDescendentMatrix(subtree, omf);
                Matrix4x4 templateFromMesh = templateFromSubtree * subtreeFromMesh;

                // Attempt to re-use mesh if mesh manager is available
                var baseMesh = mf.sharedMesh;
                if (baseMesh == null)
                {
                    // Avoid some ugly null reference exceptions if mesh filter has no mesh.
                    Debug.LogWarningFormat("MeshFilter '{0}' in RacetrackMeshTemplate '{1}' has no Mesh assigned. Skipping.", mf, p.Template);
                    continue;
                }
                if (baseMesh.vertices == null || !baseMesh.vertices.Any())
                {
                    Debug.LogWarningFormat("MeshFilter '{0}' in RacetrackMeshTemplate '{1}' references a Mesh with no vertices. Skipping.", mf, p.Template);
                    continue;
                }
                var existingMesh = meshManager != null ? meshManager.GetMesh(baseMesh, hash, templateFromMesh) : null;
                if (existingMesh != null)
                {
                    mf.sharedMesh = existingMesh;
                }
                else
                {
                    // Generate new mesh
                    var meshVertInfo = meshCache.GetMeshVertexInfo(mf.sharedMesh);
                    mf.sharedMesh = CloneMesh(mf.sharedMesh);
                    Matrix4x4 meshFromWorld = mf.transform.localToWorldMatrix.inverse;
                    WarpMeshToCurves(
                        mf.sharedMesh,
                        p.TemplateInfo,
                        meshVertInfo,
                        p.PathSection,
                        p.ZScale,
                        templateFromMesh,
                        meshFromWorld,
                        worldFromTrack,
                        GetWidenRangeForMesh(p.Template, subtree, subtreeCopy, mf),
                        false,
                        p.Template.XZAxisTransform,
                        mf.GetComponents<RacetrackUVGenerator>(),
                        p.RemoveStartFaces,
                        p.RemoveEndFaces,
                        subtree.InternalFaceZThreshold);
                }

                // Store generated mesh
                if (meshManager != null && existingMesh == null)
                    meshManager.StoreMesh(baseMesh, hash, templateFromMesh, mf.sharedMesh, p);

                // Generate secondary UVs
                if (generateSecondaryUVs)
                    RacetrackHostServices.Instance.GenerateSecondaryUVSet(mf.sharedMesh);

                // First continuous mesh is considered to be the main track surface.
                if (isFirstMesh)
                {
                    var surface = mf.gameObject.AddComponent<RacetrackSurface>();
                    var endSeg = p.PathSection.Path.GetSegmentAtZ(p.PathSection.EndZ - 0.00001f);
                    surface.StartCurveIndex = seg.Curve.Index;
                    surface.EndCurveIndex = endSeg.Curve.Index;
                    isFirstMesh = false;
                }

                meshIndex++;
            }

            // Clone and warp mesh colliders
            var origMeshColliders = subtree.GetComponentsInChildren<MeshCollider>();
            var meshColliders = subtreeCopy.GetComponentsInChildren<MeshCollider>();
            for (int i = 0; i < meshColliders.Length; i++)
            {
                var mc = meshColliders[i];
                var omc = origMeshColliders[i];
                if (mc.sharedMesh == null) continue;

                Matrix4x4 subtreeFromMesh = subtree.transform.localToWorldMatrix.inverse * omc.transform.localToWorldMatrix;
                Matrix4x4 templateFromMesh = templateFromSubtree * subtreeFromMesh;

                // Attempt to re-use mesh if mesh manager is available
                var baseMesh = mc.sharedMesh;
                var existingMesh = meshManager != null ? meshManager.GetMesh(baseMesh, hash, templateFromMesh) : null;
                if (existingMesh != null)
                {
                    mc.sharedMesh = existingMesh;
                }
                else
                {
                    // Generate new mesh
                    var meshVertInfo = meshCache.GetMeshVertexInfo(mc.sharedMesh);
                    mc.sharedMesh = CloneMesh(mc.sharedMesh);
                    Matrix4x4 meshFromWorld = mc.transform.localToWorldMatrix.inverse;
                    WarpMeshToCurves(
                        mc.sharedMesh,
                        p.TemplateInfo,
                        meshVertInfo,
                        p.PathSection,
                        p.ZScale,
                        templateFromMesh,
                        meshFromWorld,
                        worldFromTrack,
                        GetWidenRangeForMesh(p.Template, subtree, subtreeCopy, mc),
                        true,
                        p.Template.XZAxisTransform);
                }

                // Store generated mesh
                if (meshManager != null && existingMesh == null)
                    meshManager.StoreMesh(baseMesh, hash, templateFromMesh, mc.sharedMesh, p);

                meshIndex++;
            }
        };

        CreateMeshTemplateCopySpacedItems(templateCopy, p, worldFromTrack);

        return templateCopy;
    }

    public static IHasher CreateHasher(HashMethod hashMethod) {
        switch (hashMethod)
        {
            case HashMethod.Simple:
                return new SimpleHasher();
            case HashMethod.MD5:
                return new MD5Hasher(hashMethod);
            default:
                return new MD5Hasher(hashMethod);
        }
    }

    private static void CreateMeshTemplateCopySpacedItems(GameObject templateCopy, RacetrackTemplateCopyParams p, Matrix4x4 worldFromTrack)
    {
        // Pass 2: Generate spaced meshes
        foreach (var subtree in p.Template.FindSubtrees<RacetrackSpaced>(true))
        {
            // Search up parent chain for spacing group
            var spacingGroup = subtree.GetComponentsInParent<RacetrackSpacingGroup>(true).FirstOrDefault();
            if (spacingGroup == null)
            {
                Debug.LogError("Cannot find spacing group for spaced template component: " + subtree.name);
                continue;
            }

            // Validate
            if (spacingGroup.Index < 0 || spacingGroup.Index >= MaxSpacingGroups)
            {
                Debug.LogError("Invalid spacing group " + spacingGroup.Index + " found in template: " + p.Template.name);
                continue;
            }
            if (spacingGroup.Spacing < p.PathSection.Path.Params.SegmentLength)
            {
                Debug.LogError("Spacing too small in spacing group, in template: " + p.Template.name);
                continue;
            }

            // Walk spacing group forward to start of template copy
            var groupState = p.SpacingGroupStates[spacingGroup.Index];
            if (!groupState.IsActive)
            {
                Debug.LogError("Group state for spacing group " + spacingGroup.Index + " is inactive in template: " + p.Template.name);
                continue;
            }
            float z = groupState.ZOffset;
            while (z + groupState.SpacingBefore < p.PathSection.StartZ)
                z += groupState.Spacing;

            // Generate spaced objects for template copy
            float endZ = p.PathSection.EndZ;
            if (endZ > p.PathSection.Path.TotalLength && p.PathSection.Path.Params.Overrun == RacetrackMeshOverrunOption.Loop)
                endZ = p.PathSection.Path.TotalLength;
            while (z + groupState.SpacingBefore < endZ)
            {
                var spaceSeg = p.PathSection.Path.GetSegmentAndOffset(z + groupState.SpacingBefore, out float segZ);

                // Check track angle restrictions
                float trackXAngle = Mathf.Abs(RacetrackUtil.LocalAngle(spaceSeg.Direction.x));
                float trackZAngle = Mathf.Abs(RacetrackUtil.LocalAngle(spaceSeg.Direction.z));
                if (trackXAngle > subtree.MaxXAngle || trackZAngle > subtree.MaxZAngle)
                {
                    z += groupState.Spacing;                         // Outside angle restrictions. Skip creating this one
                    continue;
                }

                // Duplicate subtree
                var subtreeCopy = GameObject.Instantiate(subtree);
                subtreeCopy.transform.parent = templateCopy.transform;
                subtreeCopy.gameObject.isStatic = templateCopy.isStatic;
                subtreeCopy.name += " Spacing group " + spacingGroup.Index;
                var spacedCopy = subtreeCopy.gameObject.AddComponent<RacetrackSpacedCopy>();     // Tag with RacetrackSpacedCopy component
                spacedCopy.SpacingGroupIndex = spacingGroup.Index;

                // Calculate local to track tranform matrix for subtree.
                Matrix4x4 templateFromSubtree = p.Template.GetTemplateFromSubtreeMatrix(subtree);

                if (subtree.ApplyWidening)
                {
                    // Adjust X for track widening
                    var widening = spaceSeg.GetWidening(segZ);
                    Vector3 templatePos = RacetrackUtil.ToVector3(templateFromSubtree.GetColumn(3));
                    var widenRanges = GetWidenRangeForSubtree(p.Template, subtree);
                    float adjustedX = widenRanges.Apply(templatePos.x, widening);

                    // Multiply in translation adjustment
                    if (adjustedX != templatePos.x)
                        templateFromSubtree = Matrix4x4.Translate(new Vector3(adjustedX - templatePos.x, 0.0f, 0.0f)) * templateFromSubtree;
                }

                Matrix4x4 trackFromSeg = p.Template.XZAxisTransform == RacetrackTransformType.Rotate
                    ? spaceSeg.GetSegmentToTrack(segZ)
                    : spaceSeg.GetShearSegmentToTrack(segZ);

                Matrix4x4 trackFromSubtree = trackFromSeg                                       // Segment -> Track
                                           * templateFromSubtree;                               // Subtree -> Segment

                if (subtree.IsVertical)
                    trackFromSubtree = RacetrackUtil.AlignYAxisToWorldY(trackFromSubtree);

                // Get local to world transform matrix for subtree.
                Matrix4x4 worldFromSubtree = worldFromTrack * trackFromSubtree;

                // Calculate local transform. Essentially subtree->template space transform for adjusted position and orientation
                Matrix4x4 templateFromWorld = templateCopy.transform.localToWorldMatrix.inverse;
                Matrix4x4 newTemplateFromSubtree = templateFromWorld * worldFromSubtree;

                // Use to set transform
                subtreeCopy.transform.localPosition = newTemplateFromSubtree.MultiplyPoint(Vector3.zero);
                subtreeCopy.transform.localRotation = newTemplateFromSubtree.rotation;
                subtreeCopy.transform.localScale = newTemplateFromSubtree.lossyScale;

                // Next spaced item
                z += groupState.Spacing;
            }
        }
    }

    private static void ActivateTemplateSpacingGroups(RacetrackMeshTemplate template, RacetrackSpacingGroupState[] spacingGroups, float meshZOffset, float segmentLength)
    {
        // Track which groups were previously active, so we can detect when a group becomes active
        var wasActive = spacingGroups.Select(g => g.IsActive).ToList();

        // Deactivate groups by default
        foreach (var group in spacingGroups)
            group.IsActive = false;

        // Find spacing group components
        foreach (var comp in template.GetComponentsInChildren<RacetrackSpacingGroup>())
        {
            if (comp.Index >= 0 && comp.Index < MaxSpacingGroups && comp.Spacing >= segmentLength)
            {
                // Activate corresponding group
                var group = spacingGroups[comp.Index];                
                group.IsActive = true;
                group.SpacingBefore = comp.SpacingBefore;
                group.SpacingAfter = comp.SpacingAfter;

                // Reset Z offset for inactive groups that have become active
                if (!wasActive[comp.Index])
                    group.ZOffset = meshZOffset;
            }
        }
    }

    /// <summary>
    /// Shallow copy a mesh object
    /// </summary>
    public static Mesh CloneMesh(Mesh src)
    {
        // Note: This doesn't copy all fields, just the ones we use
        var dst = new Mesh()
        {
            name = src.name + " clone",
            vertices = src.vertices,
            normals = src.normals,
            tangents = src.tangents,
            uv = src.uv,
            uv2 = src.uv2,
            uv3 = src.uv3,
            uv4 = src.uv4,
            uv5 = src.uv5,
            uv6 = src.uv6,
            uv7 = src.uv7,
            uv8 = src.uv8,
            triangles = src.triangles,
            colors = src.colors,
            colors32 = src.colors32,
            bounds = src.bounds,
            subMeshCount = src.subMeshCount,
            indexFormat = src.indexFormat,
            bindposes = src.bindposes,
            boneWeights = src.boneWeights,
            hideFlags = src.hideFlags
        };
        for (int i = 0; i < src.subMeshCount; i++)
            dst.SetTriangles(src.GetTriangles(i), i);
        return dst;
    }

    /// <summary>
    /// Warp a single mesh along the racetrack curves
    /// </summary>
    /// <param name="mesh">The mesh to warp. Vertex, normal and tangent arrays will be cloned and modified.</param>
    /// <param name="meshZOffset">The distance along all curves where mesh begins</param>
    /// <param name="templateFromMesh">Transformation from mesh space to mesh template space</param>
    /// <param name="meshFromWorld">Transformation from world space to mesh space</param>
    public static void WarpMeshToCurves(
        Mesh mesh,
        RacetrackMeshInfoCache.TemplateInfo templateInfo,
        RacetrackMeshInfoCache.VertexInfo vertexInfo,
        RacetrackPathSection path,
        float zScale,
        Matrix4x4 templateFromMesh,
        Matrix4x4 meshFromWorld,
        Matrix4x4 worldFromTrack,
        RacetrackWidenRanges.Ranges horizontalExtRanges,
        bool isCollisionMesh,
        RacetrackTransformType bankTransform,
        RacetrackUVGenerator[] uvGenerators = null,
        bool removeStartInternalFaces = false,
        bool removeEndInternalFaces = false,
        float internalFaceZThreshold = 0.0f)
    {
        // Remove internal faces if required
        if (removeStartInternalFaces || removeEndInternalFaces)
        {
            var remover = new RacetrackMeshInternalFaceRemover(
                vertexInfo.Vertices,
                templateFromMesh,
                removeStartInternalFaces,
                removeEndInternalFaces,
                templateInfo.MinZ + internalFaceZThreshold,
                templateInfo.MaxZ - internalFaceZThreshold);

            // Filter sub meshes
            for (int i = 0; i < mesh.subMeshCount; i++)
                mesh.SetTriangles(remover.Apply(mesh.GetTriangles(i)), i);
        }

        // Transform vertices into template space
        var vertices = new Vector3[vertexInfo.Vertices.Length];
        for (int i = 0; i < vertexInfo.Vertices.Length; i++)
            vertices[i] = templateFromMesh.MultiplyPoint(vertexInfo.Vertices[i]);

        // Determine which vertices to calculate UVs for.
        Debug.Assert(vertexInfo.Vertices.Length == vertexInfo.UV.Length || vertexInfo.UV.Length == 0);
        var uvIndices = new Dictionary<int, int>();
        if (uvGenerators != null && vertexInfo.UV != null && vertexInfo.UV.Length > 0 && !isCollisionMesh)
        {
            for (int gi = 0; gi < uvGenerators.Length; gi++)
            {
                var uvGenerator = uvGenerators[gi];
                float cosMaxAngle = Mathf.Cos(uvGenerator.MaxAngle * Mathf.Deg2Rad);
                var meshRenderer = uvGenerator.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    // For each material in the UV generator, find the corresponding material 
                    // in the mesh renderer. The index of the material is the index of the corresponding
                    // submesh.
                    int[] submeshes = uvGenerator.Materials.Select(m => RacetrackUtil.FindIndex(meshRenderer.sharedMaterials, m))
                                                              .Where(i => i >= 0)
                                                              .Distinct()
                                                              .ToArray();
                    foreach (var submesh in submeshes)
                    {
                        // Iterate submesh triangles
                        int[] triangles = mesh.GetTriangles(submesh);
                        for (int i = 0; i < triangles.Length - 2; i += 3)
                        {
                            // Find triangle vertices in template space
                            Vector3 v1 = vertices[triangles[i]];
                            Vector3 v2 = vertices[triangles[i + 1]];
                            Vector3 v3 = vertices[triangles[i + 2]];

                            // Calculate triangle surface normal
                            // Check if it is within "MaxAngle" of up vector
                            Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
                            if ((uvGenerator.Side == RacetrackUVGenerationSide.Top    || uvGenerator.Side == RacetrackUVGenerationSide.TopAndBottom) &&  normal.y >= cosMaxAngle
                             || (uvGenerator.Side == RacetrackUVGenerationSide.Bottom || uvGenerator.Side == RacetrackUVGenerationSide.TopAndBottom) && -normal.y >= cosMaxAngle
                             || (uvGenerator.Side == RacetrackUVGenerationSide.Right  || uvGenerator.Side == RacetrackUVGenerationSide.LeftAndRight) &&  normal.x >= cosMaxAngle
                             || (uvGenerator.Side == RacetrackUVGenerationSide.Left   || uvGenerator.Side == RacetrackUVGenerationSide.LeftAndRight) && -normal.x >= cosMaxAngle
                             || (uvGenerator.Side == RacetrackUVGenerationSide.Front  || uvGenerator.Side == RacetrackUVGenerationSide.FrontAndBack) &&  normal.z >= cosMaxAngle
                             || (uvGenerator.Side == RacetrackUVGenerationSide.Back   || uvGenerator.Side == RacetrackUVGenerationSide.FrontAndBack) && -normal.z >= cosMaxAngle)
                            {
                                // Add vertex indices to dictionary. Value is index of generator.
                                uvIndices[triangles[i]] = gi;
                                uvIndices[triangles[i + 1]] = gi;
                                uvIndices[triangles[i + 2]] = gi;
                            }
                        }
                    }
                }
            }
        }

        // Warp vertices around road curve
        Debug.Assert(vertexInfo.Vertices.Length == vertexInfo.Normals.Length);
        var normals = !isCollisionMesh ? new Vector3[vertexInfo.Normals.Length] : null;
        var uv = uvIndices.Any() ? new Vector2[vertexInfo.UV.Length] : null;                      // Replace UVs only if any need to be generated
        for (int i = 0; i < vertexInfo.Vertices.Length; i++)
        {
            Vector3 v = vertices[i];

            // z determines index in meshSegments array
            float z = (v.z - templateInfo.MinZ) * zScale + path.StartZ;                         // Z distance down track
            if (z < 0.0f)
                z = 0.0f;           // Can happen due to floating pt error. Causes first racetrack mesh to start 1 segment along, creating small gaps in junction connections.
            var vertSeg = path.Path.GetSegmentAndOffset(z, out float segZ);

            // Calculate warped position
            Vector3 segPos = new Vector3(v.x, v.y, segZ);                                   // Position in segment space

            // Apply widening
            var segExtension = vertSeg.GetWidening(segZ);
            segPos.x = horizontalExtRanges.Apply(segPos.x, segExtension);

            Matrix4x4 segToTrack = bankTransform == RacetrackTransformType.Rotate
                ? vertSeg.GetSegmentToTrack(segPos.z)
                : vertSeg.GetShearSegmentToTrack(segPos.z);
            Vector3 trackPos = segToTrack.MultiplyPoint(segPos);                            // => Track space
            Vector3 worldPos = worldFromTrack.MultiplyPoint(trackPos);                      // => World space
            vertices[i] = meshFromWorld.MultiplyPoint(worldPos);                            // => Mesh space

            // Generate UVs based on segment position after widening applied
            if (uv != null)
            {
                // Check if vertex UV is generated, and lookup corresponding UV generator
                int gi;
                if (uvIndices.TryGetValue(i, out gi))
                {
                    // Calculate UV based on segment position
                    var uvgenerator = uvGenerators[gi];
                    float texX = 0.0f;
                    switch (uvgenerator.USource)
                    {
                        case RacetrackTexCoordSource.TrackX:
                            texX = segPos.x;
                            break;
                        case RacetrackTexCoordSource.TrackY:
                            texX = segPos.y;
                            break;
                        case RacetrackTexCoordSource.TrackZ:
                            texX = z;
                            break;
                        case RacetrackTexCoordSource.WorldX:
                            texX = trackPos.x;
                            break;
                        case RacetrackTexCoordSource.WorldY:
                            texX = trackPos.y;
                            break;
                        case RacetrackTexCoordSource.WorldZ:
                            texX = trackPos.z;
                            break;
                    }
                    float texY = 0.0f;
                    switch (uvgenerator.VSource)
                    {
                        case RacetrackTexCoordSource.TrackX:
                            texY = segPos.x;
                            break;
                        case RacetrackTexCoordSource.TrackY:
                            texY = segPos.y;
                            break;
                        case RacetrackTexCoordSource.TrackZ:
                            texY = z;
                            break;
                        case RacetrackTexCoordSource.WorldX:
                            texY = trackPos.x;
                            break;
                        case RacetrackTexCoordSource.WorldY:
                            texY = trackPos.y;
                            break;
                        case RacetrackTexCoordSource.WorldZ:
                            texY = trackPos.z;
                            break;
                    }
                    Vector2 t = new Vector2(texX, texY);

                    // Apply rotation   
                    if (uvgenerator.Rotation != 0.0f)
                    {
                        float rad = uvgenerator.Rotation * Mathf.Deg2Rad;
                        float sin = Mathf.Sin(rad);
                        float cos = Mathf.Cos(rad);
                        t = new Vector2(t.y * sin + t.x * cos, t.y * cos - t.x * sin);
                    }

                    // Apply scaling and offset
                    uv[i] = t / uvgenerator.Scale + uvgenerator.Offset;
                }
                else
                    uv[i] = vertexInfo.UV[i];
            }

            // Warp normal
            if (normals != null)
            {
                Vector3 n = templateFromMesh.MultiplyVector(vertexInfo.Normals[i]);
                Vector3 segNorm = n;                                                        // Normal in segment space
                Vector3 trackNorm = segToTrack.MultiplyVector(segNorm);                     // => Track space
                Vector3 worldNorm = worldFromTrack.MultiplyVector(trackNorm);               // => World space
                normals[i] = meshFromWorld.MultiplyVector(worldNorm).normalized;            // => Mesh space
            }
        }

        // Replace mesh vertices and normals
        mesh.vertices = vertices;
        if (normals != null)
            mesh.normals = normals;
        if (uv != null)
            mesh.uv = uv;

        // Recalculate tangents and bounds
        if (!isCollisionMesh)
            mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    private static RacetrackWidenRanges.Ranges GetWidenRangeForMesh(RacetrackMeshTemplate template, RacetrackContinuous subtree, RacetrackContinuous subtreeCopy, Component mesh)
    {
        // Search upwards from mesh to subtree for RacetrackHorizontalExtRanges component
        var rangeComponent = RacetrackUtil.FindEffectiveComponent<RacetrackWidenRanges>(mesh, subtreeCopy);
        if (rangeComponent != null)
        {
            // Calculate range -> template transformation matrix.
            // We actually want this for the original range, rather than the range we've found in the subtree copy.
            // However we know that the range copy->subtree copy matrix is the same as the original range->subtree matrix.
            Matrix4x4 subtreeCopyFromRange = RacetrackUtil.GetAncestorFromDescendentMatrix(subtreeCopy, rangeComponent);
            Matrix4x4 templateFromSubtree = template.GetTemplateFromSubtreeMatrix(subtree);

            // Apply transform to ranges
            return rangeComponent.GetRanges().Transform(templateFromSubtree * subtreeCopyFromRange);
        }

        // Otherwise search from subtree to template
        return GetWidenRangeForSubtree(template, subtree);
    }

    private static RacetrackWidenRanges.Ranges GetWidenRangeForSubtree(RacetrackMeshTemplate template, Component subtree)
    {
        // Search upwards from subtree to template for RacetrackHorizontalExtRanges component
        var rangeComponent = RacetrackUtil.FindEffectiveComponent<RacetrackWidenRanges>(subtree, template);
        if (rangeComponent != null)
        {
            // Calculate range to template transformation
            Matrix4x4 templateFromRange = template.GetTemplateFromSubtreeMatrix(rangeComponent);

            // Apply transform to ranges
            return rangeComponent.GetRanges().Transform(templateFromRange);
        }

        // Default when no explicit RacetrackHorizontalExtRanges found
        return RacetrackWidenRanges.Ranges.zero;
    }

    private static CurveRange GetCurveRange(RacetrackPathSection p)
    {
        return new CurveRange
        {
            StartIndex = p.Path.GetSegmentAtZ(p.StartZ).Curve.Index,
            EndIndex = p.Path.GetSegmentAtZ(p.EndZ).Curve.Index
        };
    }

    private class CurveRange
    {
        public int StartIndex;
        public int EndIndex;
    }
}
