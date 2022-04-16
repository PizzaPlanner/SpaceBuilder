using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[GenerateAuthoringComponent]
public struct BuildingPartPivot : IComponentData { }
public struct BuildingPartPivotGhost : IComponentData { }

[UpdateBefore(typeof(BuildingPlacementActivationSystem))]
public class RotatePivotSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (HasSingleton<BuildingGhostPlacement>())
        {
            var pivot = GetInChildren<BuildingPartPivot>(this, GetSingletonEntity<BuildingGhostPlacement>());
            var rotation = GetSingleton<BuildingGhostPlacement>().Rotation;
            EntityManager.SetComponentData(pivot, new Rotation{Value = rotation});
        }
    }

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
