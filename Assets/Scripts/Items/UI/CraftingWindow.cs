using Unity.Entities;
using UnityEngine.UIElements;

public class CraftingWindow : VisualElement, IComponentData
{
    public Entity Queue { get; set; }

    private VisualElement recipes, queue;

    public CraftingWindow()
    {
        queue = new VisualElement();
        queue.AddToClassList("queue-list");
        Add(queue);
        recipes = new VisualElement();
        recipes.AddToClassList("recipe-list");
        Add(recipes);
    }

    public DynamicBuffer<Craftables> Craftables
    {
        set
        {
            var mgr = World.All[0].EntityManager;
            foreach(var recipe in value)
                recipes.Add(new CraftingRecipeElement
                {
                    Recipe = mgr.GetAspect<RecipeAspect>(recipe.Recipe.AsPrefab)
                });
        }
    }

    public void UpdateRecipes(EntityManager mgr, ItemAspect.Lookup itemLookup)
    {
        var crafter = mgr.GetComponentData<Crafter>(Queue);
        var inputs = mgr.GetBuffer<Inventory>(crafter.InputInventory);
        var catalysts = mgr.GetBuffer<CatalystInventory>(Queue);
        var buffer = mgr.GetBuffer<CraftingQueue>(Queue);
        foreach (var recipeBtn in recipes.Query<CraftingRecipeElement>().Build())
        {
            if(recipeBtn.Update(inputs, catalysts, itemLookup))
                buffer.Add(new CraftingQueue
                {
                    Recipe = recipeBtn.Entity, 
                    TimeLeft = mgr.GetAspect<RecipeAspect>(recipeBtn.Entity).Duration
                });
        }
    }

    public void UpdateQueue(EntityManager mgr)
    {
        var buffer = mgr.GetBuffer<CraftingQueue>(Queue);
        for (int i = 0; i < buffer.Length; i++)
        {
            if(queue.childCount <= i)
                queue.Add(new CraftingQueueElement{Crafter = Queue, IdInQueue = i});
            if(queue.childCount > i)
                queue[i].Q<CraftingQueueElement>().Update(mgr);
        }
        for(int i = buffer.Length; i < queue.childCount; i++)
            queue[i].RemoveFromHierarchy();
    }

    public new class UxmlFactory : UxmlFactory<CraftingWindow, UxmlTraits> { }
}