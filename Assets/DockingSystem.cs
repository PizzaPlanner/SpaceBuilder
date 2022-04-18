using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

[UpdateBefore(typeof(TransformSystemGroup))]
public class DockingSystem : SystemBase
{
    struct OriginAnchor : IComponentData { }
    public Entity Origin;
    private VisualElement swapRoot;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        Origin = EntityManager.CreateEntity(typeof(LocalToWorld), typeof(OriginAnchor));
        SetComponent(Origin, new LocalToWorld{Value = float4x4.identity});
    }

    protected override void OnStartRunning()
    {
        swapRoot = UIDocument.FindObjectOfType<UIDocument>().rootVisualElement.Q("DockInfo");
    }

    protected override void OnUpdate()
    {
        var entity = GetSingletonEntity<LocalPlayer>();
        var grids = GetBuffer<CloseGrids>(entity);

        if (grids.Length == 0)
        {
            Dock(entity, Origin);
            swapRoot.Q("Station", "dockingbutton").Q<Label>().text = "<None>";
            swapRoot.Q("Station", "dockingbutton").SetEnabled(false);
            swapRoot.Q("Swap").SetEnabled(false);
        }
        else
        {
            swapRoot.Q("Station", "dockingbutton").Q<Label>().text = GetComponent<Name>(grids[0].Grid).Value.ToString();
            swapRoot.Q("Station", "dockingbutton").SetEnabled(true);
            swapRoot.Q("Swap").SetEnabled(true);
        }

        if (!Input.GetKeyDown(KeyCode.G))
            return;

        var dockedGrid = GetComponent<Parent>(entity).Value == Origin && grids.Length > 0 ? grids[0].Grid : Origin;
        Dock(entity, dockedGrid);
    }

    void Dock(Entity entity, Entity newParent)
    {
        var playerLtw = GetComponent<LocalToWorld>(entity);
        var newParentLtw = GetComponent<LocalToWorld>(newParent);

        // World --> Parent
        var newLocalPos = math.mul(math.inverse(newParentLtw.Rotation), playerLtw.Position);
        var newLocalRot = math.mul(math.inverse(newParentLtw.Rotation), playerLtw.Rotation);

        SetComponent(entity, new Parent{Value = newParent});
        SetComponent(entity, new Translation{Value = newLocalPos});
        SetComponent(entity, new Rotation{Value = newLocalRot});

        swapRoot.Q(newParent != Origin ? "System" : "Station", "dockingbutton").RemoveFromClassList("dockingbutton-on");
        swapRoot.Q(newParent == Origin ? "System" : "Station", "dockingbutton").AddToClassList("dockingbutton-on");
    }
}