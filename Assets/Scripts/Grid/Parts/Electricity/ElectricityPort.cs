using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct ElectricityPort : IExchangeNetworkPort<ElectricityNetwork>
{
    public float Value;
    public bool NetworkOperational;
    public bool On;
}

public struct ElectricityNetwork : IExchangeNetwork
{
    public float Power;
}

public struct ElectricityNetworkConnection : IExchangeNetworkConnector<ElectricityNetwork> { }
