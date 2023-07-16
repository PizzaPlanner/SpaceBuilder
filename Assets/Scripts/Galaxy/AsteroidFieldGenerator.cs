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
    protected override void OnUpdate()
    {
        var buffer = GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                     .CreateCommandBuffer(World.Unmanaged).AsParallelWriter();
        Entities
            .WithAll<GenerateAsteroidField>()
            .WithNone<UnloadCandidate>()
            .ForEach((Entity e, int nativeThreadIndex, in LocalToGalaxy ltg, in SubSectorData data) =>
            {
                var random = new Unity.Mathematics.Random(data.Seed);
                var field = buffer.CreateEntity(nativeThreadIndex);
                var fieldLtg = ltg;
                buffer.AddComponent(nativeThreadIndex, field, fieldLtg);
                buffer.AddComponent(nativeThreadIndex, field, new AsteroidFieldData
                {
                    Seed = random.NextUInt(),
                    AsteroidCount = random.NextInt(4000, 10000),
                    DiameterRange = random.NextInt2(new int2(15,30), new int2(20, 100))
                });
                buffer.AddComponent<ToGenerate>(nativeThreadIndex, field);
                buffer.RemoveComponent<GenerateAsteroidField>(nativeThreadIndex, e);
                Debug.Log($"Generating asteroid field");
        }).ScheduleParallel();

        var prefabs = GetEntityQuery(typeof(Prefab), typeof(AsteroidData)).ToEntityArray(Allocator.TempJob);
        Entities
            .WithReadOnly(prefabs)
            .WithDisposeOnCompletion(prefabs)
            .WithAll<ToGenerate, LocalToGalaxy>()
            .WithNone<UnloadCandidate>()
            .ForEach((Entity e, int nativeThreadIndex, ref AsteroidFieldData data) =>
            {
                var random = new Unity.Mathematics.Random {state = data.Seed};
                var spawned = 0;
                while (data.Spawned < data.AsteroidCount && spawned < 100)
                {
                    var asteroid = buffer.Instantiate(nativeThreadIndex, prefabs[random.NextInt(0, prefabs.Length - 1)]);
                    buffer.SetComponent(nativeThreadIndex, asteroid, new AsteroidData
                    {
                        Seed = random.NextUInt()
                    });
                    var asteroidLtg = GetComponent<LocalToGalaxy>(e);
                    asteroidLtg.LocalPosition = random.NextFloat3(Constants.MAX_POSITION);;
                    buffer.SetComponent(nativeThreadIndex, asteroid, asteroidLtg);
                    // buffer.AddComponent(nativeThreadIndex, asteroid, new ToGenerate());
                    spawned++;
                    data.Spawned++;
                }

                if(data.Spawned >= data.AsteroidCount)
                    buffer.RemoveComponent<ToGenerate>(nativeThreadIndex, e);
            }).ScheduleParallel();
    }
}

[UpdateInGroup(typeof(UpdatePresentationSystemGroup))]
public partial class AsteroidRelativeToPlayerSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<CameraAnchor>();
    }

    protected override void OnUpdate()
    {
        var ecb = GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged).AsParallelWriter();

        var farClipPlane = Camera.main.farClipPlane;
        var camPosition = (float3)Camera.main.transform.position;
        Entities
            .WithAll<AsteroidData, LocalToGalaxy>()
            .ForEach((Entity e, int nativeThreadIndex, ref LocalToWorld ltw, in LocalTransform transform) =>
            {
                var tBefore = transform.Position - camPosition;
                var newScale = math.min(farClipPlane / math.length(tBefore), 1);
                var tNew = math.select(math.normalize(tBefore) * farClipPlane + camPosition, tBefore, newScale >= 1);
                if (newScale < 0.2f && !SystemAPI.HasComponent<DisableRendering>(e))
                    ecb.AddComponent<DisableRendering>(nativeThreadIndex, e);
                else if(newScale > 0.2f && SystemAPI.HasComponent<DisableRendering>(e))
                    ecb.RemoveComponent<DisableRendering>(nativeThreadIndex, e);
                ltw.Value = float4x4.TRS(tNew, ltw.Rotation, newScale);
            }).ScheduleParallel();
    }
}
