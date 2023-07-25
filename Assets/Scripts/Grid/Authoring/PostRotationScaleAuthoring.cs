using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PostRotationScaleAuthoring : MonoBehaviour
{
    public float3 Value;

    public class PostRotationScaleBaker : Baker<PostRotationScaleAuthoring>
    {
        public override void Bake(PostRotationScaleAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PostRotationScale { Value = authoring.Value });
        }
    }
}