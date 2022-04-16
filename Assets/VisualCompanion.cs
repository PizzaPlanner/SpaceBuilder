using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[GenerateAuthoringComponent]
public class VisualCompanionPrefab : IComponentData
{
    public GameObject Prefab;
}

public class VisualCompanion : IComponentData
{
    public GameObject Target;
}

public class VisualCompanionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.WithNone<VisualCompanion>().ForEach((Entity e, VisualCompanionPrefab prefab) =>
        {
            var inst = GameObject.Instantiate(prefab.Prefab);
            EntityManager.RemoveComponent<VisualCompanionPrefab>(e);
            EntityManager.AddComponentData(e, new VisualCompanion {Target = inst});
        }).WithoutBurst().WithStructuralChanges().Run();

        Entities.ForEach((VisualCompanion vc, in LocalToWorld ltw) =>
        {
            vc.Target.transform.position = ltw.Position;
            vc.Target.transform.rotation = ltw.Rotation;
        }).WithoutBurst().Run();
    }
}
