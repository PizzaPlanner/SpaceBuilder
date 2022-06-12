using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public struct GenerateStar : IComponentData { }

public partial class GenerateStarSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private Entity prefab;

    public const float STAR_SIZE_BASE = 5_000_000;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnStartRunning()
    {
        if(prefab == Entity.Null)
            prefab = GetEntityQuery(typeof(Prefab), typeof(Star)).GetSingletonEntity();
    }

    protected override void OnUpdate()
    {
        Entities
            .WithAll<GenerateStar, ToGenerate>()
            .ForEach((Entity e, in LocalToGalaxy ltg, in SectorData data) =>
            {
                var random = new Unity.Mathematics.Random(data.Seed);
                var star = EntityManager.Instantiate(prefab);
                var starData = GetComponent<Star>(star);
                starData.Brightness = random.NextFloat(3, 9);
                starData.Color = Color.HSVToRGB(random.NextFloat(), random.NextFloat(0.8f, 1),
                    random.NextFloat(0.8f, 1));
                starData.Size = random.NextFloat(0.3f, 12f);
                var starLtg = ltg;
                starLtg.LocalPosition = random.NextFloat3(-2000, 2000);
                SetComponent(star, starData);
                SetComponent(star, starLtg);
                SetComponent(star, new URPMaterialPropertyEmissionColor
                {
                    Value = new float4(starData.Color.a, starData.Color.g, starData.Color.b, 0) * starData.Brightness
                });
                EntityManager.AddComponentData(star, new Scale {Value = starData.Size * STAR_SIZE_BASE});
                EntityManager.RemoveComponent<GenerateStar>(e);
            }).WithStructuralChanges().WithoutBurst().Run();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
