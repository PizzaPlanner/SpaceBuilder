using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[UpdateBefore(typeof(TransformSystemGroup))]
public class CalculateBuildingSlotSystem : SystemBase
{
    BuildPhysicsWorld physicsWorld;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<GhostPlacementBuilder>();
        RequireSingletonForUpdate<CameraAnchor>();
        physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
    }

    protected override void OnStartRunning()
    {
        GhostBuilder.WireframeDisplay.gameObject.SetActive(true);
    }

    protected override void OnStopRunning()
    {
        if(GhostBuilder.WireframeDisplay)
            GhostBuilder.WireframeDisplay.gameObject.SetActive(false);
    }

    protected override void OnUpdate()
    {
        var ltw = GetComponent<LocalToWorld>(GetSingletonEntity<CameraAnchor>());
        var bgpe = GetSingletonEntity<GhostPlacementBuilder>();
        var gridEntity = GetComponent<Parent>(bgpe).Value;
        var gridLtw = GetComponent<LocalToWorld>(gridEntity);
        var gridItem = GetComponent<GridItem>(bgpe);
        var bgp = EntityManager.GetComponentData<GhostPlacementBuilder>(bgpe);

        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;

        float3 target = ltw.Position + ltw.Forward * gridItem.Size * bgp.Range;
        for (float f = gridItem.Size * bgp.Range; f > 0; f -= gridItem.Size * 0.25f)
        {
            var input = new RaycastInput
            {
                Start = ltw.Position + ltw.Forward * f,
                End = ltw.Position
            };
            if (collisionWorld.CastRay(input, out var hit))
            {
                target = hit.Position;
                break;
            }
        }


        bgp.CurrentSlot = Grid.CellFromPosition(gridLtw, target, gridItem.Size);

        var newPos = new Translation {Value = Grid.PositionFromCell(new LocalToWorld(), bgp.CurrentSlot, gridItem.Size)};
        SetComponent(bgpe, newPos);
        Transform wireframe = GhostBuilder.WireframeDisplay.transform;
        wireframe.position = Grid.PositionFromCell(gridLtw, bgp.CurrentSlot, gridItem.Size);
        wireframe.rotation = gridLtw.Rotation;
        wireframe.localScale = new float3(1,1,1) * gridItem.Size;

        if (Input.GetMouseButtonDown(0))
        {
            EntityManager.DestroyEntity(GetSingletonEntity<GhostPlacementBuilder>());
            var e = World.GetExistingSystem<GhostPlacementSystem>().Build(gridEntity, false);
            if(e != Entity.Null)
                SetComponent(e, newPos);
        }
    }
}
