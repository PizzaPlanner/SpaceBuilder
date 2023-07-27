using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using Material = UnityEngine.Material;


[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
[CreateAfter(typeof(EntitiesGraphicsSystem))]
public partial class FixGhostVisualsAndPhysicsSystem : SystemBase
{
    public struct FixGhostandChildren : IComponentData
    {
        public bool GhostBuildable;
    }

    struct RelatedToGhost : IComponentData
    {
        public Entity Ghost;
    }

    BatchMaterialID ghostMaterialId, ghostBlockedMaterialId;
    protected override void OnCreate()
    {
        RequireForUpdate<Ghost>();
        var ghostMat = Addressables.LoadAssetAsync<Material>("GhostMaterial").WaitForCompletion();
        var ghostBlockedMat = Addressables.LoadAssetAsync<Material>("GhostErrorMaterial").WaitForCompletion();
        var graphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        ghostMaterialId = graphicsSystem.RegisterMaterial(ghostMat);
        ghostBlockedMaterialId = graphicsSystem.RegisterMaterial(ghostBlockedMat);
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (ghost, linkedEntities, parent) 
                 in SystemAPI.Query<FixGhostandChildren, DynamicBuffer<LinkedEntityGroup>>().WithEntityAccess())
        {
            foreach (var link in linkedEntities)
            {
                var e = link.Value;
                ecb.AddSharedComponent(e, new PhysicsWorldIndex(999));
                if (SystemAPI.HasComponent<MaterialMeshInfo>(e))
                {
                    ecb.AddComponent(e, new RelatedToGhost { Ghost = parent });
                }
            }
            ecb.RemoveComponent<FixGhostandChildren>(parent);
        }
        ecb.Playback(EntityManager);

        foreach (var (color, parent) in
                 SystemAPI.Query<RefRW<MaterialMeshInfo>, RelatedToGhost>())
        {
            color.ValueRW.MaterialID = SystemAPI.GetComponent<Ghost>(parent.Ghost).Buildable
                ? ghostMaterialId
                : ghostBlockedMaterialId;
        }
    }
}