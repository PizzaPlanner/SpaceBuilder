using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(CalculateCloseGridsSystem))]
public partial class DockingSystem : SystemBase
{
    private VisualElement swapRoot;
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<ReferenceFrame>();
    }

    protected override void OnStartRunning()
    {
        swapRoot = UIDocument.FindObjectOfType<UIDocument>().rootVisualElement.Q("DockInfo");
    }

    protected override void OnUpdate()
    {
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        if (SystemAPI.HasComponent<AlignUpVector>(localPlayer))
            return;
        
        var grids = SystemAPI.GetBuffer<CloseGrids>(localPlayer);
        var isDocked = SystemAPI.HasComponent<Docked>(SystemAPI.GetSingletonEntity<LocalPlayer>());

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
                ? SystemAPI.GetComponent<Docked>(SystemAPI.GetSingletonEntity<LocalPlayer>()).Target
                : grids[0].Grid;
            swapRoot.Q("Station", "dockingbutton").Q<Label>().text = SystemAPI.GetComponent<Name>(gridToDisplay).Value.ToString();
            var rooms = SystemAPI.GetBuffer<Rooms>(grids[0].Grid);
            var playerLtw = SystemAPI.GetComponent<LocalToWorld>(localPlayer);
            var playerGridCell = GridStructure.CellFromPosition(SystemAPI.GetComponent<LocalToWorld>(gridToDisplay), playerLtw.Position);
            var isPlayerInRoom = false;
            foreach (var room in rooms)
                isPlayerInRoom |= room.Room.Contains(playerGridCell);
            Debug.Log($"Check is {playerGridCell} in {grids[0].Grid}? > {isPlayerInRoom}");
            
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
        // if (HasComponent<KinematicCharacterBody>(entity))
        // {
        //     var body = GetComponent<KinematicCharacterBody>(entity);
        //     body.ParentEntity = newParent;
        //     SetComponent(entity, body);
        // }

        if (newParent != Entity.Null && SystemAPI.HasComponent<GridStructure>(newParent))
        {
            var parentLtw = SystemAPI.GetComponent<LocalToWorld>(newParent);
            var ltw = SystemAPI.GetComponent<LocalToWorld>(entity);
            var ltp = new LocalToWorld{Value = math.mul(math.inverse(parentLtw.Value), ltw.Value) };
            var docked = new Docked
            {
                Target = newParent,
                Rotation = ltp.Rotation,
                Position = ltp.Position,
                TargetPosition = parentLtw.Position,
                TargetRotation = parentLtw.Rotation
            };

            if (!SystemAPI.HasComponent<Docked>(entity))
            {
                EntityManager.AddComponentData(entity, docked);
            }
            else
            {
                EntityManager.SetComponentData(entity, docked);
            }
            
            if (!SystemAPI.HasComponent<AlignUpVector>(entity))
                EntityManager.AddComponent<AlignUpVector>(entity);
        }
        else
        {
            if (!SystemAPI.HasComponent<Docked>(entity)) return;
            
            var docked = SystemAPI.GetComponent<Docked>(entity);
            var oldParent = docked.Target;
            var parentLtg = SystemAPI.GetComponent<LocalToGalaxy>(oldParent);
            var parentRotation = SystemAPI.GetComponent<LocalToWorld>(oldParent).Rotation;
            SystemAPI.SetComponent(entity, parentLtg + math.mul(parentRotation, docked.Position));
                
            EntityManager.RemoveComponent<Docked>(entity);
        }
    }
}

[UpdateBefore(typeof(MovePlayerSystem))]
public partial class RotateTowardsDockedLtwSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var deltaTime = World.Time.DeltaTime;
        Entities
            .ForEach((Entity e, ref AlignUpVector up, ref Docked docked) =>
            {
                var forward = math.forward(docked.Rotation);
                var current = quaternion.LookRotationSafe(forward, math.up());
                var targetForward = math.cross(math.cross(math.up(), forward), math.up());
                var target = quaternion.LookRotationSafe(targetForward, math.up());
                docked.Rotation = math.slerp(current, target, up.Progress);
                up.Progress += deltaTime;
                if(up.Progress >= 1)
                {
                    docked.Rotation = target;
                    buffer.RemoveComponent<AlignUpVector>(e);
                }
            }).Schedule();
    }
}
