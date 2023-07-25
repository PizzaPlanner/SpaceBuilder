using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class BuildingUISystem : SystemBase
{
    private VisualElement buildMenu, constructionBorder;
    private GhostPlacementSystem placementSystem;
    protected override void OnCreate()
    {
        RequireForUpdate<BuilderMode_Blueprint>();
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
        RequireForUpdate<GhostPlacementBuilder>();
    }

    protected override void OnUpdate()
    {
        var builder = SystemAPI.GetSingletonRW<GhostPlacementBuilder>();
        if (builder.ValueRW.PrefabIdToBuild == 0)
            constructionBorder.Q<Label>().text = "<Press T to select a Part>";
        else if (builder.ValueRW.Target.Grid != Entity.Null)
            constructionBorder.Q<Label>().text =
                SystemAPI.GetComponent<Name>(IdentifiableMappingSystem.EntitiesById[builder.ValueRW.PrefabIdToBuild]).Value.Value;
        else
            constructionBorder.Q<Label>().text = "<No Target>";

        if (Input.GetKeyDown(KeyCode.T))
        {
            buildMenu.style.display = DisplayStyle.Flex;
            World.GetExistingSystemManaged<InputSystem>().SetCursor(InputSystem.CursorState.HotMenu);
        }
        else if (Input.GetKeyUp(KeyCode.T))
        {
            buildMenu.style.display = DisplayStyle.None;
            World.GetExistingSystemManaged<InputSystem>().SetCursor(InputSystem.CursorState.Disabled);
        }
    }

    protected override void OnStartRunning()
    {
        var root = GameObject.FindObjectOfType<UIDocument>().rootVisualElement;
        buildMenu ??= root.Q("BuildMenu");
        buildMenu.style.display = DisplayStyle.None;
        constructionBorder ??= root.Q("BlueprintBorder");
        constructionBorder.style.display = DisplayStyle.Flex;
        buildMenu.Clear();

        var query = GetEntityQuery(typeof(Prefab), typeof(GridItem), typeof(Identifieable), typeof(Name));
        var names = query.ToComponentDataArray<Name>(Allocator.Temp);
        var ids = query.ToComponentDataArray<Identifieable>(Allocator.Temp);
        for (int i = 0; i < ids.Length; i++)
        {
            var button = new BuilderButton {Id = ids[i].ItemId};
            button.Q<Label>().text = names[i].Value.Value;
            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                SetSelectedPrefab(button.Id);
            });
            buildMenu.Add(button);
        }
    }

    protected override void OnStopRunning()
    {
        constructionBorder.style.display = DisplayStyle.None;
        buildMenu.style.display = DisplayStyle.None;
        if(SystemAPI.HasSingleton<InputSystem.ControlHotMenu>())
            World.GetExistingSystemManaged<InputSystem>().SetCursor(InputSystem.CursorState.Disabled);
    }

    void SetSelectedPrefab(ulong prefabId)
    {
        foreach(var button in buildMenu.Query<BuilderButton>().Build())
            button.Selected = button.Id == prefabId;
        var builder = SystemAPI.GetSingletonRW<GhostPlacementBuilder>();
        builder.ValueRW.SetPrefab(prefabId);
    }
}