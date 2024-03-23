using Unity.Entities;
using UnityEngine;

public class InventoryAuthoring : MonoBehaviour
{
    public int Size;

    class Baker : Baker<InventoryAuthoring>
    {
        public override void Bake(InventoryAuthoring authoring)
        {
            var buffer = AddBuffer<Inventory>(GetEntity(TransformUsageFlags.None));
            for (int i = 0; i < authoring.Size; i++)
            {
                buffer.Add(new Inventory());
            }
        }
    }
}