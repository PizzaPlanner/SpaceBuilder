using System;
using Unity.Entities;

public struct GridItem : IComponentData
{
    [Flags]
    public enum Side : byte
    {
        Up = 1<<0,
        Down = 1<<1,
        Right = 1<<2,
        Left = 1<<3,
        Front = 1<<4,
        Back = 1<<5,
        Center = 1<<6
    }
    
    public BoundingBox Area;
    public bool Scalable;
    public bool IsWall;
}