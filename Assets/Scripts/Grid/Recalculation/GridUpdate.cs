using Unity.Entities;
using UnityEngine;

public struct GridUpdate : IBufferElementData
{
    public enum UpdateType
    {
        Add,
        Remove,
        Change
    }

    public UpdateType Type;
    public IntAABB Area;
}