using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(GridRecalculationSystem))]
[UpdateAfter(typeof(RecalculateElectricityNetworksSystem))]
public partial class ElectricitySystem : SystemBase
{
    private EndInitializationEntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        var prefabHashMap = BuildingPartManagementSystem.BuildingPartById;
        Entities
            .WithReadOnly(prefabHashMap)
            .WithName("ApplyElectricityConnectionRotation")
            .WithAll<ElectricityNetworkConnection>()
            .ForEach((Entity e, ref Rotation r, in Identifieable item, in UpdateRotation ur) =>
            {
                var prefab = prefabHashMap[item.ItemId];
                var prefabConnection = GetComponent<ElectricityNetworkConnection>(prefab);
                var myConnection = GetComponent<ElectricityNetworkConnection>(e);
                myConnection.ConnectsAt = prefabConnection.ConnectsAt.Rotate(ur.Value);
                SetComponent(e, myConnection);
                r.Value = ur.Value;
                buffer.RemoveComponent<UpdateRotation>(e);
            }).Run();
        bufferSystem.AddJobHandleForProducer(Dependency);

        Entities
            .WithName("ResetElectricityNetworks")
            .ForEach((ref ElectricityNetwork network) =>
            {
                network.Power = 0;
            }).Run();

        var networks = GetComponentDataFromEntity<ElectricityNetwork>(false);
        Entities
            .WithDisposeOnCompletion(networks)
            .ForEach((in ElectricityPort port, in ElectricityNetworkConnection connection) =>
            {
                if (connection.Network == Entity.Null)
                    return;
                networks[connection.Network] = new ElectricityNetwork
                    {Power = networks[connection.Network].Power + port.Value};
            }).Run();

    }
}
