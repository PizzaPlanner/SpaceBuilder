using Grid.Logic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct GalaxyCompass : IComponentData
{
    public int Level;
}

public partial class GalaxyCompassSystem : SystemBase
{
    partial struct ApplyCoordinatesJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LocalToGalaxy> ltgLookup;
        public void Execute(ref DynamicBuffer<LogicPort> ports, in GalaxyCompass compass, in Parent parent)
        {
            if (!ltgLookup.TryGetComponent(parent.Value, out var ltg))
                return;
            
            var ltgCoords = compass.Level switch
            {
                0 => ltg.LocalSector,
                1 => ltg.PlanetSector,
                2 => ltg.StarSector,
                _ => ltg.LightSector
            };
            
            for (var i = 0; i < ports.Length; i++)
            {
                var port = ports[i];
                var valueToSet = port.Type switch
                {
                    PortType.XPosition => LogicData.Double(ltg.LocalPosition.x),
                    PortType.YPosition => LogicData.Double(ltg.LocalPosition.y),
                    PortType.ZPosition => LogicData.Double(ltg.LocalPosition.z),
                    PortType.XSector => LogicData.Long(ltgCoords.x),
                    PortType.YSector => LogicData.Long(ltgCoords.y),
                    PortType.ZSector => LogicData.Long(ltgCoords.z),
                    _ => default
                };
                port.SetDirect(valueToSet);
                ports[i] = port;
            }
        }
    }

    protected override void OnUpdate()
    {
        new ApplyCoordinatesJob
        {
            ltgLookup = SystemAPI.GetComponentLookup<LocalToGalaxy>(true)
        }.ScheduleParallel();
    }
}
