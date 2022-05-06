using System;
using Unity.Entities;
using Unity.Physics.Authoring;
using UnityEngine;

public struct ControlledItems : IBufferElementData
{
    public Entity Target;
    public ToggleType Type;
}

public class ControlledItemsAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public PhysicsBodyAuthoring[] Doors;
    public ElectricityNetworkConnectionAuthoring[] Ports;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var buffer = dstManager.AddBuffer<ControlledItems>(entity);
        foreach (var door in Doors)
            buffer.Add(new ControlledItems {Target = conversionSystem.GetPrimaryEntity(door), Type = ToggleType.Door});
        foreach (var port in Ports)
            buffer.Add(new ControlledItems {Target = conversionSystem.GetPrimaryEntity(port), Type = ToggleType.PowerPort});

        foreach(var item in buffer)
            if(item.Target == Entity.Null)
                Debug.LogError($"A Target of ControlledItems ({entity}) is not set");
    }

    private void OnValidate()
    {
        if(Ports != null)
            foreach(var port in Ports)
                if(port != null && !port.IsPort)
                    Debug.LogError($"{gameObject.name} reference a ElectricityNetworkConnection that is not a Port. This is not allowed!", this);
    }
}
