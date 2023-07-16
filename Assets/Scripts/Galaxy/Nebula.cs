using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.VFX;

[WriteGroup(typeof(LocalToWorld))]
public struct Nebula : IComponentData { }

[UpdateInGroup(typeof(UpdatePresentationSystemGroup))]
public partial class NebulaRelativeToPlayerSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<CameraAnchor>();
    }

    protected override void OnUpdate()
    {
        var farClipPlane = Camera.main.farClipPlane;
        var camPosition = (float3)Camera.main.transform.position;
        
        Entities.WithAll<Nebula, ToGenerate>()
            .ForEach((VisualEffect sfx, in LocalToGalaxy ltg) =>
            {
                var seed = GalaxyTools.GetSubsectorSeed(ltg);
                sfx.startSeed = seed;
                sfx.Reinit();
            }).WithoutBurst().Run();

        EntityManager.RemoveComponent<ToGenerate>(GetEntityQuery(typeof(Nebula), typeof(ToGenerate)));
        
        Entities
            .WithAll<Nebula>()
            .ForEach((VisualEffect sfx, ref LocalToWorld ltw, in LocalTransform transform) =>
            {
                var tBefore = transform.Position - camPosition;
                var newScale = farClipPlane / math.length(tBefore);
                var tNew = math.select(math.normalize(tBefore) * farClipPlane + camPosition, tBefore, newScale >= 1);
                // var tNew = math.normalize(tBefore) * (farClipPlane * 0.95f - newScale) + camPosition;
                var newLtw = new LocalToWorld{Value = float4x4.TRS(tNew, ltw.Rotation, newScale)};
                sfx.SetFloat("Scale", newScale);
                sfx.gameObject.transform.SetPositionAndRotation(newLtw.Position, newLtw.Rotation);
                ltw = newLtw;
                Debug.DrawLine(camPosition, newLtw.Position);
                Debug.Log($"NEB at {newScale}");
            }).WithoutBurst().Run();
    }
}
