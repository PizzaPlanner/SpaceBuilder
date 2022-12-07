using Unity.Entities;

public struct InfinityElectricityGenerator : IComponentData
{
    public float Power;
    public Entity Port;
}

public partial class InfinityElectricityGeneratorSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((in InfinityElectricityGenerator ieg) =>
        {
            var port = GetComponent<ElectricityPort>(ieg.Port);
            port.Value = ieg.Power;
            SetComponent(ieg.Port, port);
        }).Schedule();
    }
}
