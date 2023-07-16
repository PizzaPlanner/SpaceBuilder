using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public struct GeneratePlanet : IComponentData { }

[WithAll(typeof(GeneratePlanet))]
partial struct GeneratePlanetJob : IJobEntity
{
    public Entity prefab;
    public Planet defaultPlanet;
    public CelestialBody defaultBody;
    public EntityCommandBuffer.ParallelWriter ecb;
        
    void Execute(Entity e, [EntityIndexInQuery] int index, in LocalToGalaxy ltg, in SubSectorData data)
    {
        var planetRank = byte.MaxValue;
        var starSectorData = GalaxyTools.GetStarSectorData(ltg.Sector);
        for (byte i = 0; i < byte.MaxValue; i++)
        {
            if (math.all(GalaxyTools.GetPlanetData(starSectorData, i).SubSector == ltg.SubSector))
            {
                planetRank = i;
                break;
            }
        }

        var random = new Random(data.Seed);
        var planet = ecb.Instantiate(index, prefab);
        var planetData = defaultPlanet;
        planetData.Color = Color.HSVToRGB(random.NextFloat(), random.NextFloat(0.6f, 1f),
            random.NextFloat(0.8f, 1));
        var body = defaultBody;
        body.Size = random.NextFloat(1f, GeneratePlanetSystem.PLANET_MAX_SIZE_FACTOR) * GeneratePlanetSystem.PLANET_SIZE_BASE;
        ecb.SetComponent(index, planet, planetData);
        ecb.SetComponent(index, planet, body);
        ecb.SetComponent(index, planet, ltg);
        ecb.SetComponent(index, planet, new URPMaterialPropertyBaseColor
        {
            Value = new float4(planetData.Color.a, planetData.Color.g, planetData.Color.b, 0)
        });
        ecb.SetComponent(index, planet, new Name{Value = $"{GalaxyTools.GetStarName(GalaxyTools.GetSectorSeed(ltg.Sector))} {GalaxyTools.ToRomanNumeral(planetRank)}"});
    }
}

[BurstCompile]
public partial struct GeneratePlanetSystem : ISystem
{
    EntityQuery prefabQuery;
    EntityQuery generateQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<GeneratePlanet>();
        prefabQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                                           .WithOptions(EntityQueryOptions.IncludePrefab)
                                           .WithAll<Planet, Prefab>());
        generateQuery = state.GetEntityQuery(typeof(GeneratePlanet));
    }

    public const float PLANET_SIZE_BASE = 30_000;
    public const float PLANET_MAX_SIZE_FACTOR = 9f;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var prefab = prefabQuery.GetSingletonEntity();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);
        ecb.RemoveComponent<GeneratePlanet>(generateQuery, EntityQueryCaptureMode.AtRecord);
        new GeneratePlanetJob()
        {
            ecb = ecb.AsParallelWriter(),
            prefab = prefab,
            defaultPlanet = SystemAPI.GetComponent<Planet>(prefab),
            defaultBody = SystemAPI.GetComponent<CelestialBody>(prefab),   
        }.ScheduleParallel();
    }
}
