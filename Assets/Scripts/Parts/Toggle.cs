using Unity.Entities;
using Unity.Physics;
using UnityEngine;

public enum ToggleType
{
    PowerPort,
    Door,
    Toggle,
    ThrusterForward,
    ThrusterBackward,
    ThrusterUp,
    ThrusterDown,
    ThrusterLeft,
    ThrusterRight,
}

[GenerateAuthoringComponent]
public struct Toggle : IComponentData
{
    public bool On;
    public Entity OnVisual;
    public Entity OffVisual;
}

public partial class ToggleSystem : SystemBase
{
    private EntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        Entities
            .WithAll<Interacted>()
            .ForEach((ref Toggle toggle) =>
            {
                toggle.On = !toggle.On;
            }).Run();

        Entities
            .WithChangeFilter<Toggle>()
            .ForEach((ref DynamicBuffer<ControlledItems> items, in Toggle toggle) => {
                foreach (var item in items)
                {
                    if (item.Type == ToggleType.PowerPort)
                    {
                        var port = GetComponent<ElectricityPort>(item.Target);
                        port.On = toggle.On;
                        SetComponent(item.Target, port);
                    }
                    else if (item.Type == ToggleType.Door)
                    {
                        if (toggle.On && !HasComponent<Disabled>(item.Target))
                        {
                            buffer.RemoveComponent<PhysicsWorldIndex>(item.Target);
                            buffer.AddComponent<Disabled>(item.Target);
                        }
                        else if (!toggle.On && HasComponent<Disabled>(item.Target))
                        {
                            buffer.AddComponent(item.Target, ComponentType.ReadOnly<PhysicsWorldIndex>());
                            buffer.RemoveComponent<Disabled>(item.Target);
                        }
                    }
                }
            }).Run();
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}

[UpdateAfter(typeof(ToggleSystem))]
public partial class ToggleVisualsUpdateSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithChangeFilter<Toggle>()
            .ForEach((in Toggle toggle) =>
            {
                var dOn = toggle.On ? toggle.OnVisual : toggle.OffVisual;
                var dOff = toggle.On ? toggle.OffVisual : toggle.OnVisual;
                if (dOn == Entity.Null || dOff == Entity.Null)
                    return;
                if (HasComponent<Disabled>(dOn))
                    EntityManager.RemoveComponent<Disabled>(dOn);
                if (!HasComponent<Disabled>(dOff))
                    EntityManager.AddComponent<Disabled>(dOff);
            }).WithoutBurst().WithStructuralChanges().Run();
    }
}
