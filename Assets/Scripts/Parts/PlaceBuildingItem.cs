using System;
using Unity.Entities;

public struct BuildableBuffer : IBufferElementData
{
    public long PrefabId;
}

public struct SelectedItem : IComponentData
{

}
