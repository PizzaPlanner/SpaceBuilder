using Unity.Entities;

[GenerateAuthoringComponent]
public struct LightSource : IComponentData
{
    public Entity Port;
    public float RequiredPower;
    public Entity Light;
}

public partial class LightSourceSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        // RequireForUpdate(GetEntityQuery(typeof(ElectricitySystem.NetworkPowerStatusChanged)));
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
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
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
