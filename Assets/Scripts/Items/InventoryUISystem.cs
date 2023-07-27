using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

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
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<ControlScheme.UI.Inventory>();
    }

    protected override void OnStartRunning()
    {
        EntityManager.CreateEntity(typeof(InventoryChanged));
        if (!initialized)
        {
            var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
            var playerInventory = SystemAPI.GetBuffer<Inventory>(localPlayer);
            Hotbar = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<InventoryElement>("Hotbar");
            Hotbar.Container = localPlayer;
            Hotbar.Subset = new int2(playerInventory.Length - 5, playerInventory.Length);
            Hotbar.OnDragDrop = OnDragDrop;
            Hotbar.style.display = DisplayStyle.Flex;

            Inventory = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("Inventory")
                                  .Q<InventoryElement>("MyBag");
            Inventory.Container = localPlayer;
            Inventory.Subset = new int2(0, playerInventory.Length - 5);
            Inventory.OnDragDrop = OnDragDrop;
            Inventory.style.display = DisplayStyle.None;

            RemoteInventory = GameObject.FindObjectOfType<UIDocument>().rootVisualElement
                                        .Q<InventoryElement>("RemoteBag");
            RemoteInventory.Container = Entity.Null;
            RemoteInventory.Subset = int2.zero;
            RemoteInventory.OnDragDrop = World.GetExistingSystemManaged<InventoryUISystem>().OnDragDrop;
            RemoteInventory.style.display = DisplayStyle.None;

            CraftWindow = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<CraftingWindow>("Crafting");
            CraftWindow.style.display = DisplayStyle.None;

            initialized = true;
        }
        Inventory.style.display = DisplayStyle.Flex;
        Hotbar.style.display = DisplayStyle.Flex;
    }

    public void OnDragDrop(InventoryButton dragged, InventoryButton button)
    {
        var i1 = SystemAPI.GetBuffer<Inventory>(dragged.Container);
        var i2 = SystemAPI.GetBuffer<Inventory>(button.Container);

        var from = i1[dragged.Slot];
        var to = i2[button.Slot];

        i1[dragged.Slot] = to;
        i2[button.Slot] = from;

        EntityManager.CreateEntity(typeof(InventoryChanged));
    }

    protected override void OnStopRunning()
    {
        Inventory.style.display = DisplayStyle.None;
        Hotbar.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        var needFullUpdate = !GetEntityQuery(typeof(InventoryChanged)).IsEmpty;

        var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
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
            var inventories = GetBufferLookup<Inventory>(true);
            var names = GetComponentLookup<Name>(true);
            var stacks = GetComponentLookup<Stackable>(true);
            var ids = GetComponentLookup<Identifieable>(true);
            GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Query<InventoryElement>()
                      .ForEach(c => c.Update(inventories, ids, names, stacks));
            EntityManager.DestroyEntity(GetEntityQuery(typeof(InventoryChanged)));
            EntityManager.RemoveComponent<Selected>(GetEntityQuery(typeof(Selected)));
            CraftWindow.UpdateRecipes(EntityManager,
                GetComponentLookup<Identifieable>(true),
                GetComponentLookup<Stackable>(true));
        }

        if (CraftWindow.style.display == DisplayStyle.Flex)
        {
            CraftWindow.UpdateQueue(EntityManager);
        }

        var input = SystemAPI.GetSingleton<InputData>();
        if(input.ScrollMode == ScrollMode.Inventory)
            SelectHotbarItem(input.Scroll);
    }

    void SelectHotbarItem(float scroll)
    {
        if (scroll == 0)
            return;

        EntityManager.RemoveComponent<Selected>(GetEntityQuery(typeof(Selected)));
        var buttons = Hotbar.Query<InventoryButton>().ToList();
        if (buttons.Count == 0)
            return;

        var nextButton = 0;
        for(int i = 0; i < buttons.Count; i++)
            if (buttons[i].Selected)
            {
                nextButton = (i + (int)math.sign(scroll) + buttons.Count) % buttons.Count;
                buttons[i].Selected = false;
                break;
            }

        buttons[nextButton].Selected = true;

        var item = SystemAPI.GetBuffer<Inventory>(SystemAPI.GetSingletonEntity<LocalPlayer>())[buttons[nextButton].Slot].Item;
        if(item != Entity.Null)
            EntityManager.AddComponent<Selected>(item);
    }
}