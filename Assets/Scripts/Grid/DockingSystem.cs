using System;
using Rival;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public struct Docked : IComponentData
{
    public Entity Target;
}

public struct AlignUpVector : IComponentData
{
    public float Progress;
}

[UpdateBefore(typeof(TransformSystemGroup))]
public partial class DockingSystem : SystemBase
{
    struct OriginAnchor : IComponentData { }
    public Entity Origin;
    private VisualElement swapRoot;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        Origin = EntityManager.CreateEntity(typeof(LocalToWorld), typeof(OriginAnchor));
        SetComponent(Origin, new LocalToWorld{Value = float4x4.identity});
    }

    protected override void OnStartRunning()
    {
        swapRoot = UIDocument.FindObjectOfType<UIDocument>().rootVisualElement.Q("DockInfo");
    }

    protected override void OnUpdate()
    {
        var entity = GetSingletonEntity<LocalPlayer>();
        var grids = GetBuffer<CloseGrids>(entity);

        if (grids.Length == 0)
        {
            Dock(entity, Origin);
            swapRoot.Q("Station", "dockingbutton").Q<Label>().text = "<None>";
            swapRoot.Q("Station", "dockingbutton").SetEnabled(false);
            swapRoot.Q("Swap").SetEnabled(false);
        }
        else
        {
            swapRoot.Q("Station", "dockingbutton").Q<Label>().text = GetComponent<Name>(grids[0].Grid).Value.ToString();
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
        // SetComponent(entity, new Parent{Value = newParent});
        // SetComponent(entity, new Translation{Value = newLocalPos});
        // SetComponent(entity, new Rotation{Value = newLocalRot});

        // swapRoot.Q(newParent != Origin ? "System" : "Station", "dockingbutton").RemoveFromClassList("dockingbutton-on");
        // swapRoot.Q(newParent == Origin ? "System" : "Station", "dockingbutton").AddToClassList("dockingbutton-on");
        var body = GetComponent<KinematicCharacterBody>(entity);
        body.ParentEntity = newParent;
        SetComponent(entity, body);
        if (newParent != Entity.Null && HasComponent<Grid>(newParent))
        {
            if (!HasComponent<Docked>(entity))
                EntityManager.AddComponentData(entity, new Docked{ Target = newParent });
            else
                EntityManager.SetComponentData(entity, new Docked{ Target = newParent });
            if (!HasComponent<AlignUpVector>(entity))
                EntityManager.AddComponent<AlignUpVector>(entity);
        }
        else
        {
            if(HasComponent<Docked>(entity))
                EntityManager.RemoveComponent<Docked>(entity);
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
            .ForEach((Entity e, ref Rotation r, ref AlignUpVector up, in Docked docked) =>
            {
                var otherLtw = GetComponent<LocalToWorld>(docked.Target);
                var myLtw = GetComponent<LocalToWorld>(e);
                if (up.Progress < 1)
                {
                    var localForwardNoY = myLtw.Forward;
                    localForwardNoY.y = 0;
                    var step = math.slerp(r.Value, quaternion.LookRotation(localForwardNoY, otherLtw.Up), up.Progress);
                    r.Value = step;
                    up.Progress += deltaTime;
                }
                else
                {
                    r.Value = quaternion.LookRotation(myLtw.Forward, otherLtw.Up);
                    buffer.RemoveComponent<AlignUpVector>(e);
                }
            }).Schedule();
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
