using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;

public class InventoryElement : VisualElement, IComponentData
{
    public Entity Container;
    private int lastLength = 0;
    public Action<InventoryButton, InventoryButton> OnDragDrop;
    public int2 Subset;

    void Init(DynamicBuffer<Inventory> buffer, ItemAspect.Lookup itemLookup)
    {
        this.Clear();
        for (int i = math.max(0, Subset.x); i < math.min(buffer.Length, Subset.y == 0 ? buffer.Length : Subset.y); i++)
        {
            var button = new InventoryButton {Container = Container, Slot = i};
            if (OnDragDrop != null)
            {
                button.RegisterCallback<MouseDownEvent>(_ =>
                {
                    InventoryButton.Dragged = button;
                    InventoryButton.DragTarget = null;
                });
                button.RegisterCallback<MouseEnterEvent>(_ => InventoryButton.DragTarget = button);
                button.RegisterCallback<MouseLeaveEvent>(_ => InventoryButton.DragTarget = null);
                button.RegisterCallback<MouseUpEvent>(_ =>
                {
                    if (InventoryButton.Dragged != null && InventoryButton.DragTarget != null && InventoryButton.Dragged != InventoryButton.DragTarget)
                    {
                        OnDragDrop(InventoryButton.Dragged, button);
                    }
                    InventoryButton.Dragged = null;
                    InventoryButton.DragTarget = null;
                });
            }
            Add(button);
        }
        lastLength = buffer.Length;
    }
    
    public void Update(DynamicBuffer<Inventory> inventory, ItemAspect.Lookup itemLookup)
    {
        if (inventory.Length != lastLength)
        {
            Init(inventory, itemLookup);
        }

        // TODO: This is not triggered for new items after crafting?
        foreach(var button in this.Query<InventoryButton>().Build())
        {
            var entity = inventory[button.Slot].Item;
            button.Item = entity == Entity.Null ? null : itemLookup[entity];
        };
    }

    public new class UxmlFactory : UxmlFactory<InventoryElement, UxmlTraits> { }
}