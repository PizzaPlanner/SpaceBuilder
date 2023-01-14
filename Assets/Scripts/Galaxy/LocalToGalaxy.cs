using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct TravellerInSector : IBufferElementData
{
    public Entity Player;
}

public static class Constants
{
    public const long MAX_POSITION = 0x2A00; // Must satisfy MAX_POSITION * MAX_POSITION < int.MaxValue
    public const long MAX_SUBSECTOR = 0x0003_FFFF; // Must satisfy MAX_SUBSECTOR * MAX_SUBSECTOR < int.MaxValue
    public const long DISTANCE_PER_SECTOR = MAX_POSITION * MAX_SUBSECTOR;
    public const float DISTANCE_PARSEC = 3.0857e+16f;
    public const float DISTANCE_LIGHTYEAR = 9_460_730_472_580_800;
    public const float DISTANCE_ASTRONOMICAL_UNIT = 149_597_870_700;
}

// private const ulong MASK_SECTOR    = 0xFFFF_FFFC_0000_0000UL;
// private const int SHIFT_SECTOR = 35;
// private const ulong MASK_SUBSECTOR = 0x0000_0003_FFFF_0000UL;
// private const int SHIFT_SUBSECTOR = 16;
//
//
// private ulong X, Y, Z;
// public uint3 Sector
// {
//     get => new uint3((uint) (X & 0xFFFF_FFFF_0000_0000u), (uint) (Y & 0xFFFF_FFFF_0000_0000u), (uint) (Z & 0xFFFF_FFFF_0000_0000u));
//     set
//     {
//         X = (X & SHIFT_SUBSECTOR) + ((ulong)value.x << SHIFT_SECTOR);
//         Y = (Y & SHIFT_SUBSECTOR) + ((ulong)value.y << SHIFT_SECTOR);
//         Z = (Z & SHIFT_SUBSECTOR) + ((ulong)value.z << SHIFT_SECTOR);
//     }
// }
// public uint3 SubSector
// {
//     get => new uint3((uint) (X & 0xFFFF_FFFF), (uint) (Y & 0xFFFF_FFFF), (uint) (Z & 0xFFFF_FFFF));
//     set
//     {
//         X = (X & MASK_SECTOR) + ((ulong)value.x << SHIFT_SUBSECTOR);
//         Y = (Y & MASK_SECTOR) + ((ulong)value.y << SHIFT_SUBSECTOR);
//         Z = (Z & MASK_SECTOR) + ((ulong)value.z << SHIFT_SUBSECTOR);
//     }
// }

public struct LocalToGalaxy : IComponentData
{
    public int3 Sector;
    public int3 SubSector;
    public float3 LocalPosition;

    public static double3 RelativePosition(LocalToGalaxy origin, LocalToGalaxy target)
    {
        var diff = target - origin;
        return (double3)diff.LocalPosition +
               (double3)diff.SubSector * Constants.MAX_POSITION +
               (double3)diff.Sector * Constants.DISTANCE_PER_SECTOR;
    }

