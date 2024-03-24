using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
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
        Add(new Label("Blueprints"));
        Add(new ScrollView{ name = "Content" });
    }

    public NativeParallelHashMap<FixedString64Bytes, SpawnablePrefab> Prefabs
    {
        set
        {
            foreach (var key in value.GetKeyArray(Allocator.Temp))
            {
                var elemParent = this.Q<ScrollView>("Content"); 
                var current = (VisualElement)elemParent;
                var path = key.Value.Split("/");
                for (int i = 0; i < path.Length - 1; i++)
                {
                    var next = current.Children().OfType<Foldout>().FirstOrDefault(x => x.name == path[i]);
                    if (next == default)
                    {
                        next = new Foldout{ value = i > 0, name = path[i], text = path[i]};
                        current.Add(next);
                    }
                    current = next;
                }
                current.Add(new BlueprintLibraryElement { Prefab = value[key] });
            }
        }
    }
    
    public new class UxmlFactory : UxmlFactory<BlueprintLibraryWindow, UxmlTraits> { }
}