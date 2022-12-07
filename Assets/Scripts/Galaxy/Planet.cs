using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public struct Planet : IComponentData
{
    public Color Color;
    public float Size;
}

[UpdateInGroup(typeof(UpdatePresentationSystemGroup))]
public partial class PlanetRelativeToPlayerSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<CameraAnchor>();
    }

    protected override void OnUpdate()
    {
        var farClipPlane = Camera.main.farClipPlane;
        var camPosition = (float3)Camera.main.transform.position;
        Entities
            .ForEach((ref LocalToWorld ltw, in Planet planet) =>
            {
                var tBefore = ltw.Position - camPosition;
                var newScale =
                    math.max(planet.Size * (farClipPlane / math.length(tBefore)), 3);
                var tNew = math.normalize(tBefore) * (farClipPlane * 0.95f - newScale) + camPosition;
                ltw.Value = float4x4.TRS(tNew, ltw.Rotation, newScale);
            }).Schedule();
    }
}
