using System;
using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryElement : VisualElement
{
    public Entity Container;
    private int lastLength = 0;
    public Action<InventoryButton, InventoryButton> OnDragDrop;
    public int2 Subset;

    public void Update(BufferLookup<Inventory> inventories, ComponentLookup<Identifieable> ids, ComponentLookup<Name> names, ComponentLookup<Stackable> stacks)
    {
        if (!inventories.HasComponent(Container))
        {
            this.Clear();
            return;
        }


        var buffer = inventories[Container];
        if (buffer.Length != lastLength)
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

        // TODO: This is not triggered for new items after crafting?
        this.Query<InventoryButton>().ForEach(x =>
        {
            var entity = buffer[x.Slot].Item;
            var empty = entity == Entity.Null;
            x.IsEmpty = empty;
            if (empty)
                return;

            x.Name = names.HasComponent(entity) ? names[entity].Value.Value : "";
            x.Info = stacks.HasComponent(entity)
                ? $"x{stacks[entity].Size}"
                : "";

            x.Image = IdentifiableMappingSystem.IconById.ContainsKey(ids[entity].ItemId) ? IdentifiableMappingSystem.IconById[ids[entity].ItemId] : null;
        });
    }

    public new class UxmlFactory : UxmlFactory<InventoryElement, UxmlTraits> { }
}

public class InventoryButton : VisualElement
{
    public Entity Container;
    public int Slot;

    public bool StartDrag;
    public static InventoryButton DragTarget;
    public static InventoryButton Dragged;
    public bool Selected
    {
        set
        {
            if (value) AddToClassList("inventory-button__Active");
            else RemoveFromClassList("inventory-button__Active");
        }
        get => ClassListContains("inventory-button__Active");
    }

    public bool IsEmpty
    {
        set { foreach (var child in Children()) child.style.display = value ? DisplayStyle.None : DisplayStyle.Flex; }
    }

    public string Name
    {
        get => this.Q<Label>(null, "name").text;
        set { this.Q<Label>(null, "name").text = value; }
    }

    public string Info
    {
        get => this.Q<Label>(null, "stack").text;
        set { this.Q<Label>(null, "stack").text = value; }
    }

    public Texture2D Image
    {
        set => this.Q(null, "icon").style.backgroundImage = value ? value : new StyleBackground(StyleKeyword.Null);
    }

    public InventoryButton()
    {
        AddToClassList("inventory-button");
        var icon = new VisualElement();
        icon.AddToClassList("icon");
        var label = new Label("Test");
        label.AddToClassList("name");
        var stack = new Label("x10");
        stack.AddToClassList("stack");
        Add(icon);
        Add(label);
        Add(stack);
    }

    public new class UxmlFactory : UxmlFactory<InventoryButton, UxmlTraits> { }
}

public class CraftingInputButton : VisualElement
{
    public CraftingInput Input;

    public bool HasItems
    {
        set
        {
            if (value) RemoveFromClassList("crafting-input__missing");
            else AddToClassList("crafting-input__missing");
        }
        get => !ClassListContains("crafting-input__missing");
    }

    public string Name
    {
        get => this.Q<Label>(null, "name").text;
        set { this.Q<Label>(null, "name").text = value; }
    }

    public string Info
    {
        get => this.Q<Label>(null, "stack").text;
        set { this.Q<Label>(null, "stack").text = value; }
    }

    public CraftingInputButton()
    {
        AddToClassList("crafting-input");
        var icon = new VisualElement();
        icon.AddToClassList("icon");
        var label = new Label("Test");
        label.AddToClassList("name");
        var stack = new Label("x10");
        stack.AddToClassList("stack");
        Add(icon);
        Add(label);
        Add(stack);
    }

    public new class UxmlFactory : UxmlFactory<CraftingInputButton, UxmlTraits> { }
}

public class BuilderButton : VisualElement
{
    public ulong Id;

    public bool Selected
    {
        set
        {
            if (value) AddToClassList("builder-button__Active");
            else RemoveFromClassList("builder-button__Active");
        }
        get => ClassListContains("builder-button__Active");
    }

    public BuilderButton()
    {
        AddToClassList("builder-button");
        var selector = new VisualElement();
        selector.AddToClassList("selector");
        var icon = new VisualElement();
        icon.AddToClassList("icon");
        var label = new Label("Test");
        label.AddToClassList("name");
        Add(selector);
        Add(icon);
        Add(label);
    }

    public new class UxmlFactory : UxmlFactory<BuilderButton, UxmlTraits> { }
}
