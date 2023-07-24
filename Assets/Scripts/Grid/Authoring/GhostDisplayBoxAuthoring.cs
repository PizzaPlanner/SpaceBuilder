using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Grid.Authoring
{
    public class GhostDisplayBoxAuthoring : MonoBehaviour
    {
        class AuthoringBaker : Baker<GhostDisplayBoxAuthoring>
        {
            public override void Bake(GhostDisplayBoxAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GhostDisplayBox());
                AddComponent<PostTransformMatrix>(entity);
            }
        }
    }
}