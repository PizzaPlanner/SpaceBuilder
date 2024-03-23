using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

[UpdateInGroup(typeof(PresentationSystemGroup))]
partial class BuildingUIFrameSystem : SystemBase
{
    VisualElement constructionBorder, destructionBorder;
    protected override void OnCreate()
    {
        RequireForUpdate<ControlScheme.Building>();
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnStartRunning()
    {
        constructionBorder ??= GameObject.FindAnyObjectByType<UIDocument>().rootVisualElement.Q("BlueprintBorder");
        destructionBorder ??= GameObject.FindAnyObjectByType<UIDocument>().rootVisualElement.Q("DestructionBorder");
    }

    protected override void OnStopRunning()
    {
        constructionBorder.style.display = DisplayStyle.None;
        destructionBorder.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        if (SystemAPI.HasSingleton<ControlScheme.Building.Destroy>())
        {
            destructionBorder.style.display = DisplayStyle.Flex;
            constructionBorder.style.display = DisplayStyle.None;
            return;
        }
        
        destructionBorder.style.display = DisplayStyle.None;
        constructionBorder.style.display = DisplayStyle.Flex;
        if (SystemAPI.TryGetSingletonRW<GhostPlacementBuilder>(out var builder) 
            && builder.ValueRW.Target.Grid != Entity.Null)
        {
            if (builder.ValueRW.PrefabIdToBuild == default)
                constructionBorder.Q<Label>().text = "<Press T to select a Part>";
            else
            {
                var partName = builder.ValueRW.PrefabIdToBuild.DefaultName;
                var builderArea = (float3)builder.ValueRW.ToPartInfo.ToArea.Size * GridStructure.CELL_SIZE;
                constructionBorder.Q<Label>().text = $"{partName}\n{builderArea.x} x {builderArea.y} x {builderArea.z}";
            }
        }
        else
            constructionBorder.Q<Label>().text = "<No Target>";
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class BuildingUISystem : SystemBase
{
    private VisualElement buildMenu;
    protected override void OnCreate()
    {
        RequireForUpdate<ControlScheme.UI.BuildingParts>();
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnUpdate() { }

    protected override void OnStartRunning()
    {
        var root = GameObject.FindAnyObjectByType<UIDocument>().rootVisualElement;
        buildMenu ??= root.Q("BuildMenu");
        buildMenu.style.display = DisplayStyle.Flex;
        buildMenu.Clear();

        var query = GetEntityQuery(typeof(Prefab), typeof(GridItem), typeof(Identifieable));
        var ids = query.ToComponentDataArray<Identifieable>(Allocator.Temp);
        for (int i = 0; i < ids.Length; i++)
        {
            var button = new BuilderButton { Id = ids[i] };
            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                SetSelectedPrefab(button.Id);
            });
            buildMenu.Add(button);
        }
    }

    protected override void OnStopRunning()
    {
        buildMenu.style.display = DisplayStyle.None;
    }

    void SetSelectedPrefab(Identifieable prefabId)
    {
        foreach(var button in buildMenu.Query<BuilderButton>().Build())
            button.Selected = button.Id == prefabId;
        var builder = SystemAPI.GetSingletonRW<GhostPlacementBuilder>();
        builder.ValueRW.SetPrefab(prefabId);
    }
}