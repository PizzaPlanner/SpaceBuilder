using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;

namespace Grid.Building
{
    [CreateAfter(typeof(EntitiesGraphicsSystem))]
    public partial class PartRemovalSystem : SystemBase
    {
        BatchMaterialID RemovalMaterial;
        protected override void OnCreate()
        {
            var removalMat = Addressables.LoadAssetAsync<Material>("GhostErrorMaterial").WaitForCompletion();
            var graphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            RemovalMaterial = graphicsSystem.RegisterMaterial(removalMat);

            RequireForUpdate<ControlScheme.Building.Destroy>();
        }

        protected override void OnStopRunning()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);
            new RemovalVisualizationUndoJob { ecb = ecb }.Run();
        }
        
        protected override void OnStartRunning()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);
            AddVisualizationToTarget(SystemAPI.GetComponent<Target>(SystemAPI.GetSingletonEntity<LocalPlayer>()).Value, ecb);
        }

        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);
            
            new RemovalVisualizationApplyJob { RemovalMaterial = RemovalMaterial }.Run();
            
            var target = SystemAPI.GetComponent<Target>(SystemAPI.GetSingletonEntity<LocalPlayer>());
            if(SystemAPI.GetSingleton<InputData>().WantBuild 
               && target.Value != Entity.Null && SystemAPI.HasComponent<VisualizeRemoval>(target.Value))
                ecb.AddComponent<RemoveFromGrid>(target.Value);

            if (target.Value == target.PreviousValue)
                return;
            
            if (target.PreviousValue != Entity.Null)
                new RemovalVisualizationUndoJob() { ecb = ecb }.Run();
            
            AddVisualizationToTarget(target.Value, ecb);
        }

        void AddVisualizationToTarget(Entity target, EntityCommandBuffer ecb)
        {
            if (target != Entity.Null && SystemAPI.HasComponent<GridItem>(target))
            {
                if(SystemAPI.HasBuffer<LinkedEntityGroup>(target))
                    foreach(var entity in SystemAPI.GetBuffer<LinkedEntityGroup>(target))
                        ecb.AddComponent<VisualizeRemoval>(entity.Value);
                else
                    ecb.AddComponent<VisualizeRemoval>(target);
            }
        }
    }
}