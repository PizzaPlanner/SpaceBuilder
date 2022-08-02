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
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private Entity prefab;
    private Star defaultStarData;

    public const float STAR_SIZE_BASE = 5_000_000;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if (prefab == Entity.Null)
        {
            prefab = GetEntityQuery(typeof(Prefab), typeof(Star)).GetSingletonEntity();
            defaultStarData = GetComponent<Star>(prefab);
        }

        var buffer = bufferSystem.CreateCommandBuffer();
        var prefabStarData = defaultStarData;
        var prefabLocal = prefab;
        Entities
            .WithAll<GenerateStar, ToGenerate>()
            .ForEach((Entity e, in LocalToGalaxy ltg, in SectorData data) =>
            {
                var random = new Random(data.Seed);
                var star = buffer.Instantiate(prefabLocal);
                var starData = prefabStarData;
                starData.Brightness = random.NextFloat(3, 9);
                starData.Color = Color.HSVToRGB(random.NextFloat(), random.NextFloat(0.8f, 1),
                    random.NextFloat(0.8f, 1));
                starData.Size = random.NextFloat(0.3f, 12f);
                buffer.SetComponent(star, starData);
                buffer.SetComponent(star, ltg);
                buffer.SetComponent(star, new URPMaterialPropertyEmissionColor
                {
                    Value = new float4(starData.Color.a, starData.Color.g, starData.Color.b, 0) * starData.Brightness
                });
                buffer.AddComponent(star, new Scale {Value = starData.Size * STAR_SIZE_BASE});
                buffer.RemoveComponent<GenerateStar>(e);
            }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
