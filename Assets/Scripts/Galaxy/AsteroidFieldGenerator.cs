using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public struct AsteroidFieldData : IComponentData
{
    public int AsteroidCount;
    public int Spawned;
    public int2 DiameterRange;
    public uint Seed;
}

public struct GenerateAsteroidField : IComponentData { }

public partial class GenerateAsteroidFieldSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer().AsParallelWriter();
        Entities
            .WithAll<GenerateAsteroidField, ToGenerate>()
            .ForEach((Entity e, int nativeThreadIndex, in LocalToGalaxy ltg, in SectorData data) =>
            {
                var random = new Unity.Mathematics.Random(data.Seed);
                var field = buffer.CreateEntity(nativeThreadIndex);
                var position = random.NextFloat3(new float3(-2000), new float3(2000));
                var fieldLtg = ltg;
                fieldLtg.PositionInSector = position;
                buffer.AddComponent(nativeThreadIndex, field, fieldLtg);
                buffer.AddComponent(nativeThreadIndex, field, new Translation{Value = position});
                buffer.AddComponent(nativeThreadIndex, field, new AsteroidFieldData
                {
                    Seed = random.NextUInt(),
                    AsteroidCount = random.NextInt(4000, 25000),
                    DiameterRange = random.NextInt2(new int2(15,30), new int2(20, 100))
                });
                buffer.AddComponent<ToGenerate>(nativeThreadIndex, field);
                buffer.RemoveComponent<GenerateAsteroidField>(nativeThreadIndex, e);
        }).ScheduleParallel();

        var prefabs = GetEntityQuery(typeof(Prefab), typeof(AsteroidData)).ToEntityArray(Allocator.TempJob);
        Entities
            .WithAll<ToGenerate, LocalToGalaxy>()
            .WithReadOnly(prefabs)
            .WithDisposeOnCompletion(prefabs)
            .ForEach((Entity e, int nativeThreadIndex, ref AsteroidFieldData data) =>
            {
                var random = new Unity.Mathematics.Random {state = data.Seed};
                var spawned = 0;
                while (data.Spawned < data.AsteroidCount && spawned < 1000)
                {
                    var location = random.NextFloat3(-new float3(2500), new float3(2500));
                    var asteroid = buffer.Instantiate(nativeThreadIndex, prefabs[random.NextInt(0, prefabs.Length - 1)]);
                    buffer.SetComponent(nativeThreadIndex, asteroid, new AsteroidData
                    {
                        Seed = random.NextUInt()
                    });
                    var asteroidLtg = GetComponent<LocalToGalaxy>(e);
                    asteroidLtg.PositionInSector += location;
                    buffer.SetComponent(nativeThreadIndex, asteroid, asteroidLtg);
                    // buffer.AddComponent(nativeThreadIndex, asteroid, new ToGenerate());
                    spawned++;
                    data.Spawned++;
                }

                if(data.Spawned >= data.AsteroidCount)
                    buffer.RemoveComponent<ToGenerate>(nativeThreadIndex, e);
            }).ScheduleParallel();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
