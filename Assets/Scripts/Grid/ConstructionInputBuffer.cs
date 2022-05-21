using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public struct ConstructionInputBuffer : IBufferElementData
{
    public ulong ItemId;
    public ushort Amount;
}

public struct UnderConstruction : IComponentData
{
    public ushort Progress;
}

public struct FinishConstruction : IComponentData { }

[UpdateBefore(typeof(InventoryManagementSystem))]
public partial class ConstructionSystem : SystemBase
{
    private VisualElement uiRoot;
    private VisualElement progressBar, itemList;
    private VisualElement constructionBorder;

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

        EntityManager.AddComponentData(GetSingletonEntity<LocalPlayer>(), new Constructor
        {
            LastTick = double.MaxValue,
            DelayPerBuilding = 0.5f,
            DelayPerItem = 0.1f
        });
    }

    protected override void OnStopRunning()
    {
        base.OnStopRunning();
        uiRoot.style.display = DisplayStyle.None;
        constructionBorder.style.display = DisplayStyle.None;

        EntityManager.RemoveComponent<Constructor>(GetSingletonEntity<LocalPlayer>());
    }

    protected override void OnUpdate()
    {
        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
        if (!target.CanConstruct)
        {
            uiRoot.style.display = DisplayStyle.None;
            return;
        }
        uiRoot.style.display = DisplayStyle.Flex;

        if (Input.GetKeyDown(KeyCode.F))
        {
            var constructor = GetComponent<Constructor>(GetSingletonEntity<LocalPlayer>());
            constructor.LastTick = Time.ElapsedTime + constructor.DelayPerBuilding;
            SetComponent(GetSingletonEntity<LocalPlayer>(), constructor);
        } else if (Input.GetKeyUp(KeyCode.F))
        {
            var constructor = GetComponent<Constructor>(GetSingletonEntity<LocalPlayer>());
            constructor.LastTick = double.MaxValue;
            SetComponent(GetSingletonEntity<LocalPlayer>(), constructor);
        }

        itemList.Clear();

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
                var label = new Label($"0/{parts[i].Amount}\t\t{GetComponent<Name>(IdentifiableMappingSystem.EntitiesById[parts[i].ItemId]).Value.Value}");
                itemList.Add(label);
            }
            else if (progress >= parts[i].Amount)
            {
                progress -= parts[i].Amount;
                var label = new Label($"{parts[i].Amount}/{parts[i].Amount}\t\t{GetComponent<Name>(IdentifiableMappingSystem.EntitiesById[parts[i].ItemId]).Value.Value}");
                label.style.color = Color.grey;
                itemList.Add(label);
            }
            else
            {
                for(int j = 0; j < playerInventory.Length; j++)
                    if (playerInventory[j].Slot.Item != Entity.Null && GetComponent<Identifieable>(playerInventory[j].Slot.Item).ItemId == parts[i].ItemId)
                    {
                        itemToConsume = playerInventory[j].Slot.Item;
                        break;
                    }

                var label = new Label($"{progress}/{parts[i].Amount}\t\t{GetComponent<Name>(IdentifiableMappingSystem.EntitiesById[parts[i].ItemId]).Value.Value}");
                label.style.color = itemToConsume == Entity.Null ? Color.red : Color.green;
                itemList.Add(label);
                progress = ushort.MaxValue;
            }
        }

        progressBar.style.width =
            new StyleLength(new Length(current.Progress * 100f / maxProgress, LengthUnit.Percent));

    }
}
