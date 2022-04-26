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
    private VisualElement uiRoot;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnStartRunning()
    {
        if (uiRoot == null)
            uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("Inventory");
        uiRoot.style.display = DisplayStyle.Flex;
        uiRoot.Clear();
        var inv = GetBuffer<Inventory>(GetSingletonEntity<LocalPlayer>());
        for(int i = 0; i < inv.Length; i++)
        {
            uiRoot.Add(new InventoryButton{ Container = GetSingletonEntity<LocalPlayer>(), Slot = i});
        }

    }

    protected override void OnStopRunning()
    {
        uiRoot.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        var inventories = GetBufferFromEntity<Inventory>(true);
        uiRoot.Query<InventoryButton>().ForEach(x =>
        {

            if (x.Container == Entity.Null ||
                !inventories.HasComponent(x.Container) ||
                inventories[x.Container].Length <= x.Slot ||
                inventories[x.Container][x.Slot].Slot.Item == Entity.Null)
            {
                x.Q<Label>().text = "";
                return;
            }

            var item = inventories[x.Container][x.Slot].Slot.Item;
            x.Name = GetComponent<Name>(item).Value.Value;
            if (HasComponent<Stackable>(item))
                x.Info = $"x{GetComponent<Stackable>(item).Size}";
            else
                x.Info = "";
        });


        if(GetSingleton<InputSystem.InputData>().ScrollMode == InputSystem.ScrollMode.Inventory)
            SelectHotbarItem(GetSingleton<InputSystem.InputData>().Scroll);
    }

    void SelectHotbarItem(float scroll)
    {
        EntityManager.RemoveComponent<SelectedItem>(GetEntityQuery(typeof(SelectedItem)));
        var buttons = uiRoot.Query<InventoryButton>().ToList();
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
