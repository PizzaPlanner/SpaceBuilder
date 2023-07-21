using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
public partial struct TRSToLocalToGalaxySystem : ISystem
{
    EntityQuery tooFarAwayQuery, notTooFarAwayQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ReferenceFrame>();
        tooFarAwayQuery = SystemAPI.QueryBuilder().WithAll<TooFarAway>().WithNone<Disabled>().Build();
        notTooFarAwayQuery = SystemAPI.QueryBuilder().WithAll<Disabled>()
                                      .WithDisabled<TooFarAway>()
                                      .WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);

        ecb.AddComponent<Disabled>(tooFarAwayQuery, EntityQueryCaptureMode.AtPlayback);
        ecb.RemoveComponent<Disabled>(notTooFarAwayQuery, EntityQueryCaptureMode.AtPlayback);

        var refFrame = SystemAPI.GetSingleton<ReferenceFrame>();
        var refFramePrevRotInverse = math.inverse(refFrame.PreviousLtw.Value);
        new LTGToLocalTransformJob
        {
            refFrame = refFrame,
            refFramePreviousLtwInverse = refFramePrevRotInverse
        }.ScheduleParallel();
    }
}