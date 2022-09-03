using Unity.Entities;
using UnityEngine;

public abstract class ExchangeNetworkConnectorAuthoring<TBase, TPort, TConnection> : MonoBehaviour, IConvertGameObjectToEntity
    where TBase : IExchangeNetwork
    where TPort : IExchangeNetworkPort<TBase>
    where TConnection : unmanaged, IExchangeNetworkConnector<TBase>
{
    public bool IsPort;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var comp = default(TConnection);
        dstManager.AddComponentData(entity, comp);
        if (IsPort)
        {
            dstManager.AddComponent<TPort>(entity);
        }
    }
}
