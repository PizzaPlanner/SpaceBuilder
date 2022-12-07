using Unity.Entities;

public struct LightSource : IComponentData
{
    public Entity Port;
    public float RequiredPower;
    public Entity Light;
}

public partial class LightSourceSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        Entities.ForEach((in LightSource light) =>
        {
            var port = GetComponent<ElectricityPort>(light.Port);
            port.Value = -light.RequiredPower;
            SetComponent(light.Port, port);
            if(port.NetworkOperational && HasComponent<Disabled>(light.Light))
                buffer.RemoveComponent<Disabled>(light.Light);
            else if(!port.NetworkOperational && !HasComponent<Disabled>(light.Light))
                buffer.AddComponent<Disabled>(light.Light);
        }).Schedule();
    }
}
