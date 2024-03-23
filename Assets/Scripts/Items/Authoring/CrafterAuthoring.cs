using Unity.Entities;
using UnityEngine;

public class CrafterAuthoring : MonoBehaviour
{
    public InventoryAuthoring Input, Output;
    public IdentifieableAuthoring[] Catalysts;

    class Baker : Baker<CrafterAuthoring>
    {
        public override void Bake(CrafterAuthoring authoring)
        {
            var self = GetEntity(TransformUsageFlags.None);
            AddComponent(self, new Crafter 
            {
                InputInventory = GetEntity(authoring.Input, TransformUsageFlags.None),
                OutputInventory = GetEntity(authoring.Output, TransformUsageFlags.None)
            });
            AddBuffer<CraftingQueue>(self);
            AddBuffer<Craftables>(self);
            var catalysts = AddBuffer<CatalystInventory>(self);
            foreach (var item in authoring.Catalysts)
            {
                catalysts.Add(new CatalystInventory{ ItemId = item.Id });
            }
        }
    }
}