using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct UnloadCandidate : IComponentData
{
    public float Timeout;
}

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class UnloadSectorSystem : SystemBase
{
    private EntityCommandBufferSystem bufferSystem;
    private const float KEEP_CANDIDATE_FOR_MAX_SECONDS = 2;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireForUpdate(GetEntityQuery(typeof(UnloadCandidate), typeof(LocalToGalaxy)));
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        var ecb = bufferSystem.CreateCommandBuffer();
        var deltaTime = Time.DeltaTime;

        var localToGalaxyEntities = GetEntityQuery(typeof(LocalToGalaxy)).ToEntityArray(Allocator.Temp);
        var localToGalaxyBuffer = GetEntityQuery(typeof(LocalToGalaxy)).ToComponentDataArray<LocalToGalaxy>(Allocator.Temp);
        var playerLtg = GetComponent<LocalToGalaxy>(GetSingletonEntity<LocalPlayer>());
        var toRemoveList = new NativeList<Entity>(Allocator.Temp);
        Entities
            .WithAll<LocalToGalaxy>()
            .ForEach((Entity e, ref UnloadCandidate candidate) =>
            {
                candidate.Timeout += deltaTime;
                if (candidate.Timeout > KEEP_CANDIDATE_FOR_MAX_SECONDS)
                {
                    var ltg = GetComponent<LocalToGalaxy>(e);
                    if (ltg == playerLtg)
                    {
                        ecb.RemoveComponent<UnloadCandidate>(e);
                        return;
                    }

                    for(int i = 0; i < localToGalaxyEntities.Length; i++)
                        if (localToGalaxyBuffer[i] == ltg)
                            toRemoveList.Add(localToGalaxyEntities[i]);
                }
            }).Run();

        EntityManager.DestroyEntity(toRemoveList);
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
