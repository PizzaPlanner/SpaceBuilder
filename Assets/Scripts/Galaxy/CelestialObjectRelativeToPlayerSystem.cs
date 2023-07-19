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
        RequireForUpdate<ReferenceFrame>();
    }

    [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
    partial struct UpdateLtwJob : IJobEntity
    {
        const float SCALE_FACTOR = 1;
        const double SCALE_BOOST_RANGE_MIN = LocalToGalaxy.DISTANCE_PER_LIGHTSECTOR;
        const double IN_POI_RANGE = LocalToGalaxy.DISTANCE_PER_LIGHTSECTOR * 0.3f;
        
        public float farClipPlane;
        public float3 camPosition;
        public LocalToGalaxy refLtg;
        public void Execute(ref LocalToWorld ltw, in LocalToGalaxy ltg, in CelestialBody body, EnabledRefRW<InPoiRange> inPoiRange)
        {
            var delta = (ltg - refLtg).Distance - camPosition;
            var distance = math.length(delta);
            var distanceFactor = farClipPlane / distance;
            var scaleFactor = math.lerp(1, SCALE_FACTOR, distance / SCALE_BOOST_RANGE_MIN);
            var newScale = (float)(body.Size * distanceFactor * scaleFactor);
            var tNew = (float3)(math.normalize(delta) * (farClipPlane - 2 * newScale - 0.1f)) + camPosition;
            ltw.Value = float4x4.TRS(tNew, ltw.Rotation, newScale);
            inPoiRange.ValueRW = distance < IN_POI_RANGE;
        }
    }

    protected override void OnUpdate()
    {
        var farClipPlane = Camera.main.farClipPlane;
        var camPosition = (float3)Camera.main.transform.position;
        var refLtg = SystemAPI.GetSingleton<ReferenceFrame>().CurrentLtg;
        new UpdateLtwJob()
        {
            refLtg = refLtg,
            camPosition = camPosition,
            farClipPlane = farClipPlane
        }.ScheduleParallel();
    }
}