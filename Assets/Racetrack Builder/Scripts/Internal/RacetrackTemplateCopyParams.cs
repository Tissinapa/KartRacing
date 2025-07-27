using System;
using System.Linq;
/// <summary>
/// Captures all information required to generate a RacetrackMeshTemplateCopy
/// </summary>
public class RacetrackTemplateCopyParams
{
    public RacetrackCurve Curve;
    public RacetrackMeshTemplate Template;
    public RacetrackMeshInfoCache.TemplateInfo TemplateInfo;
    public RacetrackPathSection PathSection;
    public RacetrackSpacingGroupState[] SpacingGroupStates;
    public bool RemoveStartFaces;
    public bool RemoveEndFaces;
    public float ZScale;

    /// <summary>
    /// Calculate a hash of all the state that affects the shape of the generated mesh template copy,
    /// except for: 
    ///     * The Template reference
    ///     * The spacing group state
    /// </summary>
    /// <param name="hash">Hashing helper object</param>
    public int CalcHash(IHasher hash)
    {
        hash.RoundedFloat(ZScale)
            .Bool(RemoveStartFaces)
            .Bool(RemoveEndFaces);
        PathSection.CalcHash(hash);

        return hash.Hash;
    }

    /// <summary>
    /// Calculate a hash of the state that affects how spaced objects will be generated
    /// </summary>
    /// <param name="hash">Hashing helper object</param>
    public int CalcSpacingGroupsHash(IHasher hash)
    {
        foreach (var s in SpacingGroupStates)
        {
            if (!s.IsActive)
                hash.Int(0);
            else
                hash.Float(s.SpacingBefore)
                    .Float(s.SpacingAfter)
                    .RoundedFloat(s.ZOffset - PathSection.StartZ);
        }

        return hash.Hash;
    }
}

public class RacetrackSpacingGroupState
{
    public bool IsActive = false;
    public float ZOffset;
    public float SpacingBefore;
    public float SpacingAfter;

    public float Spacing {
        get { return SpacingBefore + SpacingAfter; }
    }

    public RacetrackSpacingGroupState Clone() {
        return new RacetrackSpacingGroupState {
            IsActive = IsActive,
            ZOffset = ZOffset,
            SpacingBefore = SpacingBefore,
            SpacingAfter = SpacingAfter
        };
    }
}
