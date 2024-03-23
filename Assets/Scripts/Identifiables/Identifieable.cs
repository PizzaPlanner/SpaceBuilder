using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

public partial struct Identifieable : IComponentData, IEquatable<Identifieable>
{
    public uint ItemId;

    public Identifieable(ulong guid)
    {
        ItemId = FromULong(guid);
    }

    static uint FromULong(ulong guid)
    {
        var shortened = guid >> 32;
        if(shortened == 0)
            return (uint)guid;
        else
            return (uint)shortened;
    }
    
    public static bool operator ==(Identifieable left, Identifieable right)
    {
        return left.ItemId == right.ItemId;
    }

    public static bool operator !=(Identifieable left, Identifieable right)
    {
        return !(left == right);
    }
    
    // Override Equals and GetHashCode for consistency
    public override bool Equals(object obj)
    {
        return obj is Identifieable other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ItemId.GetHashCode();
    }

    public bool Equals(Identifieable other)
    {
        return ItemId == other.ItemId;
    }
}

public partial struct Identifieable
{
    public Entity AsPrefab => IdentifiableMappingSystem.EntitiesById.TryGetValue(this, out var e) ? e : Entity.Null;
    
    [BurstDiscard]
    public Texture2D Icon => IdentifiableMappingSystem.IconById.TryGetValue(this, out var v) ? v : null;
    public FixedString64Bytes DefaultName => IdentifiableMappingSystem.NameById.TryGetValue(this, out var n) ? n : default;
    public static NativeArray<FixedString64Bytes> AllNames => IdentifiableMappingSystem.NameById.GetValueArray(Allocator.Temp); 

    public static Identifieable FromName(FixedString64Bytes defaultName)
    {
        var kvp = IdentifiableMappingSystem.NameById.GetKeyValueArrays(Allocator.Temp);
        for (var i = 0; i < kvp.Values.Length; i++)
        {
            var name = kvp.Values[i];
            if (name == defaultName)
                return kvp.Keys[i];
        }
        Debug.LogError($"No Identifiable with Name '{defaultName}'");
        return default;
    }

    public static bool TryGet(string hint, out Identifieable id)
    {
        id = default;
        if (ulong.TryParse(hint, out var ul))
        {
            id = new Identifieable(ul);
            return true;
        }
        id = FromName(hint);
        return id != default;
    }
}

// Only used for baking and initial loading.
public struct DefaultName : IComponentData
{
    public FixedString64Bytes Value;
}