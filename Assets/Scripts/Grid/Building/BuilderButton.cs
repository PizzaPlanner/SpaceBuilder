using UnityEngine.UIElements;

public class BuilderButton : VisualElement
{
    Identifieable _id;
    public Identifieable Id
    {
        set
        {
            _id = value;
            this.Q<Label>().text = value.DefaultName.Value;
            this.Q(className:"icon").style.backgroundImage = _id.Icon;
        }
        get => _id;
    }

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