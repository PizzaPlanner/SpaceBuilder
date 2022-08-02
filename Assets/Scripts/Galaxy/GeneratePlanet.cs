using System.Linq;
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
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private Entity prefab;
    private Planet defaultPlanetData;

    public const float PLANET_SIZE_BASE = 30_000;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if (prefab == Entity.Null)
        {
            prefab = GetEntityQuery(typeof(Prefab), typeof(Planet)).GetSingletonEntity();
            defaultPlanetData = GetComponent<Planet>(prefab);
        }

        var buffer = bufferSystem.CreateCommandBuffer();
        var prefabPlanetData = defaultPlanetData;
        var prefabLocal = prefab;
        Entities
            .WithAll<GeneratePlanet, ToGenerate>()
            .ForEach((Entity e, in LocalToGalaxy ltg, in SubSectorData data) =>
            {
                var random = new Random(data.Seed);
                var planet = buffer.Instantiate(prefabLocal);
                var planetData = prefabPlanetData;
                planetData.Color = Color.HSVToRGB(random.NextFloat(), random.NextFloat(0.6f, 1f),
                    random.NextFloat(0.8f, 1));
                planetData.Size = random.NextFloat(1f, 9f);
                buffer.SetComponent(planet, planetData);
                buffer.SetComponent(planet, ltg);
                buffer.SetComponent(planet, new URPMaterialPropertyEmissionColor
                {
                    Value = new float4(planetData.Color.a, planetData.Color.g, planetData.Color.b, 0)
                });
                buffer.AddComponent(planet, new Scale {Value = planetData.Size * PLANET_SIZE_BASE});
                buffer.RemoveComponent<GeneratePlanet>(e);
            }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
