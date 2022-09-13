using Rival;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public struct Docked : IComponentData
{
    public Entity Target;
    public quaternion TargetRotation;
    public quaternion Rotation;
    public float3 Position;
    public float3 TargetPosition;
}

public struct AlignUpVector : IComponentData
{
    public float Progress;
}

[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(CalculateCloseGridsSystem))]
public partial class DockingSystem : SystemBase
{
    private VisualElement swapRoot;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<ReferenceFrame>();
    }

    protected override void OnStartRunning()
    {
        swapRoot = UIDocument.FindObjectOfType<UIDocument>().rootVisualElement.Q("DockInfo");
    }

    protected override void OnUpdate()
    {
        var localPlayer = GetSingletonEntity<LocalPlayer>();
        var grids = GetBuffer<CloseGrids>(localPlayer);
        var isDocked = HasComponent<Docked>(GetSingletonEntity<LocalPlayer>());

        if (grids.Length == 0 || !EntityManager.Exists(grids[0].Grid))
        {
            if(isDocked)
                Dock(localPlayer, Entity.Null);
            swapRoot.Q("Station", "dockingbutton").Q<Label>().text = "<None>";
            swapRoot.Q("Station", "dockingbutton").SetEnabled(false);
            swapRoot.Q("Swap").SetEnabled(false);
        }
        else
        {
            var gridToDisplay = isDocked
                ? GetComponent<Docked>(GetSingletonEntity<LocalPlayer>()).Target
                : grids[0].Grid;
            swapRoot.Q("Station", "dockingbutton").Q<Label>().text = GetComponent<Name>(gridToDisplay).Value.ToString();
            var rooms = GetBuffer<Rooms>(grids[0].Grid);
            var playerLtw = GetComponent<LocalToWorld>(localPlayer);
            var playerGridCell = Grid.CellFromPosition(GetComponent<LocalToWorld>(gridToDisplay), playerLtw.Position);
            var isPlayerInRoom = false;
            foreach (var cell in rooms)
                isPlayerInRoom |= math.all(cell.Cell == playerGridCell) && !cell.IsExposedToSpace;
            
            swapRoot.Q("Station", "dockingbutton").SetEnabled(isPlayerInRoom);
            swapRoot.Q("Swap").SetEnabled(isPlayerInRoom);

            if(isPlayerInRoom && !isDocked)
                Dock(localPlayer, gridToDisplay);
            else if(!isPlayerInRoom && isDocked)
                Dock(localPlayer, Entity.Null);
        }
    }

    void Dock(Entity entity, Entity newParent)
    {
        swapRoot.Q(newParent != Entity.Null ? "System" : "Station", "dockingbutton").RemoveFromClassList("dockingbutton-on");
        swapRoot.Q(newParent == Entity.Null ? "System" : "Station", "dockingbutton").AddToClassList("dockingbutton-on");
        if (HasComponent<KinematicCharacterBody>(entity))
        {
            var body = GetComponent<KinematicCharacterBody>(entity);
            body.ParentEntity = newParent;
            SetComponent(entity, body);
        }

        if (newParent != Entity.Null && HasComponent<Grid>(newParent))
        {
            var parentLtw = GetComponent<LocalToWorld>(newParent);
            var ltw = GetComponent<LocalToWorld>(entity);
            var ltp = new LocalToWorld{Value = math.mul(math.inverse(parentLtw.Value), ltw.Value) };
            var docked = new Docked
            {
                Target = newParent,
                Rotation = ltp.Rotation,
                Position = ltp.Position,
                TargetPosition = parentLtw.Position,
                TargetRotation = parentLtw.Rotation
            };

            if (!HasComponent<Docked>(entity))
            {
                EntityManager.AddComponentData(entity, docked);
                // EntityManager.AddComponentData(entity, new Parent { Value = newParent });
                // EntityManager.AddComponentData(entity, new LocalToParent());
            }
            else
            {
                EntityManager.SetComponentData(entity, docked);
                // EntityManager.SetComponentData(entity, new Parent { Value = newParent });
                // EntityManager.SetComponentData(entity, new LocalToParent());
            }
            
            // if (!HasComponent<AlignUpVector>(entity))
            //     EntityManager.AddComponent<AlignUpVector>(entity);
        }
        else
        {
            if (HasComponent<Docked>(entity))
            {
                var docked = GetComponent<Docked>(entity);
                var oldParent = docked.Target;
                var parentLtg = GetComponent<LocalToGalaxy>(oldParent);
                var parentRotation = GetComponent<LocalToWorld>(oldParent).Rotation;
                SetComponent(entity, LocalToGalaxy.OffsetBy(parentLtg, math.mul(parentRotation, docked.Position)));
                
                EntityManager.RemoveComponent<Docked>(entity);
                // EntityManager.RemoveComponent<Parent>(entity);
                // EntityManager.RemoveComponent<LocalToParent>(entity);
            }
            if(HasComponent<AlignUpVector>(entity))
                EntityManager.RemoveComponent<AlignUpVector>(entity);
        }
    }
}

[UpdateBefore(typeof(MovePlayerSystem))]
public partial class RotateTowardsDockedLtwSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        var deltaTime = Time.DeltaTime;
        Entities
            .ForEach((Entity e, ref AlignUpVector up, ref Docked docked) =>
            {
                var otherLtw = GetComponent<LocalToWorld>(docked.Target);
                var myLtw = GetComponent<LocalToWorld>(e);
                var forward = myLtw.Forward;
                forward.y = 0;
                var target = quaternion.LookRotation(forward, math.mul(otherLtw.Rotation, math.up()));
                docked.Rotation = math.slerp(docked.Rotation, target, up.Progress);
                up.Progress += deltaTime;
                if(up.Progress >= 1)
                {
                    docked.Rotation = target;
                    buffer.RemoveComponent<AlignUpVector>(e);
                }
            }).Schedule();
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
