using Unity.Entities;

[GenerateAuthoringComponent]
public struct Toggle : IComponentData
{
    public bool On;
    public Entity Target;
    public Entity OnVisual;
    public Entity OffVisual;
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
                var port = GetComponent<ElectricityPort>(toggle.Target);
                port.On = toggle.On;
                SetComponent(toggle.Target, port);

                var dOn = toggle.On ? toggle.OnVisual : toggle.OffVisual;
                var dOff = toggle.On ? toggle.OffVisual : toggle.OnVisual;
                if (HasComponent<Disabled>(dOn))
                    EntityManager.RemoveComponent<Disabled>(dOn);
                if (!HasComponent<Disabled>(dOff))
                    EntityManager.AddComponent<Disabled>(dOff);
            }).WithoutBurst().WithStructuralChanges().Run();
    }
}
