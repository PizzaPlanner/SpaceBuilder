using Unity.Entities;
using UnityEngine.UIElements;

public class InventoryButton : VisualElement
{
    public Entity Container;
    public int Slot;

    public bool Selected
    {
        set
        {
            if (value) AddToClassList("inventory-button__Active");
            else RemoveFromClassList("inventory-button__Active");
        }
        get => ClassListContains("inventory-button__Active");
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

public class BuilderButton : VisualElement
{
    public long Id;

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
