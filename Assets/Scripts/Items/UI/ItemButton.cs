using UnityEngine;
using UnityEngine.UIElements;

public class ItemButton : VisualElement
{
    public string Name
    {
        get => this.Q<Label>(null, "name").text;
        protected set { this.Q<Label>(null, "name").text = value; }
    }

    public string Info
    {
        get => this.Q<Label>(null, "stack").text;
        protected set { this.Q<Label>(null, "stack").text = value; }
    }

    protected Texture2D Image
    {
        set => this.Q(null, "icon").style.backgroundImage = value ? value : new StyleBackground(StyleKeyword.Null);
    }

    public bool IsEmpty
    {
        set { foreach (var child in Children()) child.style.display = value ? DisplayStyle.None : DisplayStyle.Flex; }
    }
    
    public ItemButton()
    {
        AddToClassList("item-button");
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
}