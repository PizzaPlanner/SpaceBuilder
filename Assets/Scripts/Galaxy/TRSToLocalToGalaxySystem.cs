using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
public partial class TRSToLocalToGalaxySystem : SystemBase
{
    EntityQuery tooFarAwayQuery, notTooFarAwayQuery;

    protected override void OnCreate()
    {
        tooFarAwayQuery = SystemAPI.QueryBuilder().WithAll<TooFarAway>().WithNone<Disabled>().Build();
        notTooFarAwayQuery = SystemAPI.QueryBuilder().WithAll<Disabled>()
                                      .WithDisabled<TooFarAway>()
                                      .WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build();
    }

    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(World.Unmanaged);

        ecb.AddComponent<Disabled>(tooFarAwayQuery, EntityQueryCaptureMode.AtPlayback);
        ecb.RemoveComponent<Disabled>(notTooFarAwayQuery, EntityQueryCaptureMode.AtPlayback);
        
        Dependency = new TravelSectorChangeJob
        {
            ecb = ecb
        }.Schedule(Dependency);

        var refFrame = SystemAPI.GetSingleton<ReferenceFrame>();
        var refFramePrevRotInverse = math.inverse(refFrame.PreviousLtw.Value);
        Dependency = new LTGToLocalTransformJob
        {
            refFrame = refFrame,
            refFramePreviousLtwInverse = refFramePrevRotInverse
        }.ScheduleParallel(Dependency);
        
    }
}