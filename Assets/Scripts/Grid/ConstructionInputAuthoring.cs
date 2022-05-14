using Unity.Entities;
using UnityEngine;

public class ConstructionInputAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public CraftingRecipeAuthoring.ItemWithAmount[] Cost;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var totalAmount = 0;
        var buffer = dstManager.AddBuffer<ConstructionInputBuffer>(entity);
        foreach (var input in Cost)
        {
            totalAmount += input.Amount;
            if (totalAmount >= ushort.MaxValue)
            {
                Debug.LogError($"Maximum Construction cost is {ushort.MaxValue} items", this);
                return;
            }

            buffer.Add(new ConstructionInputBuffer
                {Amount = input.Amount, ItemId = input.Prefab.GetComponent<IdentifieableAuthoring>().Id});
        }
    }
}
