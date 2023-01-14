using Galaxy;
using System.Linq;
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

public partial class GenerateStarSystem : SystemBase
{
    private Entity prefab;
    private Star defaultStarData;

    public const float STAR_SIZE_BASE = 5_000_000;
    public const float STAR_MAX_SIZE_FACTOR = 12;
    
    protected override void OnUpdate()
    {
        if (prefab == Entity.Null)
        {
            if (!GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                                .WithOptions(EntityQueryOptions.IncludePrefab)
                                .WithAll<Star>())
                    .TryGetSingletonEntity<Star>(out prefab))
                return;
            
            defaultStarData = GetComponent<Star>(prefab);
        }

        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var prefabStarData = defaultStarData;
        var prefabLocal = prefab;
        Entities
            .WithAll<GenerateStar>()
            .ForEach((Entity e, in LocalToGalaxy ltg, in SectorData data) =>
            {
                var random = new Random(data.Seed);
                var star = buffer.Instantiate(prefabLocal);
                var starData = prefabStarData;
                var _ = random.NextFloat();
                starData.Size = random.NextFloat(0.3f, STAR_MAX_SIZE_FACTOR) * STAR_SIZE_BASE;
                starData.ColorTemperature = random.NextFloat(-2.1f, 2.06f);
                starData.Brightness = random.NextFloat(200 + 40 * starData.ColorTemperature, 300);
                buffer.SetComponent(star, starData);
                buffer.SetComponent(star, ltg);
                buffer.AddComponent(star, new URPStarTemperature { Value = starData.ColorTemperature });
                buffer.AddComponent(star, new URPStarFlareIntensity { Value = starData.Brightness });
                buffer.AddComponent(star, WorldTransform.Identity.WithScale(starData.Size));
                buffer.SetComponent(star, new Name{Value = $"{GalaxyTools.GetStarName(data.Seed)}"});
                buffer.RemoveComponent<GenerateStar>(e);
            }).Schedule();
    }
}
