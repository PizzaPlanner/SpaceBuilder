using Galaxy;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public struct GenerateStar : IComponentData { }

[MaterialProperty("_Temperature", -1)]
public struct URPStarTemperature : IComponentData, IQueryTypeParameter
{
    public float Value;
}

[MaterialProperty("_FlareIntensity", -1)]
public struct URPStarFlareIntensity : IComponentData, IQueryTypeParameter
{
    public float Value;
}

[WithAll(typeof(GenerateStar))]
partial struct GenerateStarJob : IJobEntity
{
    public Entity prefab;
    public Star defaultStar;
    public CelestialBody defaultBody;
    public EntityCommandBuffer.ParallelWriter ecb;
        
    void Execute(Entity e, [EntityIndexInQuery] int index, in LocalToGalaxy ltg, in SubSectorData data)
    {
        var random = new Random(data.Seed);
        var star = ecb.Instantiate(index, prefab);
        var starData = defaultStar;
        var starBody = defaultBody;
        random.NextFloat();
        starBody.Size = random.NextFloat(0.3f, GenerateStarSystem.STAR_MAX_SIZE_FACTOR) * GenerateStarSystem.STAR_SIZE_BASE;
        starData.ColorTemperature = random.NextFloat(-2.1f, 2.06f);
        starData.Brightness = random.NextFloat(200 + 40 * starData.ColorTemperature, 300);
        ecb.SetComponent(index, star, starData);
        ecb.SetComponent(index, star, starBody);
        ecb.SetComponent(index, star, ltg);
        ecb.SetComponent(index, star, new URPStarTemperature { Value = starData.ColorTemperature });
        ecb.SetComponent(index, star, new URPStarFlareIntensity { Value = starData.Brightness });
        ecb.SetComponent(index, star, new Name{Value = $"{GalaxyTools.GetStarName(data.Seed)}"});
    }
}

[BurstCompile]
public partial struct GenerateStarSystem : ISystem
{
    EntityQuery prefabQuery;
    EntityQuery generateQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<GenerateStar>();
        prefabQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                                           .WithOptions(EntityQueryOptions.IncludePrefab)
                                           .WithAll<Star, Prefab>());
        generateQuery = state.GetEntityQuery(typeof(GenerateStar));
    }

    public const float STAR_SIZE_BASE = 5_000_000;
    public const float STAR_MAX_SIZE_FACTOR = 12;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var prefab = prefabQuery.GetSingletonEntity();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);
        ecb.RemoveComponent<GenerateStar>(generateQuery, EntityQueryCaptureMode.AtRecord);
        new GenerateStarJob()
        {
            ecb = ecb.AsParallelWriter(),
            prefab = prefab,
            defaultStar = SystemAPI.GetComponent<Star>(prefab),
            defaultBody = SystemAPI.GetComponent<CelestialBody>(prefab),   
        }.ScheduleParallel();
    }
}