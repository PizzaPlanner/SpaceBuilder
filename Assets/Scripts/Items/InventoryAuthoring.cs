using Unity.Entities;
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

public class InventoryButton : VisualElement
{
    public Entity Owner;
    public int Slot;
    public InventoryButton(Entity owner, int slot)
    {
        Owner = owner;
        Slot = slot;
        AddToClassList("inventory-button");
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
            uiRoot.Add(new InventoryButton(GetSingletonEntity<LocalPlayer>(), i));
        }

    }

    protected override void OnStopRunning()
    {
        uiRoot.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {

    }
}
