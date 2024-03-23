using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour
{
    [Range(1, 9999)]
    public ushort MaxStackSize;
    [Range(1, 9999)]
    public ushort Stack;

    public Texture2D Icon;

    class Baker : Baker<ItemAuthoring>
    {
        public override void Bake(ItemAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new Item{ });
            if (authoring.MaxStackSize > 1)
                AddComponent(e, new Stackable {MaxSize = authoring.MaxStackSize, Size = authoring.Stack});

            if (authoring.Icon == null)
                return;

            AddComponentObject(e, new IconReference {Icon = authoring.Icon});
            AddComponent(e, new Parent());
        }
    }
}
