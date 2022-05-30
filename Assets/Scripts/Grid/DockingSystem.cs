using System;
using Rival;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
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
public partial class DockingSystem : SystemBase
{
    private VisualElement swapRoot;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnStartRunning()
    {
        swapRoot = UIDocument.FindObjectOfType<UIDocument>().rootVisualElement.Q("DockInfo");
    }

    protected override void OnUpdate()
    {
        var entity = GetSingletonEntity<LocalPlayer>();
        var grids = GetBuffer<CloseGrids>(entity);
        var isDocked = HasComponent<Docked>(GetSingletonEntity<LocalPlayer>());

        if (grids.Length == 0)
        {
            if(isDocked)
                Dock(entity, Entity.Null);
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
            swapRoot.Q("Station", "dockingbutton").SetEnabled(true);
            swapRoot.Q("Swap").SetEnabled(true);
        }

        if (!Input.GetKeyDown(KeyCode.G))
            return;

        var previousParent = HasComponent<Docked>(entity) ? GetComponent<Docked>(entity).Target : Entity.Null;
        var dockedGrid = previousParent == Entity.Null && grids.Length > 0 ? grids[0].Grid : Entity.Null;
        Dock(entity, dockedGrid);
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
            var docked = new Docked
            {
                Target = newParent,
                Rotation = ltw.Rotation,
                Position = ltw.Position - parentLtw.Position
            };

            if (!HasComponent<Docked>(entity))
                EntityManager.AddComponentData(entity, docked);
            else
                EntityManager.SetComponentData(entity, docked);
            if (!HasComponent<AlignUpVector>(entity))
                EntityManager.AddComponent<AlignUpVector>(entity);
        }
        else
        {

            if(HasComponent<Docked>(entity))
                EntityManager.RemoveComponent<Docked>(entity);
            if(HasComponent<AlignUpVector>(entity))
                EntityManager.RemoveComponent<AlignUpVector>(entity);

            var ltw = GetComponent<LocalToWorld>(entity);
            SetComponent(entity, new Translation{Value = ltw.Position});
            SetComponent(entity, new Rotation{Value = ltw.Rotation});
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
                var target = quaternion.LookRotation(forward, math.up());
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
