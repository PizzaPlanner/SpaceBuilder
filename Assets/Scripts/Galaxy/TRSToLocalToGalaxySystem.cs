using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
public partial class TRSToLocalToGalaxySystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(World.Unmanaged);

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

        var refFrame = SystemAPI.GetSingleton<ReferenceFrame>();
        var refFramePrevRotInverse = math.inverse(refFrame.PreviousLtw.Value);
        Entities
            .WithNone<ReferenceFrameOrigin>()
            .WithNone<Parent>()
            .WithNone<Docked>()
            .ForEach((Entity e, ref LocalTransform transform, in LocalToGalaxy ltg) =>
            {
                var ltgDiff = (ltg - refFrame.PreviousLtg).Distance;
                var relativeRotation = math.mul(new quaternion(refFramePrevRotInverse), transform.Rotation);

                transform.Position = (float3)ltgDiff;
                transform.Rotation = math.mul(relativeRotation, refFrame.CurrentLtw.Rotation);
            }).ScheduleParallel();
    }
}