    public static LocalToGalaxy OffsetBy(LocalToGalaxy origin, double3 offset)
    {
        var totalMoveSectors = (int3)(offset / Constants.DISTANCE_PER_SECTOR);
        offset %= Constants.DISTANCE_PER_SECTOR;

        var totalMoveSubSectors = (int3)(offset / Constants.MAX_POSITION);
        offset %= Constants.MAX_POSITION;

        var totalLocal = (float3)offset;
        var result = origin + new LocalToGalaxy
            {Sector = totalMoveSectors, SubSector = totalMoveSubSectors, LocalPosition = totalLocal};
        // Debug.Log($"({origin.Sector.x:X}:{origin.SubSector.x:X}, {origin.Sector.y:X}:{origin.SubSector.y:X}, {origin.Sector.y:X}:{origin.SubSector.y:X})@{origin.LocalPosition} + {offset} = ({result.Sector.x:X}:{result.SubSector.x:X}, {result.Sector.y:X}:{result.SubSector.y:X}, {result.Sector.z:X}:{result.SubSector.z:X})@{result.LocalPosition}");
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile]
    public static LocalToGalaxy Corrected(LocalToGalaxy ltg)
    {
        var overflowSubSector = (int3) math.floor(ltg.LocalPosition / (int) Constants.MAX_POSITION);
        // var overflowLocalPos = (double3)ltg.LocalPosition + math.sign(overflowSubSector) * (int) Constants.MAX_POSITION;
        var overflow = new LocalToGalaxy()
        {
            Sector = (ltg.SubSector + overflowSubSector + (int)Constants.MAX_SUBSECTOR) /
                (int)Constants.MAX_SUBSECTOR - 1,
            SubSector = overflowSubSector
        };
        var correction = new LocalToGalaxy()
        {
            Sector = ltg.Sector + overflow.Sector,
            SubSector = GridTools.mod(ltg.SubSector + overflow.SubSector, (int)Constants.MAX_SUBSECTOR),
            LocalPosition = (float3)GridTools.mod((double3)ltg.LocalPosition, (int)Constants.MAX_POSITION)
        };
        return correction;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile]
    public static LocalToGalaxy operator +(LocalToGalaxy lhs, LocalToGalaxy rhs)
    {
        var sum = new LocalToGalaxy()
        {
            Sector = lhs.Sector + rhs.Sector,
            SubSector = lhs.SubSector + rhs.SubSector,
            LocalPosition = lhs.LocalPosition + rhs.LocalPosition
        };
        return Corrected(sum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile]
    public static LocalToGalaxy operator -(LocalToGalaxy lhs, LocalToGalaxy rhs)
    {
        var sum = new LocalToGalaxy()
        {
            Sector = lhs.Sector - rhs.Sector,
            SubSector = lhs.SubSector - rhs.SubSector,
            LocalPosition = lhs.LocalPosition - rhs.LocalPosition
        };
        return Corrected(sum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile]
    public static bool operator ==(LocalToGalaxy l1, LocalToGalaxy l2)
    {
        return math.all(l1.Sector == l2.Sector) && math.all(l1.SubSector == l2.SubSector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile]
    public static bool operator !=(LocalToGalaxy l1, LocalToGalaxy l2)
    {
        return math.any(l1.Sector != l2.Sector) || math.any(l1.SubSector != l2.SubSector);
    }

    public override string ToString()
    {
        return $"({Sector.x:X}:{SubSector.x:X}, {Sector.y:X}:{SubSector.y:X}, {Sector.z:X}:{SubSector.z:X}) @ ({LocalPosition:F0})";
    }
}

public enum SectorChangeType
{
    Normal,
    FTL,
    Teleport
}

public struct SectorChange : IComponentData
{
    public Entity Entity;
    public LocalToGalaxy Previous;
    public LocalToGalaxy New;
    public SectorChangeType ChangeType;
}

public struct CanTravelSectors : IComponentData
{
    public LocalToGalaxy PreviousSector;
    public SectorChangeType CurrentTravelType;
    public double CurrentSpeed;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class MakeTravellerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithNone<CanTravelSectors>()
            .WithAny<Player, Flyable>()
            .WithAll<LocalToGalaxy>()
            .ForEach((Entity e) =>
            {
                EntityManager.AddComponent<CanTravelSectors>(e);
            }).WithoutBurst().WithStructuralChanges().Run();
    }
}

[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
public partial class TRSToLocalToGalaxySystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        Entities
            .WithNone<Parent>()
            .ForEach((Entity e, ref LocalToGalaxy ltg, ref CanTravelSectors cts) =>
            {
                if (ltg != cts.PreviousSector && cts.CurrentTravelType != SectorChangeType.FTL)
                {
                    var update = ecb.CreateEntity();
                    ecb.AddComponent(update, new SectorChange
                    {
                        Entity = e,
                        Previous = cts.PreviousSector,
                        New = ltg,
                        ChangeType = cts.CurrentTravelType
                    });
                }

                cts.PreviousSector = ltg;
            }).Run();

        var refFrame = GetSingleton<ReferenceFrame>();
        var refFramePrevRotInverse = math.inverse(refFrame.PreviousLtw.Value);
        Entities
            .WithNone<ReferenceFrameOrigin>()
            .WithNone<Parent>()
            .WithNone<Docked>()
            .ForEach((Entity e, ref WorldTransform transform, in LocalToGalaxy ltg) =>
            {
                var ltgDiff = LocalToGalaxy.RelativePosition(refFrame.PreviousLtg, ltg);
                var relativeRotation = math.mul(new quaternion(refFramePrevRotInverse), transform.Rotation);
                var localTRS = float4x4.TRS((float3)ltgDiff, relativeRotation, 1);
                
                transform.Position = (float3)ltgDiff;
                transform.Rotation = math.mul(relativeRotation, refFrame.CurrentLtw.Rotation);
            }).ScheduleParallel();
    }
}
