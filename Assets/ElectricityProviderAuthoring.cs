using System;
using Unity.Entities;
using Unity.Mathematics;

public interface IExchangeNetworkConnector<T> : IComponentData
    where T : IExchangeNetwork
{
    public Entity Network { get; }
    public GridItem.Side Sides { get; set; }
}

public interface IExchangeNetwork : IComponentData
{

}

public interface IExchangeNetworkPort<out TValue, TNetwork> : IComponentData
    where TNetwork: IExchangeNetwork
    where TValue: unmanaged
{
    TValue Value { get; }
}
