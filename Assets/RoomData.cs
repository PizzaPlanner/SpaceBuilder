using Unity.Entities;

public struct RoomData : IComponentData
{
    public int Id;
}

public struct ElectricityNetwork : IComponentData
{
    public float Power;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class ElectricitySystem : SystemBase
{
    protected override void OnUpdate()
    {
        var networks = GetComponentDataFromEntity<ElectricityNetwork>(false);
        Entities
            .WithName("ResetElectricityNetworks")
            .ForEach((ref ElectricityNetwork network) =>
            {
                network.Power = 0;
            }).Run();

        Entities
            .ForEach((in ElectricityProvider provider) =>
            {
                networks[provider.Network] = new ElectricityNetwork
                    {Power = networks[provider.Network].Power + provider.Value};
            }).Run();

        Entities
            .ForEach((in ElectricityConsumer consumer) =>
            {
                networks[consumer.Network] = new ElectricityNetwork
                    {Power = networks[consumer.Network].Power - consumer.Value};
            }).Run();

        Entities
            .ForEach((ref ElectricityConsumer consumer) =>
            {
                consumer.DemandSatisfied = networks[consumer.Network].Power >= 0;
            }).Run();

    }
}
