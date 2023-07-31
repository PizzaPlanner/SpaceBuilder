using Grid.Logic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public partial class SteeringControllerSystem : SystemBase
{
    partial struct SteeringJob : IJobEntity
    {
        public void Execute(ref DynamicBuffer<LogicPort> ports, in SteeringController steering)
        {
            for (var i = 0; i < ports.Length; i++)
            {
                var port = ports[i];
                var valueToSet = port.Type switch
                {
                    PortType.Right => steering.Thrust.x,
                    PortType.Up => steering.Thrust.y,
                    PortType.Forward => steering.Thrust.z,
                    PortType.Pitch => steering.SteeringAngle.x,
                    PortType.Yaw => steering.SteeringAngle.y,
                    PortType.Roll => steering.SteeringAngle.z,
                    _ => float.NaN
                };
                port.SetDirect(LogicData.Double(valueToSet));
                ports[i] = port;
            }
        }
    }

    protected override void OnUpdate()
    {
        new SteeringJob().ScheduleParallel();
    }
}
