using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public static class EntityHelpers
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int mod(this int i1, int i2)
    {
        return (i1 < 0 ? (i1 + i2) : i1) % i2;
    }

    public static Entity GetInChildren<T>(this EntityManager manager, Entity root) where T: IComponentData
    {
        if (manager.HasComponent<T>(root))
            return root;

        if(!manager.HasComponent<Child>(root))
            return Entity.Null;

        var buffer = manager.GetBuffer<Child>(root);
        if (buffer.IsEmpty)
            return Entity.Null;
        for (int i = 0; i < buffer.Length; i++)
        {
            var res = GetInChildren<T>(manager, buffer[i].Value);
            if (res != Entity.Null)
                return res;
        }

        return Entity.Null;
    }

    public static IEnumerable<Entity> GetAllChildren(this BufferFromEntity<Child> buffer, Entity root)
    {
        yield return root;
        if (buffer.HasComponent(root))
            foreach (var child in buffer[root])
            foreach (var item in GetAllChildren(buffer, child.Value))
                yield return item;
    }

    public static Entity FindParentWith<T>(this SystemBase system, Entity root) where T: IComponentData
    {
        var cdfe = system.GetComponentDataFromEntity<Parent>();
        while (root != Entity.Null && cdfe.HasComponent(root))
        {
            if (system.EntityManager.HasComponent<T>(root))
                return root;
            root = cdfe[root].Value;
        }

        return Entity.Null;
    }
}
