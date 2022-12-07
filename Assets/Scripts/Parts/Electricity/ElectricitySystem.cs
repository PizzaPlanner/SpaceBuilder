using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(GridRecalculationSystemGroup))]
public partial class ElectricitySystem : SystemBase
{
    public struct NetworkPowerStatusChanged : IComponentData { }

    protected override void OnUpdate()
    {
        var ports = GetComponentLookup<ElectricityPort>(false);
        var query = GetEntityQuery(typeof(ElectricityPort), typeof(GridItem));
        var portEntities = query.ToEntityArray(Allocator.TempJob);
        
        Entities
            .WithName("CalculateNetworkPower")
            .WithNone<NetworkPowerStatusChanged>()
            .WithAll<Grid>()
            .ForEach((Entity e, ref ElectricityNetwork network) =>
            {
                var prevPower = network.Power;
                network.Power = 0;
                for (int i = 0; i < portEntities.Length; i++)
                {
                    if (GetComponent<GridItem>(portEntities[i]).Grid != e)
                        continue;

                    var port = ports[portEntities[i]];
                    network.Power += port.On ? port.Value : 0;
                }

                if (network.Power == prevPower)
                    return;
                
                for (int i = 0; i < portEntities.Length; i++)
                {
                    if (GetComponent<GridItem>(portEntities[i]).Grid != e)
                        continue;

                    var port = ports[portEntities[i]];
                    port.NetworkOperational = network.Power >= 0;
                    ports[portEntities[i]] = port;
                }
            }).Schedule();
    }
}
