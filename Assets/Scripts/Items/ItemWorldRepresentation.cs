using Unity.Entities;

[GenerateAuthoringComponent]
public struct ItemWorldRepresentation : IComponentData
{
    public Entity Item;
}

public partial class PickupItemSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var inventories = GetBufferFromEntity<Inventory>(false);
        Entities.ForEach((Entity e, in ItemWorldRepresentation iw, in Interacted interaction) =>
        {
            if (!inventories.HasComponent(interaction.Interactor))
                return;

            var inv = inventories[interaction.Interactor];
            bool pickedUp = false;
            for(int i = 0; i < inv.Length; i++)
                if (inv[i].Slot.Item == Entity.Null)
                {
                    var slot = inv[i];
                    slot.Slot.Item = iw.Item;
                    inv[i] = slot;
                    pickedUp = true;
                }

            if(pickedUp)
                EntityManager.AddComponent<Disabled>(e);
        }).WithStructuralChanges().WithoutBurst().Run();
    }
}
