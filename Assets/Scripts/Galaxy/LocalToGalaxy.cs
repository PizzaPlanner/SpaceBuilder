using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct LocalToGalaxy : IComponentData, IEquatable<LocalToGalaxy>, IComparable<LocalToGalaxy>
{
    [SerializeField] long bitsX, bitsY, bitsZ;
    [SerializeField] double3 _localPosition;
    
    public static LocalToGalaxy Center => new LocalToGalaxy
    {
        LightSector = 0,
        StarSector = MAX_STARSECTOR / 2,
        PlanetSector = MAX_PLANETSECTOR / 2,
        LocalSector = MAX_LOCALSECTOR / 2,
        LocalPosition = MAX_POSITION / 2,
    };

    public int3 LightSector
    {
        get => GetFromMaskAndShift(LIGHTSECTOR_MASK, LIGHTSECTOR_SHIFT);
        set => OverwriteAndAddRemainder(value, LIGHTSECTOR_MASK, LIGHTSECTOR_SHIFT);
    }
    
    public int3 StarSector
    {
        get => GetFromMaskAndShift(STARSECTOR_MASK, STARSECTOR_SHIFT);
        set => OverwriteAndAddRemainder(value, STARSECTOR_MASK, STARSECTOR_SHIFT);
    }

    public int3 PlanetSector
    {
        get => GetFromMaskAndShift(PLANETSECTOR_MASK, PLANETSECTOR_SHIFT);
        set => OverwriteAndAddRemainder(value, PLANETSECTOR_MASK, PLANETSECTOR_SHIFT);
    }
    
    public int3 LocalSector
    {
        get => GetFromMaskAndShift(LOCALSECTOR_MASK, LOCALSECTOR_SHIFT);
        set => OverwriteAndAddRemainder(value, LOCALSECTOR_MASK, LOCALSECTOR_SHIFT);
    }

    int3 GetFromMaskAndShift(long mask, int shift)
    {
        return new int3(
            (int)((bitsX & mask) >> shift),
            (int)((bitsY & mask) >> shift),
            (int)((bitsZ & mask) >> shift));
    }

    void OverwriteAndAddRemainder(int3 value, long mask, int shift)
    {
        var cutoutX = bitsX & ~mask;
        var cutoutY = bitsY & ~mask;
        var cutoutZ = bitsZ & ~mask;
        bitsX = cutoutX + ((long)value.x << shift);
        bitsY = cutoutY + ((long)value.y << shift);
        bitsZ = cutoutZ + ((long)value.z << shift);
    }

    public double3 LocalPosition
    {
        get => _localPosition;
        set
        {
            var floor = math.floor(value / MAX_POSITION);
            _localPosition = (value % MAX_POSITION + MAX_POSITION) % MAX_POSITION;
            bitsX += (long)floor.x;
            bitsY += (long)floor.y;
            bitsZ += (long)floor.z;
        }
    }

    public bool3 DistanceMeasureable => math.abs(new double3(
        bitsX,
        bitsY,
        bitsZ
    )) <= 1L << MAX_MEASURABLE_SECTOR_BITS;
    
    public double3 Distance
    {
        get
        {
            var bitsAsDouble = new double3(
                bitsX,
                bitsY,
                bitsZ
            );
            return math.select(double.PositiveInfinity,
                _localPosition + bitsAsDouble * MAX_POSITION,
                DistanceMeasureable); 
        }
    } 

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalToGalaxy operator +(LocalToGalaxy lhs, LocalToGalaxy rhs)
    {
        var result = new LocalToGalaxy
        {
            bitsX = lhs.bitsX + rhs.bitsX,
            bitsY = lhs.bitsY + rhs.bitsY,
            bitsZ = lhs.bitsZ + rhs.bitsZ,
            LocalPosition = lhs.LocalPosition + rhs.LocalPosition,
        };
        return result;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalToGalaxy operator +(LocalToGalaxy lhs, double3 rhs)
    {
        var result = lhs;
        result.LocalPosition += rhs;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalToGalaxy operator -(LocalToGalaxy lhs, LocalToGalaxy rhs)
    {
        var result = new LocalToGalaxy
        {
            bitsX = lhs.bitsX - rhs.bitsX,
            bitsY = lhs.bitsY - rhs.bitsY,
            bitsZ = lhs.bitsZ - rhs.bitsZ,
            LocalPosition = lhs.LocalPosition - rhs.LocalPosition,
        };
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalToGalaxy operator -(LocalToGalaxy lhs, double3 rhs)
    {
        var result = lhs;
        result.LocalPosition -= rhs;
        return result;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(LocalToGalaxy l1, LocalToGalaxy l2)
    {
        return l1.bitsX == l2.bitsX && l1.bitsY == l2.bitsY && l1.bitsZ == l2.bitsZ;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile]
    public static bool operator !=(LocalToGalaxy l1, LocalToGalaxy l2)
    {
        return l1.bitsX != l2.bitsX || l1.bitsY != l2.bitsY || l1.bitsZ != l2.bitsZ;
    }

    public override string ToString()
    {
        return $"({LightSector.x:X}:{StarSector.x:X}:{PlanetSector.x:X}:{LocalPosition.x:F0}, {LightSector.y:X}:{StarSector.y:X}:{PlanetSector.y:X}:{LocalPosition.y:F0}, {LightSector.z:X}:{StarSector.z:X}:{PlanetSector.z:X}:{LocalPosition.z:F0})";
    }

    [BurstDiscard]
    public string DebugX => $"({bitsX:X}:{LocalPosition.x:F0})";

    public bool Equals(LocalToGalaxy other)
    {
        return bitsX == other.bitsX && bitsY == other.bitsY && bitsZ == other.bitsZ;
    }

    public override bool Equals(object obj)
    {
        return obj is LocalToGalaxy other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(bitsX, bitsY, bitsZ);
    }

    public int CompareTo(LocalToGalaxy other)
    {
        var bitsXComparison = bitsX.CompareTo(other.bitsX);
        if (bitsXComparison != 0) return bitsXComparison;
        var bitsYComparison = bitsY.CompareTo(other.bitsY);
        if (bitsYComparison != 0) return bitsYComparison;
        return bitsZ.CompareTo(other.bitsZ);
    }
}