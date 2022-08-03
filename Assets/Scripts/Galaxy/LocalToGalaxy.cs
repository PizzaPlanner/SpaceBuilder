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
    public const int MAX_POSITION = 0x2000;
    public const int MAX_SUBSECTOR = 0x0FFF_FFFF;
}

[GenerateAuthoringComponent]
public struct LocalToGalaxy : IComponentData
{
    public int3 Sector;
    public int3 SubSector;
    public float3 LocalPosition;

    public static double3 RelativePosition(LocalToGalaxy origin, LocalToGalaxy target)
    {
        var diff = target - origin;
        return (double3)diff.LocalPosition +
               diff.SubSector * Constants.MAX_POSITION +
               diff.Sector * Constants.MAX_SUBSECTOR;
    }

    public static LocalToGalaxy OffsetBy(LocalToGalaxy origin, double3 offset)
    {
        var totalMoveSectors = (int3)(offset / Constants.MAX_POSITION / Constants.MAX_SUBSECTOR);
        offset -= totalMoveSectors * Constants.MAX_POSITION * Constants.MAX_SUBSECTOR;

        var totalMoveSubSectors = (int3)(offset / Constants.MAX_POSITION);
        offset -= totalMoveSubSectors * Constants.MAX_POSITION;

        var totalLocal = (float3)offset;
        return origin + new LocalToGalaxy
            {Sector = totalMoveSectors, SubSector = totalMoveSubSectors, LocalPosition = totalLocal};
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalToGalaxy operator +(LocalToGalaxy lhs, LocalToGalaxy rhs)
    {
        var sum = new LocalToGalaxy()
        {
            Sector = lhs.Sector + rhs.Sector,
            SubSector = lhs.SubSector + rhs.SubSector,
            LocalPosition = lhs.LocalPosition + rhs.LocalPosition
        };
        var overflowSubSector = ((int3) sum.LocalPosition + Constants.MAX_POSITION) /
            Constants.MAX_POSITION - 1;
        var overflow = new LocalToGalaxy()
        {
            Sector = (sum.SubSector + overflowSubSector + Constants.MAX_SUBSECTOR) /
                Constants.MAX_SUBSECTOR - 1,
            SubSector = overflowSubSector
        };
        var correction = new LocalToGalaxy()
        {
            Sector = sum.Sector + overflow.Sector,
            SubSector = GridTools.mod(sum.SubSector + overflow.SubSector, Constants.MAX_SUBSECTOR),
            LocalPosition = GridTools.mod(sum.LocalPosition, Constants.MAX_POSITION)
        };
        return correction;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalToGalaxy operator -(LocalToGalaxy lhs, LocalToGalaxy rhs)
    {
        var sum = new LocalToGalaxy()
        {
            Sector = lhs.Sector - rhs.Sector,
            SubSector = lhs.SubSector - rhs.SubSector,
            LocalPosition = lhs.LocalPosition - rhs.LocalPosition
        };
        var overflowSubSector = ((int3) sum.LocalPosition - Constants.MAX_POSITION) /
            Constants.MAX_POSITION + 1;
        var overflow = new LocalToGalaxy()
        {
            Sector = (sum.SubSector + overflowSubSector - Constants.MAX_SUBSECTOR) /
                Constants.MAX_SUBSECTOR + 1,
            SubSector = overflowSubSector
        };
        var correction = new LocalToGalaxy()
        {
            Sector = sum.Sector + overflow.Sector,
            SubSector = GridTools.mod(sum.SubSector + overflow.SubSector, Constants.MAX_SUBSECTOR),
            LocalPosition = GridTools.mod(sum.LocalPosition, Constants.MAX_POSITION)
        };
        return correction;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalToGalaxy operator -(LocalToGalaxy rhs)
    {
        rhs.Sector = -rhs.Sector;
        rhs.SubSector = Constants.MAX_SUBSECTOR - rhs.SubSector;
        rhs.LocalPosition = Constants.MAX_POSITION - rhs.LocalPosition;
        return rhs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(LocalToGalaxy l1, LocalToGalaxy l2)
    {
        return math.all(l1.Sector == l2.SubSector) && math.all(l1.SubSector == l2.SubSector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(LocalToGalaxy l1, LocalToGalaxy l2)
    {
        return math.any(l1.Sector != l2.SubSector) || math.any(l1.SubSector != l2.SubSector);
    }
}

public struct SectorChange : IComponentData
{
    public Entity Entity;
    public LocalToGalaxy Previous;
    public LocalToGalaxy New;
}

public struct CanTravelSectors : IComponentData { }

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
            .WithAll<CanTravelSectors>()
            .WithNone<Parent>()
            .ForEach((Entity e, ref LocalToGalaxy ltg, ref Translation t) =>
            {
                var oldLtg = ltg;
                ltg = LocalToGalaxy.OffsetBy(ltg, t.Value);

                if (ltg != oldLtg)
                {
                    var update = ecb.CreateEntity();
                    ecb.AddComponent(update, new SectorChange{Entity = e, Previous = oldLtg, New = ltg});
                }
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
