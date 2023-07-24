using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Galaxy
{
    public struct LightEmitter : IComponentData, IEnableableComponent
    {
        public float4 Color;
        public double MaxDistanceSq;
    }

    [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
    public readonly partial struct LightEmitterAspect : IAspect
    {
        readonly EnabledRefRW<LightEmitter> emitterFlag;
        readonly RefRW<LightEmitter> emitter;
        readonly RefRO<LocalToGalaxy> ltg;

        public LocalToGalaxy LocalToGalaxy => ltg.ValueRO;

        public void TogglyByDistance(double distanceSq)
        {
            emitterFlag.ValueRW = emitter.ValueRO.MaxDistanceSq > distanceSq;
        }
    }

    [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
    public partial struct EnableLightEmittersJob : IJobEntity
    {
        public LocalToGalaxy referenceLtg;
        void Execute(LightEmitterAspect aspect)
        {
            var deltaSq = math.lengthsq((aspect.LocalToGalaxy - referenceLtg).Distance);
            aspect.TogglyByDistance(deltaSq);
        }
    }

    public partial class LightEmitterSystem : SystemBase
    {
        Light[] _lights;
        EntityQuery _emitterQuery;

        protected override void OnCreate()
        {
            _lights = new Light[16];
            for (int i = 0; i < _lights.Length; i++)
            {
                var e = new GameObject(){name = "StarLight"};
                var light = e.AddComponent<Light>();
                _lights[i] = light;
                light.type = LightType.Directional;
            }
            _emitterQuery = GetEntityQuery(typeof(LightEmitter), typeof(LocalToWorld), typeof(LocalToGalaxy));
            RequireForUpdate<ReferenceFrame>();
            RequireForUpdate<ReferenceFrameOrigin>();
        }

        protected override void OnUpdate()
        {
            var refFrame = SystemAPI.GetSingleton<ReferenceFrame>();

            var localToWorlds = _emitterQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            var localToGalaxies = _emitterQuery.ToComponentDataArray<LocalToGalaxy>(Allocator.Temp);
            var lightEmitters = _emitterQuery.ToComponentDataArray<LightEmitter>(Allocator.Temp);
            var currentEmitter = 0;
            for (int i = 0; i < _lights.Length; i++)
            {
                if (currentEmitter >= lightEmitters.Length)
                    _lights[i].gameObject.SetActive(false);
                else
                {
                    var emitter = lightEmitters[currentEmitter];
                    var stellarDistance = (localToGalaxies[currentEmitter] - refFrame.CurrentLtg).Distance;
                    var location = localToWorlds[currentEmitter].Position;
                    _lights[i].gameObject.SetActive(true);
                    _lights[i].color = new Color(emitter.Color.x, emitter.Color.y, emitter.Color.z, emitter.Color.w);
                    _lights[i].intensity = (float)(emitter.Color.w / math.lengthsq(stellarDistance) * LocalToGalaxy.DISTANCE_PER_STARSECTOR);
                    _lights[i].transform.position = location;
                    _lights[i].transform.LookAt(refFrame.CurrentLtw.Position);
                    ++currentEmitter;
                }
            }
            
            new EnableLightEmittersJob
            {
               referenceLtg = refFrame.CurrentLtg
            }.ScheduleParallel();
        }
    }
}