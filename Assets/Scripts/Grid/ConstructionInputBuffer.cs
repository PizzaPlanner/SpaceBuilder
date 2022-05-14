using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public struct ConstructionInputBuffer : IBufferElementData
{
    public long ItemId;
    public ushort Amount;
}

public struct UnderConstruction : IComponentData
{
    public ushort Progress;
}

[UpdateBefore(typeof(InventoryManagementSystem))]
public partial class ConstructionSystem : SystemBase
{
    private VisualElement uiRoot;
    private VisualElement progressBar, itemList;
    private VisualElement constructionBorder;
    private bool needItemListUpdate;
    private double lastInputGiven;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<BuilderMode_Build>();
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        uiRoot ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("ContextMenu").Q("Construction");
        uiRoot.style.display = DisplayStyle.Flex;
        progressBar ??= uiRoot.Q("progressbar");
        progressBar.style.width = 0;
        constructionBorder ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("ConstructionBorder");
        constructionBorder.style.display = DisplayStyle.Flex;
        itemList ??= uiRoot.Q("ItemList");
        needItemListUpdate = true;
    }

    protected override void OnStopRunning()
    {
        base.OnStopRunning();
        uiRoot.style.display = DisplayStyle.None;
        constructionBorder.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        Entities
            .WithNone<ConstructionInputBuffer>()
            .WithAll<UnderConstruction>()
            .ForEach((Entity e) =>
            {
                EntityManager.AddBuffer<ConstructionInputBuffer>(e);
            }).WithStructuralChanges().WithoutBurst().Run();



        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
        needItemListUpdate |= target.Value != target.PreviousValue;
        if (needItemListUpdate)
            itemList.Clear();

        if (!target.CanConstruct ||
            target.Value == Entity.Null ||
            !HasComponent<UnderConstruction>(target.Value) ||
            !EntityManager.HasComponent<ConstructionInputBuffer>(target.Value))
            return;


        var current = GetComponent<UnderConstruction>(target.Value);
        var progress = current.Progress;
        var maxProgress = 0;

        var playerInventory = GetBuffer<Inventory>(GetSingletonEntity<LocalPlayer>());
        var parts = GetBuffer<ConstructionInputBuffer>(target.Value);
        var itemToConsume = Entity.Null;
        for (int i = 0; i < parts.Length; i++)
        {
            maxProgress += parts[i].Amount;
            if (progress == ushort.MaxValue)
            {
                if (needItemListUpdate)
                {

                    var label = new Label($"0/{parts[i].Amount}\t\t{GetComponent<Name>(IdentifiableMappingSystem.EntitiesById[parts[i].ItemId]).Value.Value}");
                    itemList.Add(label);
                }
            }
            else if (progress >= parts[i].Amount)
            {
                progress -= parts[i].Amount;
                if (needItemListUpdate)
                {
                    var label = new Label($"{parts[i].Amount}/{parts[i].Amount}\t\t{GetComponent<Name>(IdentifiableMappingSystem.EntitiesById[parts[i].ItemId]).Value.Value}");
                    label.style.color = Color.grey;
                    itemList.Add(label);
                }
            }
            else
            {
                for(int j = 0; j < playerInventory.Length; j++)
                    if (playerInventory[j].Slot.Item != Entity.Null && GetComponent<Identifieable>(playerInventory[j].Slot.Item).ItemId == parts[i].ItemId)
                    {
                        itemToConsume = playerInventory[j].Slot.Item;
                        break;
                    }

                if (needItemListUpdate)
                {
                    var label = new Label($"{progress}/{parts[i].Amount}\t\t{GetComponent<Name>(IdentifiableMappingSystem.EntitiesById[parts[i].ItemId]).Value.Value}");
                    label.style.color = itemToConsume == Entity.Null ? Color.red : Color.green;
                    itemList.Add(label);
                }
                progress = ushort.MaxValue;
            }
        }

        if (Input.GetKey(KeyCode.F) && Time.ElapsedTime - lastInputGiven > 0.1f && itemToConsume != Entity.Null)
        {
            EntityManager.AddComponentData(itemToConsume, new RemoveItem {Quantity = 1});
            current.Progress++;
            if (current.Progress >= maxProgress)
            {
                ConstructBlueprint(target.Value);
            }
            else
                SetComponent(target.Value, current);
            lastInputGiven = Time.ElapsedTime;
            needItemListUpdate = true;
        }
        else needItemListUpdate = false;


        progressBar.style.width =
            new StyleLength(new Length(current.Progress * 100f / maxProgress, LengthUnit.Percent));

    }

    private void ConstructBlueprint(Entity bp)
    {
        var pp = EntityManager.CreateEntity(typeof(PendingPart), typeof(UnderConstruction));
        var position = GetComponent<GridItem>(bp).Area.Origin;
        var rotation = GetComponent<Rotation>(bp).Value;
        SetComponent(pp, new PendingPart
        {
            Data = new BuildingPartInfo
            {
                Id = GetComponent<Identifieable>(bp).ItemId,
                Position = position,
                Orientation = rotation,
                BuildStatus = ushort.MaxValue
            },
            Grid = GetComponent<GridItem>(bp).Grid
        });
        var ppquery = GetEntityQuery(typeof(UnderConstruction), typeof(PendingPart));
        new BuildingSystem.BuildJob
        {
            buffer = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer(),
            grids = GetComponentDataFromEntity<Grid>(true),
            gridItems = GetComponentDataFromEntity<GridItem>(true),
            occupations = GetBufferFromEntity<OccupationBuffer>(true),
            prefabMap = IdentifiableMappingSystem.EntitiesById,
            IsFinishConstructionJob = true
        }.Run(ppquery);
        EntityManager.DestroyEntity(pp);
        EntityManager.DestroyEntity(bp);
    }
}
