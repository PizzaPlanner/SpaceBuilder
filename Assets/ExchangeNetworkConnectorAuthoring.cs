using Unity.Entities;
using UnityEngine;

public abstract class ExchangeNetworkConnectorAuthoring<TBase, TPort, TConnection, TValueType> : MonoBehaviour, IConvertGameObjectToEntity
    where TBase : IExchangeNetwork
    where TValueType: unmanaged
    where TPort : IExchangeNetworkPort<TValueType, TBase>
    where TConnection : unmanaged, IExchangeNetworkConnector<TBase>
{
    public bool IsPort;
    public GridItem.Side Sides;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var comp = default(TConnection);
        comp.Sides = Sides;
        dstManager.AddComponentData(entity, comp);
        if (IsPort)
        {
            dstManager.AddComponent<TPort>(entity);
        }
    }

    
}
