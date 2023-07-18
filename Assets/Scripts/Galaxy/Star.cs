using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Galaxy {
    public struct Star : IComponentData
    {
        public float ColorTemperature;
        // public Entity LightEntity;
        public float Brightness;
    }

    // [UpdateInGroup(typeof(PresentationSystemGroup))]
    // public partial class StarLightingSystem : SystemBase
    // {
    //     protected override void OnCreate()
    //     {
    //         RequireForUpdate<LocalPlayer>();
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

    public partial class CalculateClosestStarSystem : SystemBase
    {
        public struct ClosestStarSector : IComponentData
        {
            public LocalToGalaxy StarLTG;
            public double DistanceInSubSectors;
        }
        protected override void OnCreate()
        {
            RequireForUpdate<CameraAnchor>();
            RequireForUpdate<LocalPlayer>();
            RequireForUpdate(GetEntityQuery(typeof(SectorChange)));
            EntityManager.CreateEntity(typeof(ClosestStarSector));
        }

        protected override void OnUpdate()
        {
            var playerLtg = SystemAPI.GetComponent<LocalToGalaxy>(SystemAPI.GetSingletonEntity<LocalPlayer>());
            LocalToGalaxy closestStar = default;
            var closestDistance = double.PositiveInfinity;
            Entities
                .WithAll<Star>()
                .ForEach((in LocalToGalaxy ltg) =>
                {
                    var len = GalaxyTools.DistanceInSubsectors(ltg - playerLtg);
                    if (len < closestDistance)
                    {
                        closestDistance = len;
                        closestStar = ltg;
                    }
                }).Run();
            SetSingleton(new ClosestStarSector{StarLTG = closestStar, DistanceInSubSectors = closestDistance});
        }
    }
}