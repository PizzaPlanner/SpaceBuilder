using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace Grid.Logic
{
    [UpdateInGroup(typeof(LogicSystemGroup), OrderFirst = true)]
    partial struct PropagateLogicSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RoomsLookup>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (inputs, parent) in
                     SystemAPI.Query<DynamicBuffer<LogicPort>, Parent>().WithAll<GridItem>())
            {
                var inputsRW = inputs;
                for (var i = 0; i < inputs.Length; i++)
                {
                    var port = inputs[i];
                    if (port.Connection.FromEntity == Entity.Null)
                        continue;

                    if(!SystemAPI.HasBuffer<LogicPort>(port.Connection.FromEntity) || !port.CanWrite)
                        port.Connection.FromEntity = Entity.Null;
                    else
                    {
                        var fromBuffer = SystemAPI.GetBuffer<LogicPort>(port.Connection.FromEntity);
                        if (fromBuffer.Length <= port.Connection.FromPort || !fromBuffer[port.Connection.FromPort].CanRead)
                            port.Connection.FromEntity = Entity.Null;
                        else
                            port.SetFromOutput(fromBuffer[port.Connection.FromPort]);
                    }

                    inputsRW[i] = port;
                }
            }
        }
    }
}