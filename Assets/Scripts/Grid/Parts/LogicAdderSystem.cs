using Grid.Logic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct LogicAdder : IComponentData { }

[UpdateInGroup(typeof(LogicSystemGroup))]
public partial class LogicAdderSystem : SystemBase
{
    partial struct LogicAdderJob : IJobEntity
    {
        public void Execute(ref DynamicBuffer<LogicPort> ports, in LogicAdder adder)
        {
            var sum = ports[0].Data.AsLong + ports[1].Data.AsLong;
            var p = ports[2];
            p.Data.AsLong = sum;
            ports[2] = p;
        }
    }

    protected override void OnUpdate()
    {
        new LogicAdderJob { }.ScheduleParallel();
    }
}
