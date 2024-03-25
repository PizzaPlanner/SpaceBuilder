using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Authoring;
using Unity.Transforms;
using UnityEngine;

namespace PhysicsExtensions.Editor
{
    /// <summary>
    /// Indicate that this Child should have partially independent Physics from its Parent
    /// The Part will be constrained to the Parent, but have an independently movable Collider
    /// This can be used i.e. on the inner part of a Door
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MovablePartAuthoring : MonoBehaviour
    {
        void OnValidate()
        {
            GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePosition;
            GetComponent<Rigidbody>().isKinematic = true;
        }

        class PartBaker : Baker<MovablePartAuthoring>
        {
            public override void Bake(MovablePartAuthoring authoring)
            {
                if (authoring.transform.parent == null)
                {
                    Debug.LogWarning($"{authoring.gameObject.name} has {nameof(MovablePartAuthoring)} attached, but is not a child of another GameObject");
                    return;
                }
                
                var self = GetEntity(TransformUsageFlags.Dynamic);
                var parent = GetEntity(authoring.transform.parent, TransformUsageFlags.None);
                AddComponent(self, new ReparentLater{Parent = parent});
            }
        }
    }

    [BakingType]
    public struct ReparentLater : IComponentData
    {
        public Entity Parent;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateAfter(typeof(EndColliderBakingSystem))]
    partial struct ReparentSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (reparent, e) in
                     SystemAPI.Query<ReparentLater>()
                              .WithOptions(
                                  EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                              .WithNone<Parent>()
                              .WithEntityAccess())
            {
                ecb.AddComponent(e, new Parent { Value = reparent.Parent });
                Debug.Log($"Reparenting");
            }
            ecb.Playback(state.EntityManager);
        }
    }
}