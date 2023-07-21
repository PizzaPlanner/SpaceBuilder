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
public partial class CelestialObjectRelativeToReferenceSystem : SystemBase
{

    struct FarAwayObjectsVisibiltyBoostSettings : IComponentData
    {
        public float BoostAtMaximumRange;
    }
    
    protected override void OnCreate()
    {
        RequireForUpdate<ReferenceFrame>();
        RequireForUpdate<FarAwayObjectsVisibiltyBoostSettings>();
        EntityManager.CreateEntity(typeof(FarAwayObjectsVisibiltyBoostSettings));
        SystemAPI.SetSingleton(new FarAwayObjectsVisibiltyBoostSettings { BoostAtMaximumRange = 1 });
    }

    [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
    partial struct UpdateLtwJob : IJobEntity
    {
        const double IN_POI_RANGE_STAR = LocalToGalaxy.DISTANCE_PER_LIGHTSECTOR * 0.6f;

        public float distanceScaleBoost;
        public float farClipPlane;
        public float3 camPosition;
        public LocalToGalaxy refLtg;
        public void Execute(ref LocalToWorld ltw, in LocalToGalaxy ltg, in CelestialBody body, EnabledRefRW<ShouldShowAsPOI> inPoiRange)
        {
            var delta = (ltg - refLtg).Distance - camPosition;
            var distance = math.length(delta);
            var distanceFactor = farClipPlane / distance;
            var fancyFactor = math.lerp(1, distanceScaleBoost, distance / LocalToGalaxy.MAX_MEASURABLE_DISTANCE);
            var newScale = (float)(body.Size * distanceFactor * fancyFactor);
            var tNew = (float3)(math.normalize(delta) * farClipPlane + camPosition);
            ltw.Value = float4x4.TRS(tNew, ltw.Rotation, newScale);
            inPoiRange.ValueRW = distance < IN_POI_RANGE_STAR;
        }
    }

    protected override void OnUpdate()
    {
        var farClipPlane = Camera.main.farClipPlane;
        var camPosition = (float3)Camera.main.transform.position;
        var refLtg = SystemAPI.GetSingleton<ReferenceFrame>().CurrentLtg;
        var boost = SystemAPI.GetSingleton<FarAwayObjectsVisibiltyBoostSettings>().BoostAtMaximumRange;
        new UpdateLtwJob()
        {
            distanceScaleBoost = boost,
            refLtg = refLtg,
            camPosition = camPosition,
            farClipPlane = farClipPlane
        }.ScheduleParallel();
    }
}