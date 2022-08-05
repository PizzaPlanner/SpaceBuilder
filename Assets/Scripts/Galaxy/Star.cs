using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[GenerateAuthoringComponent]
public struct Star : IComponentData
{
    public Color Color;
    // public Entity LightEntity;
    public float Brightness;
    public float Size;
}

// [UpdateInGroup(typeof(PresentationSystemGroup))]
// public partial class StarLightingSystem : SystemBase
// {
//     protected override void OnCreate()
//     {
//         RequireSingletonForUpdate<LocalPlayer>();
//     }
//
//     protected override void OnUpdate()
//     {
//         var playerLtg = GetComponent<LocalToGalaxy>(GetSingletonEntity<LocalPlayer>());
//         Entities.ForEach((in Star star, in LocalToGalaxy ltg) =>
//         {
//             var light = EntityManager.GetComponentObject<Light>(star.LightEntity);
//             var relativePosition = LocalToGalaxy.RelativePosition(playerLtg, ltg);
//             var relativePosNormal = (float3)math.normalize(relativePosition);
//             SetComponent(star.LightEntity, new Rotation{Value = quaternion.LookRotation(relativePosNormal, math.up())});
//             light.color = star.Color * star.Brightness * math.clamp((float)(1.0 - math.log10(math.length(relativePosition) - GenerateStarSystem.STAR_SIZE_BASE) * 0.1f), 0, 1);
//         }).WithoutBurst().Run();
//     }
// }

[UpdateInGroup(typeof(UpdatePresentationSystemGroup))]
public partial class StarRelativeToPlayerSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<CameraAnchor>();
    }

    protected override void OnUpdate()
    {
        var farClipPlane = Camera.main.farClipPlane;
        var camPosition = (float3)Camera.main.transform.position;
        Entities
            .ForEach((ref LocalToWorld ltw, in Star star) =>
            {
                var tBefore = ltw.Position - camPosition;
                var newScale =
                    math.max(star.Size * (farClipPlane / math.length(tBefore)), 1);
                var tNew = math.normalize(tBefore) * (farClipPlane * 0.95f - newScale) + camPosition;
                ltw.Value = float4x4.TRS(tNew, ltw.Rotation, newScale);
            }).Schedule();
    }
}
