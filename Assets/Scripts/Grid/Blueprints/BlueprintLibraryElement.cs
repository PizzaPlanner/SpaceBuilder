using System.Collections.Generic;
using UnityEngine.UIElements;

public class BlueprintLibraryElement : VisualElement
{
    public BlueprintLibraryElement()
    {
        Add(new Label() { name = "Name" });
        Add(new VisualElement() { name = "SourceIcon" });
        RegisterCallback<MouseDownEvent>(OnClick);
    }

    SpawnablePrefab _prefab;
    public SpawnablePrefab Prefab
    {
        set
        {
            this.Q<Label>("Name").text = value.Name.Value;
            var icon = this.Q("SourceIcon");
            icon.ClearClassList();
            icon.AddToClassList(value.Type.ToString());
            _prefab = value;
        }
    }

    void OnClick(MouseDownEvent evt)
    {
        BlueprintGeneratorSystem.QueueBlueprintSpawnAtPlayer(_prefab.Prefab);
    }
    
    public new class UxmlFactory : UxmlFactory<BlueprintLibraryElement, UxmlTraits> { }
}

public class BlueprintLibraryWindow : VisualElement
{
    public BlueprintLibraryWindow()
    {
        Add(new VisualElement{ name = "Content" });
    }

    public IEnumerable<SpawnablePrefab> Prefabs
    {
        set
        {
            var contentContainer = this.Q("Content");
            contentContainer.Clear();
            foreach (var v in value)
                contentContainer.Add(new BlueprintLibraryElement { Prefab = v });
        }
    }
    
    public new class UxmlFactory : UxmlFactory<BlueprintLibraryWindow, UxmlTraits> { }
}