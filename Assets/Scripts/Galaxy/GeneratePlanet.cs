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
        
    void Execute(Entity e, [EntityIndexInQuery] int index, in LocalToGalaxy ltg, in PlanetSectorData data)
    {
        var starSectorData = GalaxyTools.GetStarSectorData(ltg);
        var name = $"{GalaxyTools.GetStarName(GalaxyTools.GetSectorSeed(ltg.StarSector))}";
        for (byte i = 0; i < byte.MaxValue; i++)
        {
            if (math.all(starSectorData.GetPlanetSector(i) == ltg.PlanetSector))
            {
                name = $"{GalaxyTools.GetStarName(GalaxyTools.GetSectorSeed(ltg.StarSector))} {GalaxyTools.ToRomanNumeral(i)}";
            break;
            }
        }

        var random = new Random(data.Seed);
        var planet = ecb.Instantiate(index, prefab);
        var planetData = defaultPlanet;
        planetData.Color = Color.HSVToRGB(random.NextFloat(), random.NextFloat(0.6f, 1f),
            random.NextFloat(0.8f, 1));
        var body = defaultBody;
        body.Size = random.NextFloat(0.3f, 1);
        ecb.SetComponent(index, planet, planetData);
        ecb.SetComponent(index, planet, body);
        ecb.SetComponent(index, planet, ltg);
        ecb.SetComponent(index, planet, new URPMaterialPropertyBaseColor
        {
            Value = new float4(planetData.Color.a, planetData.Color.g, planetData.Color.b, 0)
        });
        ecb.SetComponent(index, planet, new Name{Value = name});
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
