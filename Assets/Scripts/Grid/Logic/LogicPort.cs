using System.Runtime.InteropServices;
using Unity.Entities;

namespace Grid.Logic
{
    public enum LogicAccess : byte
    {
        Read = 0,
        Write = 1,
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
        Up,
        Back,
        Left,
        Down,
        XPosition,
        YPosition,
        ZPosition,
        XSector,
        YSector,
        ZSector,
        Level,
        In,
        Out,
    }

    public static class PortTypeExtensions
    {
        public static bool IsFractionalType(this PortType type) => type switch
        {
            PortType.Ratio => true,
            PortType.Pitch => true,
            PortType.Yaw => true,
            PortType.Roll => true,
            PortType.Forward => true,
            PortType.Up => true,
            PortType.Right => true,
            PortType.Back => true,
            PortType.Left => true,
            PortType.Down => true,
            PortType.XPosition => true,
            PortType.YPosition => true,
            PortType.ZPosition => true,
            _ => false
        };
    }
    
    public struct LogicPort : IBufferElementData
    {
        public bool CanRead => Access == LogicAccess.Read;
        public bool CanWrite => Access == LogicAccess.Write;
        
        public void SetFromOutput(LogicPort fromPort)
        {
            if (!fromPort.CanRead || !CanWrite)
                return;
            
            SetDirect(fromPort.Data);
        }

        public void SetDirect(LogicData data)
        {
            if (data.UseFractions && double.IsNaN(data.AsDouble))
                return;

            if(Data.UseFractions)
                Data.AsDouble = data.UseFractions ? data.AsDouble : data.AsLong;
            else
                Data.AsLong = data.UseFractions ? (long)data.AsDouble : data.AsLong;
        }

        public PortType Type;
        public LogicAccess Access;
        public LogicInput Connection;
        public LogicData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct LogicData
    {
        [FieldOffset(0)]
        public bool UseFractions;
        [FieldOffset(1)]
        public double AsDouble;
        [FieldOffset(1)]
        public long AsLong;

        public static LogicData Double(double value)
        {
            return new LogicData
            {
                UseFractions = true,
                AsDouble = value
            };
        }
        
        public static LogicData Long(long value)
        {
            return new LogicData
            {
                UseFractions = false,
                AsLong = value
            };
        }
    }
}