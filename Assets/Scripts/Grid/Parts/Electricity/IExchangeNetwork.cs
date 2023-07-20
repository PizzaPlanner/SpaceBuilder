using Unity.Entities;

public interface IExchangeNetworkConnector<T> : IComponentData
    where T : IExchangeNetwork { }

public interface IExchangeNetwork : IComponentData
{

}

public interface IExchangeNetworkPort<TNetwork> : IComponentData
    where TNetwork: IExchangeNetwork { }
