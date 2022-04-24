using System;
using Unity.Entities;
using Unity.Mathematics;

public interface IExchangeNetworkConnector<T> : IComponentData
    where T : IExchangeNetwork { }

public interface IExchangeNetwork : IComponentData
{

}

public interface IExchangeNetworkPort<TNetwork> : IComponentData
    where TNetwork: IExchangeNetwork { }
