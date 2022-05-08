using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
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

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class InventoryUISystem : SystemBase
{
    public struct InventoryChanged : IComponentData { }

    private VisualElement hotbar;
    private VisualElement inventory;

    private bool inventoryOpen;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        // RequireForUpdate(GetEntityQuery(typeof(InventoryChanged)));
    }

    protected override void OnStartRunning()
    {
        hotbar ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("Hotbar");
        hotbar.style.display = DisplayStyle.Flex;
        hotbar.Clear();

        inventory ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("Inventory");
        inventory.style.display = DisplayStyle.None;
        inventory.Q("Bag").Clear();

        var inv = GetBuffer<Inventory>(GetSingletonEntity<LocalPlayer>());
        for(int i = 0; i < 5; i++)
        {
            hotbar.Add(new InventoryButton{ Container = GetSingletonEntity<LocalPlayer>(), Slot = i});
        }

        for(int i = 0; i < inv.Length; i++)
        {
            var button = new InventoryButton {Container = GetSingletonEntity<LocalPlayer>(), Slot = i};
            button.RegisterCallback<MouseEnterEvent>(_ => button.Selected = true);
            button.RegisterCallback<MouseLeaveEvent>(_ => button.Selected = false);
            inventory.Q("Bag").Add(button);
        }

    }

    protected override void OnStopRunning()
    {
        hotbar.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        // EntityManager.DestroyEntity(GetEntityQuery(typeof(InventoryChanged)));
        var inventories = GetBufferFromEntity<Inventory>(true);
        hotbar.Query<InventoryButton>().ForEach(x =>
        {
            if (x.Container == Entity.Null ||
                !inventories.HasComponent(x.Container) ||
                inventories[x.Container].Length <= x.Slot ||
                inventories[x.Container][x.Slot].Slot.Item == Entity.Null)
            {
                x.IsEmpty = true;
                return;
            }

            x.IsEmpty = false;
            var item = inventories[x.Container][x.Slot].Slot.Item;
            x.Name = GetComponent<Name>(item).Value.Value;
            if (HasComponent<Stackable>(item))
                x.Info = $"x{GetComponent<Stackable>(item).Size}";
            else
                x.Info = "";
        });

        if(GetSingleton<InputSystem.InputData>().ScrollMode == InputSystem.ScrollMode.Inventory)
            SelectHotbarItem(GetSingleton<InputSystem.InputData>().Scroll);

        if (Input.GetKeyDown(KeyCode.C))
            inventoryOpen = !inventoryOpen;
        inventory.style.display = inventoryOpen ? DisplayStyle.Flex : DisplayStyle.None;

        if (inventory.style.display == DisplayStyle.None)
            return;

        inventory.Query<InventoryButton>().ForEach(x =>
        {
            if (x.Container == Entity.Null ||
                !inventories.HasComponent(x.Container) ||
                inventories[x.Container].Length <= x.Slot ||
                inventories[x.Container][x.Slot].Slot.Item == Entity.Null)
            {
                x.IsEmpty = true;
                return;
            }

            x.IsEmpty = false;
            var item = inventories[x.Container][x.Slot].Slot.Item;
            x.Name = GetComponent<Name>(item).Value.Value;
            if (HasComponent<Stackable>(item))
                x.Info = $"x{GetComponent<Stackable>(item).Size}";
            else
                x.Info = "";
            x.style.display = DisplayStyle.Flex;
        });
    }

    void SelectHotbarItem(float scroll)
    {
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
