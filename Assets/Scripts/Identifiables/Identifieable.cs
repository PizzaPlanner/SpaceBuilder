using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

[Flags]
public enum IdTag : byte
{
    Generic   = 0,
    Item      = 1,
    Tool      = 3, // All Tools are also Items
    Recipe    = 4,
    Buildable = 8,
}

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
    public IdTag Tags => IdentifiableMappingSystem.TagsById.TryGetValue(this, out var e) ? e : IdTag.Generic;
    
    [BurstDiscard]
    public Texture2D Icon => IdentifiableMappingSystem.IconById.TryGetValue(this, out var v) ? v : null;
    public FixedString64Bytes DefaultName => IdentifiableMappingSystem.NameById.TryGetValue(this, out var n) ? n : default;
    public static NativeArray<FixedString64Bytes> AllNames => IdentifiableMappingSystem.NameById.GetValueArray(Allocator.Temp);
    public static NativeArray<Identifieable> All => IdentifiableMappingSystem.EntitiesById.GetKeyArray(Allocator.Temp);

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
    
    public NativeList<IdentifiableLink> References
    {
        get
        {
            var allRefs = IdentifiableMappingSystem.IdRelations;
            var result = new NativeList<IdentifiableLink>(Allocator.Temp);
            foreach(var refs in allRefs)
                if(refs.From == this)
                    result.Add(refs);
            return result;
        }
    }
}

// Only used for baking and initial loading.
public struct DefaultIdentifiableLink : IBufferElementData
{
    public Identifieable To;
    public Identifieable From;
    public IdLinkType RelationTo;
    public IdLinkType RelationFrom;
}

// Only used for baking and initial loading.
[Serializable]
public struct PreIdentifiable : IComponentData
{
    public FixedString64Bytes Name;
    public Identifieable Id;
    public IdTag Tags;
}