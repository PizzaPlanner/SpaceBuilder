using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public partial class InventoryInteractionSystem : SystemBase
{
    private InventoryElement element;

    private bool initialized;

    protected override void OnStartRunning()
    {
        if (!initialized)
        {
            element = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<InventoryElement>("RemoteBag");
            element.Container = Entity.Null;
            element.Subset = int2.zero;
            element.OnDragDrop = World.GetExistingSystem<InventoryUISystem>().OnDragDrop;
            var he = EntityManager.CreateEntity(typeof(InventoryUI));
            EntityManager.SetComponentData(he, new InventoryUI {Container = element});
            initialized = true;
        }

        element.style.display = DisplayStyle.Flex;
    }

    protected override void OnUpdate()
    {
        var player = GetSingletonEntity<LocalPlayer>();
        var elm = element;
        Entities
            .ForEach((Entity e, in DynamicBuffer<Inventory> inv, in Interacted interact) =>
            {
                if (interact.Interactor != player)
                    return;

                elm.Container = e;
            }).WithoutBurst().Run();
    }
}
