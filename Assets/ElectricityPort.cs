using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct ElectricityPort : IExchangeNetworkPort<float, ElectricityNetwork>
{
    public float value;
    public bool NetworkOperational;
    public float Value => value;
}

public struct ElectricityNetwork : IExchangeNetwork
{
    public uint Id;
    public float Power;
}

public struct ElectricityNetworkConnection : IExchangeNetworkConnector<ElectricityNetwork>
{
    public GridItem.Side ConnectsAt;
    public uint Id;
    public GridItem.Side Sides
    {
        get => ConnectsAt;
        set => ConnectsAt = value;
    }
}
