using Unity.Entities;
using UnityEngine;

public class ConstructionInputAuthoring : MonoBehaviour
{
    public CraftingRecipeAuthoring.ItemWithAmount[] Cost;

    class Baker : Baker<ConstructionInputAuthoring>
    {
        public override void Bake(ConstructionInputAuthoring authoring)
        {
            var totalAmount = 0;
            var buffer = AddBuffer<ConstructionInputBuffer>();
            foreach (var input in authoring.Cost)
            {
                totalAmount += input.Amount;
                if (totalAmount >= ushort.MaxValue)
                {
                    Debug.LogError($"Maximum Construction cost is {ushort.MaxValue} items", authoring);
                    return;
                }

                buffer.Add(new ConstructionInputBuffer
                    {Amount = input.Amount, ItemId = input.Prefab.GetComponent<IdentifieableAuthoring>().Id});
            }
        }
    }
}
