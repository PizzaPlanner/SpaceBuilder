using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[GenerateAuthoringComponent]
public struct Star : IComponentData
{
    public Color Color;
    public Entity LightEntity;
    public float Brightness;
    public float Size;
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class StarLightingSystem : SystemBase
{
    public float STAR_SIZE_BASE = 5_000_000;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        var playerLtg = GetComponent<LocalToGalaxy>(GetSingletonEntity<LocalPlayer>());
        Entities
            .WithAll<ToGenerate>()
            .ForEach((Entity e, ref URPMaterialPropertyEmissionColor emission, in Star star) =>
            {
                emission.Value = new float4(star.Color.a * star.Brightness, star.Color.b * star.Brightness, star.Color.g * star.Brightness, 0);
                EntityManager.RemoveComponent<ToGenerate>(e);
                EntityManager.AddComponentData(e, new Scale {Value = star.Size * STAR_SIZE_BASE});
            }).WithoutBurst().WithStructuralChanges().Run();

        Entities.ForEach((in Star star, in LocalToGalaxy ltg) =>
        {
            var light = EntityManager.GetComponentObject<Light>(star.LightEntity);
            var relativePosition = playerLtg.AbsolutePosition - ltg.AbsolutePosition;
            var relativePosNormal = (float3)math.normalize(relativePosition);
            SetComponent(star.LightEntity, new Rotation{Value = quaternion.LookRotation(relativePosNormal, math.up())});
            light.color = star.Color * star.Brightness * math.clamp((float)(1.0 - math.log10(math.length(relativePosition) - STAR_SIZE_BASE) * 0.1f), 0, 1);
        }).WithoutBurst().Run();
    }
}
