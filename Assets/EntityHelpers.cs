using Unity.Entities;
using Unity.Transforms;

public static class EntityHelpers
{

    public static Entity GetInChildren<T>(SystemBase system, Entity root) where T : unmanaged, IComponentData
    {
        if (system.EntityManager.HasComponent<T>(root))
            return root;
        var buffer = system.GetBuffer<Child>(root);
        if (buffer.IsEmpty)
            return Entity.Null;
        for (int i = 0; i < buffer.Length; i++)
        {
            var res = GetInChildren<T>(system, buffer[i].Value);
            if (res != Entity.Null)
                return res;
        }

        return Entity.Null;
    }
}