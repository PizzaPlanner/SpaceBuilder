using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct TravellerInSector : IBufferElementData
{
    public Entity Player;
}

public struct LocalToGalaxy : IComponentData
{
    [SerializeField] long bitsX, bitsY, bitsZ;
    [SerializeField] double3 _localPosition;
    
    public static LocalToGalaxy Center => new LocalToGalaxy
    {
        LightSector = 0,
        StarSector = MAX_STARSECTOR / 2,
        PlanetSector = MAX_PLANETSECTOR / 2,
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

    const int LOCALPOSITION_BITS = 16;
    public const int MAX_POSITION = 1 << LOCALPOSITION_BITS;

    const int PLANETSECTOR_BITS = 12;
    const int PLANETSECTOR_SHIFT = 0;
    const long PLANETSECTOR_MASK = (MAX_PLANETSECTOR - 1L) << PLANETSECTOR_SHIFT;
    public const short MAX_PLANETSECTOR = 1 << PLANETSECTOR_BITS;
    
    const int STARSECTOR_BITS = 12;
    const int STARSECTOR_SHIFT = PLANETSECTOR_BITS + PLANETSECTOR_SHIFT;
    const long STARSECTOR_MASK = (MAX_STARSECTOR - 1L) << STARSECTOR_SHIFT;
    public const short MAX_STARSECTOR = 1 << STARSECTOR_BITS;
    
    const int LIGHTSECTOR_BITS = 39;
    const int LIGHTSECTOR_SHIFT = STARSECTOR_BITS + STARSECTOR_SHIFT;
    const long LIGHTSECTOR_MASK = (MAX_LIGHTSECTOR - 1L) << LIGHTSECTOR_SHIFT;
    public const long MAX_LIGHTSECTOR = 1L << LIGHTSECTOR_BITS;

    const int MAX_MEASURABLE_SECTOR_BITS = 39;
    public const double MAX_MEASURABLE_DISTANCE = (1L << MAX_MEASURABLE_SECTOR_BITS) * DISTANCE_PER_PLANETSECTOR - 1;
    const double MAX_MEASURABLE_LY = MAX_MEASURABLE_DISTANCE / DISTANCE_LIGHTYEAR;
    
    public const double DISTANCE_PER_LIGHTSECTOR = DISTANCE_PER_STARSECTOR * MAX_STARSECTOR;
    public const double DISTANCE_PER_STARSECTOR = DISTANCE_PER_PLANETSECTOR * MAX_PLANETSECTOR;
    public const double DISTANCE_PER_PLANETSECTOR = MAX_POSITION;
    
    public const float DISTANCE_PARSEC = 3.0857e+16f;
    public const float DISTANCE_LIGHTYEAR = 9_460_730_472_580_800;
    public const float DISTANCE_ASTRONOMICAL_UNIT = 149_597_870_700;

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
                _localPosition + bitsAsDouble * DISTANCE_PER_PLANETSECTOR,
                DistanceMeasureable); 
        }
    } 

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalToGalaxy operator +(LocalToGalaxy lhs, LocalToGalaxy rhs)
    {
        var result = new LocalToGalaxy
        {
            LightSector = lhs.LightSector + rhs.LightSector,
            StarSector = lhs.StarSector + rhs.StarSector,
            PlanetSector = lhs.PlanetSector + rhs.PlanetSector,
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
            LightSector = lhs.LightSector - rhs.LightSector,
            StarSector = lhs.StarSector - rhs.StarSector,
            PlanetSector = lhs.PlanetSector - rhs.PlanetSector,
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
        return
               math.all(l1.LightSector == l2.LightSector) && 
               math.all(l1.PlanetSector == l2.PlanetSector) &&
               math.all(l1.StarSector == l2.StarSector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile]
    public static bool operator !=(LocalToGalaxy l1, LocalToGalaxy l2)
    {
        return 
               math.any(l1.StarSector != l2.StarSector) ||
               math.any(l1.PlanetSector != l2.PlanetSector) ||
               math.any(l1.LightSector != l2.LightSector);
    }

    public override string ToString()
    {
        return $"({LightSector.x:X}:{StarSector.x:X}:{PlanetSector.x:X}:{LocalPosition.x:F0}, {LightSector.y:X}:{StarSector.y:X}:{PlanetSector.y:X}:{LocalPosition.y:F0}, {LightSector.z:X}:{StarSector.z:X}:{PlanetSector.z:X}:{LocalPosition.z:F0})";
    }

    [BurstDiscard]
    public string DebugX => $"({bitsX:X}:{LocalPosition.x:F0})";
}