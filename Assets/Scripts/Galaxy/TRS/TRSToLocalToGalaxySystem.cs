using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(LocalToWorldSystem))]
public partial struct TRSToLocalToGalaxySystem : ISystem
{
    EntityQuery tooFarAwayQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ReferenceFrame>();
        state.RequireForUpdate<ReferenceFrameOrigin>();
        tooFarAwayQuery = SystemAPI.QueryBuilder().WithAll<TooFarAway>().Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);

        ecb.DestroyEntity(tooFarAwayQuery, EntityQueryCaptureMode.AtPlayback);

        var refFrame = SystemAPI.GetSingleton<ReferenceFrame>();
        var refFramePrevRotInverse = math.inverse(refFrame.PreviousLtw.Value);
        new LTGToLocalTransformJob
        {
            refFrame = refFrame,
            refFramePreviousLtwInverse = refFramePrevRotInverse
        }.ScheduleParallel();
    }
}