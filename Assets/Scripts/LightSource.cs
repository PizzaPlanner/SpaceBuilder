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
        var buffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        foreach(var light in SystemAPI.Query<LightSource>())
        {
            var port = SystemAPI.GetComponentRW<ElectricityPort>(light.Port);
            port.ValueRW.Value = -light.RequiredPower;
            if(port.ValueRW.NetworkOperational && SystemAPI.HasComponent<Disabled>(light.Light))
                buffer.RemoveComponent<Disabled>(light.Light);
            else if(!port.ValueRW.NetworkOperational && !SystemAPI.HasComponent<Disabled>(light.Light))
                buffer.AddComponent<Disabled>(light.Light);
        }
    }
}
