using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Transforms;

public static class EntityHelpers
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int mod(this int i1, int i2)
    {
        return (i1 < 0 ? (i1 + i2) : i1) % i2;
    }

    public static Entity GetInChildren<T>(this EntityManager manager, Entity root) where T : unmanaged, IComponentData
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
}
