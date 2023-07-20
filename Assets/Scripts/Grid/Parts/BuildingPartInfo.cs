using Unity.Entities;
using UnityEngine;

public struct BuildingPartInfo : IBufferElementData
{
    public ulong Id;
    public BoundingBox Area;
    public RightAngleRotation Orientation;
    public uint BuildStatus;
    public Color Color;
}