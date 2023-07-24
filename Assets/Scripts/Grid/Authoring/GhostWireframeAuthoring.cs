using Unity.Entities;
using UnityEngine;

namespace Grid.Authoring
{
    public class GhostWireframeAuthoring : MonoBehaviour
    {
        class AuthoringBaker : Baker<GhostWireframeAuthoring>
        {
            public override void Bake(GhostWireframeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GhostWireframe());
            }
        }
    }
}