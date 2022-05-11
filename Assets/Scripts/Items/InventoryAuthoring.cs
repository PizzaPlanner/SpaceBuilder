using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UIElements;

public struct Inventory : IBufferElementData
{
    public Slot Slot;
}

public struct Slot
{
    public Entity Item;
    public SlotType Type;
}

public enum SlotType
{
    All,
    Tool,
}

public class InventoryAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public int Size;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var buffer = dstManager.AddBuffer<Inventory>(entity);
        for (int i = 0; i < Size; i++)
        {
            buffer.Add(new Inventory{ Slot = new Slot {Type = SlotType.All}});
        }
    }
}

public class InventoryUI : IComponentData
{
    public InventoryElement Container;
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class InventoryUISystem : SystemBase
{
    public struct InventoryChanged : IComponentData { }

    private bool initialized;
    private bool inventoryOpen;
    private InventoryElement inventory, hotbar;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnStartRunning()
    {
        if (initialized)
            return;

        var playerInventory = GetBuffer<Inventory>(GetSingletonEntity<LocalPlayer>());
        hotbar = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<InventoryElement>("Hotbar");
        hotbar.Container = GetSingletonEntity<LocalPlayer>();
        hotbar.Subset = new int2(playerInventory.Length - 5, playerInventory.Length);
        hotbar.OnDragDrop = OnDragDrop;
        hotbar.style.display = DisplayStyle.Flex;

        inventory = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("Inventory").Q<InventoryElement>("MyBag");
        inventory.Container = GetSingletonEntity<LocalPlayer>();
        inventory.Subset = new int2(0, playerInventory.Length - 5);
        inventory.OnDragDrop = OnDragDrop;
        inventory.style.display = DisplayStyle.None;

        var he = EntityManager.CreateEntity(typeof(InventoryUI));
        EntityManager.SetComponentData(he, new InventoryUI{Container = hotbar});

        var ie = EntityManager.CreateEntity(typeof(InventoryUI));
        EntityManager.SetComponentData(ie, new InventoryUI{Container = inventory});

        initialized = true;
        EntityManager.CreateEntity(typeof(InventoryChanged));
    }

    public void OnDragDrop(InventoryButton dragged, InventoryButton button)
    {
        var i1 = GetBuffer<Inventory>(dragged.Container);
        var i2 = GetBuffer<Inventory>(button.Container);

        var from = i1[dragged.Slot];
        var to = i2[button.Slot];

        i1[dragged.Slot] = to;
        i2[button.Slot] = from;

        EntityManager.CreateEntity(typeof(InventoryChanged));
    }

    protected override void OnStopRunning() { }

    protected override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            inventoryOpen = !inventoryOpen;
            inventory.style.display = inventoryOpen ? DisplayStyle.Flex : DisplayStyle.None;
            World.GetExistingSystem<InputSystem>().SetCursor(inventoryOpen ? InputSystem.CursorState.Default : InputSystem.CursorState.Disabled);
        }

        if (!GetEntityQuery(typeof(InventoryChanged)).IsEmpty)
        {
            var inventories = GetBufferFromEntity<Inventory>(true);
            var names = GetComponentDataFromEntity<Name>(true);
            var stacks = GetComponentDataFromEntity<Stackable>(true);
            var uiPanels = GetEntityQuery(typeof(InventoryUI)).ToComponentDataArray<InventoryUI>();
            foreach (var ui in uiPanels)
                ui.Container.Update(inventories, names, stacks);
            EntityManager.DestroyEntity(GetEntityQuery(typeof(InventoryChanged)));
            EntityManager.RemoveComponent<SelectedItem>(GetEntityQuery(typeof(SelectedItem)));
        }

        if(GetSingleton<InputSystem.InputData>().ScrollMode == InputSystem.ScrollMode.Inventory)
            SelectHotbarItem(GetSingleton<InputSystem.InputData>().Scroll);
    }

    void SelectHotbarItem(float scroll)
    {
        if (scroll == 0)
            return;

        EntityManager.RemoveComponent<SelectedItem>(GetEntityQuery(typeof(SelectedItem)));
        var buttons = hotbar.Query<InventoryButton>().ToList();
        if (buttons.Count == 0)
            return;

        var nextButton = 0;
        for(int i = 0; i < buttons.Count; i++)
            if (buttons[i].Selected)
            {
                nextButton = (i + (int)math.sign(scroll)).mod(buttons.Count);
                buttons[i].Selected = false;
                break;
            }

        buttons[nextButton].Selected = true;

        var item = GetBuffer<Inventory>(GetSingletonEntity<LocalPlayer>())[buttons[nextButton].Slot].Slot.Item;
        if(item != Entity.Null)
            EntityManager.AddComponent<SelectedItem>(item);
    }
}
