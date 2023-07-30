using Grid.Essentials;
using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Grid.Logic
{
    [System.Flags]
    public enum LogicAccess : byte
    {
        Read = 1,
        Write = 2,
        ReadWrite = Read | Write
    }

    public enum PortType : byte
    {
        On,
        Open,
        Toggle,
        Ratio,
        Pitch,
        Yaw,
        Roll,
        Forward,
        Right,
        Up
    }

    public struct LogicPort : IBufferElementData
    {
        public bool CanRead => (Access & LogicAccess.Read) != 0;
        public bool CanWrite => (Access & LogicAccess.Write) != 0;
        
        public void SetFromOutput(LogicPort fromPort)
        {
            if (!fromPort.CanRead || !CanWrite)
                return;
            
            SetDirect(fromPort.Data);
        }

        public void SetDirect(float data)
        {
            if (float.IsNaN(data))
                return;
            
            Data = data;
        }
        
        public PortType Type;
        public LogicAccess Access;
        public float Data;
    }

    public struct LogicTarget : IBufferElementData
    {
        public byte TargetInPort;
        public byte MyOutPort;
        public Cell TargetOriginCell;

        public Entity GetTarget(Entity grid, RoomsLookup rooms) => 
            rooms.LogicLookup.TryGetValue((grid, TargetOriginCell), out var e) ? e : Entity.Null;
    }

    partial struct WriteLogicOutputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RoomsLookup>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rooms = SystemAPI.GetSingleton<RoomsLookup>();
            foreach (var (outputs, targets, parent) in
                     SystemAPI.Query<DynamicBuffer<LogicPort>, DynamicBuffer<LogicTarget>, Parent>().WithAll<GridItem>())
            {
                foreach (var target in targets)
                {
                    var inputEntity = target.GetTarget(parent.Value, rooms);
                    if(inputEntity == Entity.Null)
                        continue;

                    var inputs = SystemAPI.GetBuffer<LogicPort>(inputEntity);
                    if (inputs.Length <= target.TargetInPort)
                        continue;
                    
                    var input = inputs[target.TargetInPort];
                    input.SetFromOutput(outputs[target.MyOutPort]);
                    inputs[target.TargetInPort] = input;
                }
            }
        }
    }
}