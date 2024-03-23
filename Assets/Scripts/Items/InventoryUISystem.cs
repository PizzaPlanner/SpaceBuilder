using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class InventoryUISystem : SystemBase
{
    public struct Open : IComponentData { }

    VisualElement anchor;
    
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<Open>();
    }

    protected override void OnStartRunning()
    {
        anchor = GameObject.FindAnyObjectByType<UIDocument>().rootVisualElement.Q("InventoryRoot"); 
        anchor.style.display = DisplayStyle.Flex;
    }

    public void OnDragDrop(InventoryButton dragged, InventoryButton button)
    {
        var i1 = SystemAPI.GetBuffer<Inventory>(dragged.Container);
        var i2 = SystemAPI.GetBuffer<Inventory>(button.Container);

        var from = i1[dragged.Slot];
        var to = i2[button.Slot];

        i1[dragged.Slot] = to;
        i2[button.Slot] = from;
    }

    protected override void OnStopRunning()
    {
        anchor.style.display = DisplayStyle.None;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (ui, e) in SystemAPI.Query<InventoryElement>().WithEntityAccess())
        {
            ui.RemoveFromHierarchy();
            ecb.RemoveComponent<InventoryElement>(e);
        }
        foreach (var (ui, e) in SystemAPI.Query<CraftingWindow>().WithEntityAccess())
        {
            ui.RemoveFromHierarchy();
            ecb.RemoveComponent<CraftingWindow>(e);
        }
        ecb.Playback(EntityManager);
    }

    protected override void OnUpdate()
    {
        
        var noUIInvQuery = SystemAPI.QueryBuilder().WithAll<Inventory, Open>().WithNone<InventoryElement>().Build();
        var entities = noUIInvQuery.ToEntityArray(Allocator.Temp);
        foreach (var e in entities)
        {
            var window = new InventoryElement
            {
                Container = e,
                OnDragDrop = World.GetExistingSystemManaged<InventoryUISystem>().OnDragDrop
            };
            anchor.Add(window);
            EntityManager.AddComponentData(e, window);
        }
        
        var noUICraftQuery = SystemAPI.QueryBuilder().WithAll<Crafter, Open>().WithNone<CraftingWindow>().Build();
        entities = noUICraftQuery.ToEntityArray(Allocator.Temp);
        foreach (var e in entities)
        {
            var craftables = SystemAPI.GetBuffer<Craftables>(e);
            var crafter = new CraftingWindow
            {
                Queue = e,
                Craftables = craftables
            };
            anchor.Add(crafter);
            EntityManager.AddComponentData(e, crafter);
        }

        var itemLookup = new ItemAspect.Lookup(ref CheckedStateRef);
        foreach (var (inventory, ui) in SystemAPI.Query<DynamicBuffer<Inventory>, InventoryElement>())
        {
            ui.Update(inventory, itemLookup);   
        }

        foreach (var (_, ui) in SystemAPI.Query<DynamicBuffer<Craftables>, CraftingWindow>())
        {
            ui.UpdateRecipes(EntityManager, itemLookup);
            ui.UpdateQueue(EntityManager);
        }
    }
}