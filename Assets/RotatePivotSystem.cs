using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.PlayerLoop;

public struct UpdateRotation : IComponentData
{
    public quaternion Rotation;
}

[UpdateAfter(typeof(TransformSystemGroup))]
public class RotatePivotSystem : SystemBase
{
         EndSimulationEntityCommandBufferSystem bufferSystem;
         private EntityQuery removeQuery;

         protected override void OnCreate()
         {
             bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
             removeQuery = GetEntityQuery(ComponentType.Exclude<GhostPlacementBuilder>(), typeof(UpdateRotation));
         }

         protected override void OnUpdate()
        {
            Dependency = Entities.ForEach((ref Rotation r, ref RotationPivot pivot, in GridItem grid, in UpdateRotation update) =>
            {
                r.Value = update.Rotation;
                pivot.Value =  new float3(0.5f * grid.Size);
            }).Schedule(Dependency);

            var buffer = bufferSystem.CreateCommandBuffer();
            buffer.RemoveComponent<UpdateRotation>(removeQuery);
            bufferSystem.AddJobHandleForProducer(Dependency);
        }
}
