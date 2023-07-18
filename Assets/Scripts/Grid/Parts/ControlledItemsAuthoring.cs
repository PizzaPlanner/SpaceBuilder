using System;
using Unity.Entities;
using UnityEngine;

public struct ControlledItems : IBufferElementData
{
    public Entity Target;
    public ToggleType Type;
}

public class ControlledItemsAuthoring : MonoBehaviour
{
    public GameObject[] Doors;
    public ElectricityNetworkConnectionAuthoring[] Ports;

    class Baker : Baker<ControlledItemsAuthoring>
    {
        public override void Bake(ControlledItemsAuthoring authoring)
        {
        var buffer = AddBuffer<ControlledItems>();
        foreach (var door in authoring.Doors)
            buffer.Add(new ControlledItems {Target = GetEntity(door), Type = ToggleType.Door});
        foreach (var port in authoring.Ports)
            buffer.Add(new ControlledItems {Target = GetEntity(port), Type = ToggleType.PowerPort});

        foreach(var item in buffer)
            if(item.Target == Entity.Null)
                Debug.LogError($"ControlledItems not configured correctly for {authoring.gameObject.name}");
        }
    }

    private void OnValidate()
    {
        if(Ports != null)
            foreach(var port in Ports)
                if(port != null && !port.IsPort)
                    Debug.LogError($"{gameObject.name} reference a ElectricityNetworkConnection that is not a Port. This is not allowed!", this);
    }
}
