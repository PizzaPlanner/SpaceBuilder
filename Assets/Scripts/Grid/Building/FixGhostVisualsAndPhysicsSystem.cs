using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Physics;
using UnityEngine.Rendering;



[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
// Required until hopefully someday there is ecb.AddComponentForLinkedEntityGroup that accepts sharedcomponents
public partial struct FixGhostVisualsAndPhysicsSystem : ISystem
{
    public struct FixGhostandChildren : IComponentData
    {
        public bool GhostBuildable;
    }
    
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (ghost, linkedEntities, parent) 
                 in SystemAPI.Query<FixGhostandChildren, DynamicBuffer<LinkedEntityGroup>>().WithEntityAccess())
        {
            foreach (var link in linkedEntities)
            {
                var e = link.Value;
                ecb.AddSharedComponent(e, new PhysicsWorldIndex(999));
                // ecb.AddSharedComponent(e, new RenderFilterSettings
                // {
                //     Layer = ghost.GhostBuildable ? 8 : 9, 
                //     ReceiveShadows = false,
                //     ShadowCastingMode = ShadowCastingMode.Off
                // });
            }
            ecb.RemoveComponent<FixGhostandChildren>(parent);
        }
        ecb.Playback(state.EntityManager);
    }
}