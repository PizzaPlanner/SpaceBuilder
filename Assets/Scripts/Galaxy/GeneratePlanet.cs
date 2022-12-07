using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public struct GeneratePlanet : IComponentData { }

public partial class GeneratePlanetSystem : SystemBase
{
    private Entity prefab;
    private Planet defaultPlanetData;

    public const float PLANET_SIZE_BASE = 30_000;
    public const float PLANET_MAX_SIZE_FACTOR = 9f;

    protected override void OnUpdate()
    {
        if (prefab == Entity.Null)
        {
            if (!GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                                .WithOptions(EntityQueryOptions.IncludePrefab)
                                .WithAll<Planet>())
                    .TryGetSingletonEntity<Planet>(out prefab))
                return;
            defaultPlanetData = GetComponent<Planet>(prefab);
        }

        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var prefabPlanetData = defaultPlanetData;
        var prefabLocal = prefab;
        Entities
            .WithAll<GeneratePlanet>()
            .ForEach((Entity e, in LocalToGalaxy ltg, in SubSectorData data) =>
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
                var planet = buffer.Instantiate(prefabLocal);
                var planetData = prefabPlanetData;
                planetData.Color = Color.HSVToRGB(random.NextFloat(), random.NextFloat(0.6f, 1f),
                    random.NextFloat(0.8f, 1));
                planetData.Size = random.NextFloat(1f, PLANET_MAX_SIZE_FACTOR) * PLANET_SIZE_BASE;
                buffer.SetComponent(planet, planetData);
                buffer.SetComponent(planet, ltg);
                buffer.SetComponent(planet, new URPMaterialPropertyEmissionColor
                {
                    Value = new float4(planetData.Color.a, planetData.Color.g, planetData.Color.b, 0)
                });
                buffer.AddComponent(planet, new Scale {Value = planetData.Size});
                buffer.SetComponent(planet, new Name{Value = $"{GalaxyTools.GetStarName(GalaxyTools.GetSectorSeed(ltg.Sector))} {GalaxyTools.ToRomanNumeral(planetRank)}"});
                buffer.RemoveComponent<GeneratePlanet>(e);
            }).Schedule();
    }
}
