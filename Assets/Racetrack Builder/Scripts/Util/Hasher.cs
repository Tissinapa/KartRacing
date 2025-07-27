using UnityEngine;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit)]
struct NumericTypeUnion
{
    [FieldOffset(0)]
    public float floatValue;

    [FieldOffset(0)]
    public int intValue;

    [FieldOffset(0)] public byte b0;
    [FieldOffset(1)] public byte b1;
    [FieldOffset(2)] public byte b2;
    [FieldOffset(3)] public byte b3;
}

public interface IHasher 
{
    int Hash { get; }
    HashMethod HashMethod { get; }
    IHasher Int(int i);
    IHasher Object(object o);
    IHasher Float(float f);
    IHasher Bool(bool b);
    IHasher RoundedFloat(float f);
    IHasher Vector3(UnityEngine.Vector3 v);
    IHasher RoundedMatrix4x4(Matrix4x4 m);
}

/// <summary>
/// Helper class for calculating hashes.
/// Uses MD5 internally.
/// </summary>
public class MD5Hasher : IHasher
{
    private readonly HashAlgorithm alg = MD5.Create();
    private List<byte> bytes = new List<byte>();

    public MD5Hasher(HashMethod hashMethod)
    {
        this.HashMethod = hashMethod;
    }

    public int Hash { 
        get
        {
            // Compute hash
            var hashedBytes = alg.ComputeHash(bytes.ToArray());

            // XOR down into a single int
            var union = new NumericTypeUnion();
            int hash = 0;
            for (int i = 0; i < hashedBytes.Length; i += 4)
            {                
                union.b0 = i + 0 < hashedBytes.Length ? hashedBytes[i + 0] : (byte)0;
                union.b1 = i + 1 < hashedBytes.Length ? hashedBytes[i + 1] : (byte)0;
                union.b2 = i + 2 < hashedBytes.Length ? hashedBytes[i + 2] : (byte)0;
                union.b3 = i + 3 < hashedBytes.Length ? hashedBytes[i + 3] : (byte)0;                
                hash ^= union.intValue;
            }

            return hash;
        } 
    }

    public HashMethod HashMethod { get; private set; }

    public IHasher Int(int i)
    {
        return AddNumeric(new NumericTypeUnion { intValue = i });
    }

    public IHasher Object(object o)
    {
        return Int(o != null ? o.GetHashCode() : 0);
    }

    public IHasher Float(float f)
    {
        return AddNumeric(new NumericTypeUnion { floatValue = f });
    }

    public IHasher Bool(bool b)
    {
        bytes.Add(b ? (byte)1 : (byte)0);
        return this;
    }

    public IHasher RoundedFloat(float f)
    {
        return Float(RacetrackUtil.RoundedFloat(f));
    }

    public IHasher Vector3(UnityEngine.Vector3 v)
    {
        return RoundedFloat(v.x).RoundedFloat(v.y).RoundedFloat(v.z);
    }

    public IHasher RoundedMatrix4x4(Matrix4x4 m)
    {
        for (int i = 0; i < 16; i++)
            RoundedFloat(m[i]);
        return this;
    }

    private IHasher AddNumeric(NumericTypeUnion union)
    {
        bytes.Add(union.b0);
        bytes.Add(union.b1);
        bytes.Add(union.b2);
        bytes.Add(union.b3);
        return this;
    }
}

/// <summary>
/// Old simple hasher. 
/// </summary>
/// <remarks>
/// Intended for scenes from older Racetrack Builder versions that have a RacetrackMeshManager.
/// Ensures hashes are calculated as the older version would have calculated them.
/// </remarks>
public class SimpleHasher : IHasher
{
    public int Hash { get; private set; } = 17;

    public HashMethod HashMethod 
    { 
        get { return HashMethod.Simple; } 
    }

    public IHasher Int(int value)
    {
        unchecked // Don't error on overflow
        {
            Hash = Hash * 23 + value;
        }

        return this;
    }

    public IHasher Object(object o)
    {
        return Int(o != null ? o.GetHashCode() : 0);
    }

    public IHasher Float(float f)
    {
        // Note: If just using Int(f.GetHashCode()) then negated sequences can produce the same hash as their non negated version.
        // E.g. Float(-9.0f).Float(-0.2f) == Float(9.0f).Float(0.2f) !

        // Thus hash negatives differently
        if (f < 0.0f)
        {
            Int(7607);              // (Prime #)
            f = -f;                 // Hash positive value
        }

        return Int(f.GetHashCode());
    }

    public IHasher Bool(bool b)
    {
        return Int(b.GetHashCode());
    }

    public IHasher RoundedFloat(float f)
    {
        return Float(RacetrackUtil.RoundedFloat(f));
    }

    public IHasher Vector3(UnityEngine.Vector3 v)
    {
        return RoundedFloat(v.x).RoundedFloat(v.y).RoundedFloat(v.z);
    }

    public IHasher RoundedMatrix4x4(Matrix4x4 m)
    {
        for (int i = 0; i < 16; i++)
            RoundedFloat(m[i]);
        return this;
    }
}

public enum HashMethod
{
    // Old method used in previous Racetrack Builder releases
    Simple = 0,

    // MD5 hash
    MD5 = 1,

    // MD5 hash with RacetrackPath.CalcHash gap bug-fix
    MD5WithGapFix = 2    
}
