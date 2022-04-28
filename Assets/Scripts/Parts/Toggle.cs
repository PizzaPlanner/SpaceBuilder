using Unity.Entities;
using Unity.Physics;

public enum ToggleType
{
    PowerPort,
    Door
}

[GenerateAuthoringComponent]
public struct Toggle : IComponentData
{
    public bool On;
    public Entity Target;
    public Entity OnVisual;
    public Entity OffVisual;
    public ToggleType Type;
}

public partial class ToggleSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithAll<Interacted>()
            .ForEach((ref Toggle toggle) =>
            {
                toggle.On = !toggle.On;
                if (toggle.Type == ToggleType.PowerPort)
                {
                    var port = GetComponent<ElectricityPort>(toggle.Target);
                    port.On = toggle.On;
                    SetComponent(toggle.Target, port);
                } else if (toggle.Type == ToggleType.Door)
                {
                    if (toggle.On && !HasComponent<Disabled>(toggle.Target))
                    {
                        EntityManager.RemoveComponent<PhysicsWorldIndex>(toggle.Target);
                        EntityManager.AddComponent<Disabled>(toggle.Target);
                    }
                    else if (!toggle.On && HasComponent<Disabled>(toggle.Target))
                    {
                        // EntityManager.AddComponent<PhysicsWorldIndex>(toggle.Target);
                        EntityManager.RemoveComponent<Disabled>(toggle.Target);
                    }

                }

                var dOn = toggle.On ? toggle.OnVisual : toggle.OffVisual;
                var dOff = toggle.On ? toggle.OffVisual : toggle.OnVisual;
                if (HasComponent<Disabled>(dOn))
                    EntityManager.RemoveComponent<Disabled>(dOn);
                if (!HasComponent<Disabled>(dOff))
                    EntityManager.AddComponent<Disabled>(dOff);
            }).WithoutBurst().WithStructuralChanges().Run();
    }
}
