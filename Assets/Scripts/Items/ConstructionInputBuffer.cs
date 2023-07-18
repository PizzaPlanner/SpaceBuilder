using Parts;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public struct ConstructionInputBuffer : IBufferElementData
{
    public ulong ItemId;
    public ushort Amount;
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
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<BuilderMode_Build>();
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
            DelayPerBuilding = 0.4f,
            InitialDelayPerItem = 0.15f,
            MinDelayPerItem = 0.01f
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
        if (!target.CanConstruct || target.Value != target.PreviousValue)
        {
            var constructor = GetComponent<Constructor>(GetSingletonEntity<LocalPlayer>());
            constructor.LastTick = double.MaxValue;
            SetComponent(GetSingletonEntity<LocalPlayer>(), constructor);
            uiRoot.style.display = DisplayStyle.None;
            return;
        }
        uiRoot.style.display = DisplayStyle.Flex;

        if (Input.GetKeyDown(KeyCode.F))
        {
            var constructor = GetComponent<Constructor>(GetSingletonEntity<LocalPlayer>());
            constructor.LastTick = World.Time.ElapsedTime + constructor.DelayPerBuilding;
            constructor.TimeSpentOnItem = 0;
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
        var maxProgress = 0u;
        var partScale = 1u;
        if (HasComponent<GridItem>(target.Value) && GetComponent<GridItem>(target.Value).Scalable)
        {
            var size = GetComponent<GridItem>(target.Value).Area.Size;
            partScale = (uint)(size.x * size.y * size.z);
        }

        var playerInventory = GetBuffer<Inventory>(GetSingletonEntity<LocalPlayer>());
        var parts = GetBuffer<ConstructionInputBuffer>(target.Value);
        var itemToConsume = Entity.Null;
        for (int i = 0; i < parts.Length; i++)
        {
            var partAmount = parts[i].Amount * partScale; 
            maxProgress += partAmount;
            if (progress == uint.MaxValue)
            {
                var label = new Label($"0/{partAmount}\t\t{GetComponent<Name>(IdentifiableMappingSystem.EntitiesById[parts[i].ItemId]).Value.Value}");
                itemList.Add(label);
            }
            else if (progress >= partAmount)
            {
                progress -= partAmount;
                var label = new Label($"{partAmount}/{partAmount}\t\t{GetComponent<Name>(IdentifiableMappingSystem.EntitiesById[parts[i].ItemId]).Value.Value}");
                label.style.color = Color.grey;
                itemList.Add(label);
            }
            else
            {
                for(int j = 0; j < playerInventory.Length; j++)
                    if (playerInventory[j].Item != Entity.Null && GetComponent<Identifieable>(playerInventory[j].Item).ItemId == parts[i].ItemId)
                    {
                        itemToConsume = playerInventory[j].Item;
                        break;
                    }

                var label = new Label($"{progress}/{partAmount}\t\t{GetComponent<Name>(IdentifiableMappingSystem.EntitiesById[parts[i].ItemId]).Value.Value}");
                label.style.color = itemToConsume == Entity.Null ? Color.red : Color.green;
                itemList.Add(label);
                progress = uint.MaxValue;
            }
        }

        progressBar.style.width =
            new StyleLength(new Length(current.Progress * 100f / maxProgress, LengthUnit.Percent));

    }
}
