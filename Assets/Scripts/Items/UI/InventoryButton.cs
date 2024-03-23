using Unity.Entities;
using UnityEngine.UIElements;

public class InventoryButton : ItemButton
{
    public Entity Container;
    public int Slot;
    
    public static InventoryButton DragTarget;
    public static InventoryButton Dragged;

    public ItemAspect? Item
    {
        set
        {
            IsEmpty = !value.HasValue;
            if(!value.HasValue)
                return;
            
            Name = value.Value.Name.Value;
            Info = value.Value.IsStackable(out var stack) ? $"x{stack.Size}" : "";
            Image = value.Value.Image;
        }
    }
    
    public new class UxmlFactory : UxmlFactory<InventoryButton, UxmlTraits> { }
}