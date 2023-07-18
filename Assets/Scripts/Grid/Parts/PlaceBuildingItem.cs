using System;
using Unity.Entities;

public struct BuildableBuffer : IBufferElementData
{
    public ulong PrefabId;
}

public struct SelectedItem : IComponentData
{

}
