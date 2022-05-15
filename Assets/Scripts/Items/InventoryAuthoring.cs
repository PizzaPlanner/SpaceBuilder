using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.tvOS;
using UnityEngine.UIElements;

public struct Inventory : IBufferElementData
{
    public Slot Slot;
}

public static class InventoryHelper
{
    public static bool Contains(this DynamicBuffer<Inventory> buffer, ComponentDataFromEntity<Identifieable> idBuffer, ComponentDataFromEntity<Stackable> stackBuffer, long itemId, ushort amount)
    {
        ushort has = 0;
        for(int i = 0; i < buffer.Length; i++)
            if (buffer[i].Slot.Item != Entity.Null && idBuffer[buffer[i].Slot.Item].ItemId == itemId)
            {
                var item = buffer[i].Slot.Item;
                has += stackBuffer.HasComponent(item) ? stackBuffer[item].Size : (ushort)1;
            }

        return has >= amount;
    }
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

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class InventoryUISystem : SystemBase
{
    public struct InventoryChanged : IComponentData { }

    private bool initialized;
    private bool inventoryOpen;
    private bool wantOpenInventory;
    static InventoryElement Inventory, Hotbar, RemoteInventory;
    static CraftingWindow CraftWindow;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnStartRunning()
    {
        if (initialized)
            return;

        var playerInventory = GetBuffer<Inventory>(GetSingletonEntity<LocalPlayer>());
        Hotbar = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<InventoryElement>("Hotbar");
        Hotbar.Container = GetSingletonEntity<LocalPlayer>();
        Hotbar.Subset = new int2(playerInventory.Length - 5, playerInventory.Length);
        Hotbar.OnDragDrop = OnDragDrop;
        Hotbar.style.display = DisplayStyle.Flex;

        Inventory = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("Inventory").Q<InventoryElement>("MyBag");
        Inventory.Container = GetSingletonEntity<LocalPlayer>();
        Inventory.Subset = new int2(0, playerInventory.Length - 5);
        Inventory.OnDragDrop = OnDragDrop;
        Inventory.style.display = DisplayStyle.None;

        RemoteInventory = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<InventoryElement>("RemoteBag");
        RemoteInventory.Container = Entity.Null;
        RemoteInventory.Subset = int2.zero;
        RemoteInventory.OnDragDrop = World.GetExistingSystem<InventoryUISystem>().OnDragDrop;
        RemoteInventory.style.display = DisplayStyle.None;

        CraftWindow = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<CraftingWindow>("Crafting");
        CraftWindow.style.display = DisplayStyle.None;

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
        var needFullUpdate = !GetEntityQuery(typeof(InventoryChanged)).IsEmpty;
        if (Input.GetKeyDown(KeyCode.C) || wantOpenInventory)
        {
            wantOpenInventory = false;
            inventoryOpen = !inventoryOpen;
            Inventory.style.display = inventoryOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!inventoryOpen)
            {
                RemoteInventory.style.display = DisplayStyle.None;
                CraftWindow.style.display = DisplayStyle.None;
            }
            else
            {
                needFullUpdate = true;
            }
            World.GetExistingSystem<InputSystem>().SetCursor(inventoryOpen ? InputSystem.CursorState.Default : InputSystem.CursorState.Disabled);
        }

        var player = GetSingletonEntity<LocalPlayer>();
        var elm = InventoryUISystem.RemoteInventory;
        Entities
            .ForEach((Entity e, in DynamicBuffer<Inventory> inv, in Interacted interact) =>
            {
                if (interact.Interactor != player)
                    return;

                elm.Container = e;
                elm.style.display = DisplayStyle.Flex;
                if(!inventoryOpen)
                    wantOpenInventory = true;
            }).WithoutBurst().Run();

        var crft = InventoryUISystem.CraftWindow;
        Entities
            .ForEach((in Crafter crafter, in Interacted interact) =>
            {
                if (interact.Interactor != player)
                    return;

                crft.SetCrafter(crafter.Queue, EntityManager);
                crft.style.display = DisplayStyle.Flex;
            }).WithoutBurst().Run();

        if (needFullUpdate)
        {
            var inventories = GetBufferFromEntity<Inventory>(true);
            var names = GetComponentDataFromEntity<Name>(true);
            var stacks = GetComponentDataFromEntity<Stackable>(true);
            GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Query<InventoryElement>()
                         .ForEach(c => c.Update(inventories, names, stacks));
            EntityManager.DestroyEntity(GetEntityQuery(typeof(InventoryChanged)));
            EntityManager.RemoveComponent<SelectedItem>(GetEntityQuery(typeof(SelectedItem)));
            CraftWindow.UpdateRecipes(EntityManager,
                GetComponentDataFromEntity<Identifieable>(true),
                GetComponentDataFromEntity<Stackable>(true));
        }

        if (CraftWindow.style.display == DisplayStyle.Flex)
        {
            CraftWindow.UpdateQueue(EntityManager);
        }

        if(GetSingleton<InputSystem.InputData>().ScrollMode == InputSystem.ScrollMode.Inventory)
            SelectHotbarItem(GetSingleton<InputSystem.InputData>().Scroll);
    }

    void SelectHotbarItem(float scroll)
    {
        if (scroll == 0)
            return;

        EntityManager.RemoveComponent<SelectedItem>(GetEntityQuery(typeof(SelectedItem)));
        var buttons = Hotbar.Query<InventoryButton>().ToList();
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
