using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[WriteGroup(typeof(LocalToWorld))]
public struct CelestialBody : IComponentData
{
    public float Size;
}

[UpdateInGroup(typeof(UpdatePresentationSystemGroup))]
public partial class CelestialObjectRelativeToPlayerSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<CameraAnchor>();
    }

    partial struct UpdateLtwJob : IJobEntity
    {
        public float farClipPlane;
        public float3 camPosition;
        public void Execute(ref LocalToWorld ltw, in LocalTransform transform, in CelestialBody body)
        {
            var tBefore = transform.Position - camPosition;
            var newScale = math.max(body.Size * (farClipPlane / math.length(tBefore)), 3);
            var tNew = math.normalize(tBefore) * (farClipPlane * 0.95f - newScale) + camPosition;
            ltw.Value = float4x4.TRS(tNew, ltw.Rotation, newScale);
        }
    }

    protected override void OnUpdate()
    {
        var farClipPlane = Camera.main.farClipPlane;
        var camPosition = (float3)Camera.main.transform.position;
        new UpdateLtwJob()
        {
            camPosition = camPosition,
            farClipPlane = farClipPlane
        }.ScheduleParallel();
    }
}