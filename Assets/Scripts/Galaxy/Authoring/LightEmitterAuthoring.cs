using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Galaxy
{
    public class LightEmitterAuthoring : MonoBehaviour
    {
        public Color Color;
        public double MaxDistance;
        public float Intensity;

        public class LightEmitterAuthoringBaker : Baker<LightEmitterAuthoring>
        {
            public override void Bake(LightEmitterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new LightEmitter
                    {
                        Color = new float4(authoring.Color.r, authoring.Color.g, authoring.Color.b, authoring.Intensity), 
                        MaxDistanceSq = authoring.MaxDistance * authoring.MaxDistance
                    });
                SetComponentEnabled<LightEmitter>(entity, false);
            }
        }
    }
}