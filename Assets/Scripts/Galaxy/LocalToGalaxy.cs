using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct TravellerInSector : IBufferElementData
{
    public Entity Player;
}

public static class Constants
{
    public const long MAX_POSITION = 0x2A00; // Must satisfy MAX_POSITION * MAX_POSITION < int.MaxValue
    public const long MAX_SUBSECTOR = 0x0003_FFFF; // Must satisfy MAX_SUBSECTOR * MAX_SUBSECTOR < int.MaxValue
    public const long DISTANCE_PER_SECTOR = MAX_POSITION * MAX_SUBSECTOR;
    // private const ulong BYTE_MASK = 0x0000_0000_0000_0000;
    // private const ulong POST_COMMA_MASK = 0x0000_0000_0000_3FFF;
    // private const ulong POSITION_MASK = 0x0000_0000_01FF_C000;
    // private const ulong SUBSECTOR_MASK = 0x0000_0FFF_FE00_0000;
    // private const ulong SECTOR_MASK = 0xFFFF_F000_0000_0000;
    // private const ulong POST_COMMA_MAX = POST_COMMA_MASK >> 0;
    // private const ulong POSITION_MAX = POSITION_MASK >> 12;
    // private const ulong SUBSECTOR_MAX = SUBSECTOR_MASK >> 24;
    // private const ulong SECTOR_MAX = SECTOR_MASK >> 44;

}

// public struct LocalToGalaxyComparer : IComparer<LocalToGalaxy>
// {
//     public float3 forward;
//     public int Compare(LocalToGalaxy x, LocalToGalaxy y)
//     {
//         return math.all(x.Sector == y.Sector) ?
//             math.acos(math.dot(forward, math.normalize(x.Sector))).CompareTo(math.acos(math.dot(forward, math.normalize(y.Sector)))) :
//             math.acos(math.dot(forward, math.normalize(x.SubSector))).CompareTo(math.acos(math.dot(forward, math.normalize(y.SubSector))));
//     }
// }

[GenerateAuthoringComponent]
public struct LocalToGalaxy : IComponentData
{
    // private fixed byte BytesX[10];
    // private fixed byte BytesY[10];
    // private fixed byte BytesZ[10];
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

public struct SectorChange : IComponentData
{
    public Entity Entity;
    public LocalToGalaxy Previous;
    public LocalToGalaxy New;
}

public struct CanTravelSectors : IComponentData
{
    public LocalToGalaxy PreviousSector;
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
[UpdateBefore(typeof(TRSToLocalToWorldSystem))]
[UpdateBefore(typeof(TRSToLocalToParentSystem))]
public partial class TRSToLocalToGalaxySystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        var ecb = bufferSystem.CreateCommandBuffer();

        Entities
            .WithNone<Parent>()
            .ForEach((Entity e, ref LocalToGalaxy ltg, ref CanTravelSectors cts) =>
            {
                if (ltg != cts.PreviousSector)
                {
                    var update = ecb.CreateEntity();
                    ecb.AddComponent(update, new SectorChange{Entity = e, Previous = cts.PreviousSector, New = ltg});
                }

                cts.PreviousSector = ltg;
            }).Run();

        var playerPosition = GetComponent<LocalToGalaxy>(GetSingletonEntity<LocalPlayer>());
        Entities
            .WithNone<LocalPlayer>()
            .WithNone<Parent>()
            .ForEach((Entity e, ref Translation t, in LocalToGalaxy ltg) =>
            {
                t.Value = (float3)LocalToGalaxy.RelativePosition(playerPosition, ltg);
            }).ScheduleParallel();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
