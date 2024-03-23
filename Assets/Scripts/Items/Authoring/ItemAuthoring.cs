using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour
{
    [Range(1, 9999)]
    public ushort MaxStackSize;

    class Baker : Baker<ItemAuthoring>
    {
        public override void Bake(ItemAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new Item{ });
            if (authoring.MaxStackSize > 1)
                AddComponent(e, new Stackable {MaxSize = authoring.MaxStackSize, Size = 0});
        }
    }
}
