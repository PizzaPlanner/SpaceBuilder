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
                starData.Brightness = random.NextFloat(8, 44);
                starData.Color = Color.HSVToRGB(random.NextFloat(), random.NextFloat(0.8f, 1),
                    random.NextFloat(0.8f, 2));
                starData.Size = random.NextFloat(0.3f, STAR_MAX_SIZE_FACTOR) * STAR_SIZE_BASE;
                buffer.SetComponent(star, starData);
                buffer.SetComponent(star, ltg);
                buffer.SetComponent(star, new URPMaterialPropertyEmissionColor
                {
                    Value = new float4(starData.Color.a, starData.Color.g, starData.Color.b, 0) * starData.Brightness
                });
                buffer.AddComponent(star, new Scale { Value = starData.Size });
                buffer.SetComponent(star, new Name{Value = $"{GalaxyTools.GetStarName(data.Seed)}"});
                buffer.RemoveComponent<GenerateStar>(e);
            }).Schedule();
    }
}
