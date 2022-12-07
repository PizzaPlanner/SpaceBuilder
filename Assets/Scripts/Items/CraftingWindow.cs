using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class CraftingWindow : VisualElement
{
    public Entity Crafter { get; private set; }

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

    public void SetCrafter(Entity crafter, EntityManager mgr)
    {
        Crafter = crafter;
        recipes.Clear();
        var buffer = mgr.GetBuffer<CraftableBuffer>(Crafter);
        for (int i = 0; i < buffer.Length; i++)
        {
            recipes.Add(new CraftingRecipeElement{Recipe = buffer[i].Recipe, Crafter = Crafter});
        }
    }

    public void UpdateRecipes(EntityManager mgr, ComponentLookup<Identifieable> idBuffer, ComponentLookup<Stackable> stackBuffer)
    {
        recipes.Query<CraftingRecipeElement>().ForEach(x => x.Update(mgr, idBuffer, stackBuffer));
    }

    public void UpdateQueue(EntityManager mgr)
    {
        var buffer = mgr.GetBuffer<CraftingBuffer>(Crafter);
        for (int i = 0; i < buffer.Length; i++)
        {
            if(queue.childCount <= i)
                queue.Add(new CraftingQueueElement{Crafter = Crafter, IdInQueue = i});
            if(queue.childCount > i)
                queue[i].Q<CraftingQueueElement>().Update(mgr);
        }
        for(int i = buffer.Length; i < queue.childCount; i++)
            queue[i].RemoveFromHierarchy();
    }

    public new class UxmlFactory : UxmlFactory<CraftingWindow, UxmlTraits> { }
}

public class CraftingQueueElement : VisualElement
{
    public Entity Crafter;
    public int IdInQueue;

    private VisualElement progressbarFg;
    public CraftingQueueElement()
    {
        AddToClassList("crafting-queue-item");
        var progressbarBg = new VisualElement();
        progressbarBg.AddToClassList("progress-background");
        progressbarFg = new VisualElement();
        progressbarFg.AddToClassList("progress-foreground");
        progressbarBg.Add(progressbarFg);
        Add(progressbarBg);
    }

    public void Update(EntityManager mgr)
    {
        var buffer = mgr.GetBuffer<CraftingBuffer>(Crafter);
        if (buffer.Length <= IdInQueue)
            return;

        var item = buffer[IdInQueue];
        var recipe = mgr.GetComponentData<CraftingRecipe>(item.Recipe);
        var progress = 1 - (item.TimeLeft / recipe.Duration);
        progressbarFg.style.width = new StyleLength(new Length(progress * 100, LengthUnit.Percent));
    }

    public new class UxmlFactory : UxmlFactory<CraftingQueueElement, UxmlTraits> { }
}

public class CraftingRecipeElement : VisualElement
{
    public Entity Recipe;
    public Entity Crafter;
    public bool Craftable { get; private set; }
    private VisualElement inputList, outputList, cataList;

    private Action OnClick;

    public CraftingRecipeElement()
    {
        AddToClassList("crafting-recipe");
        inputList = new VisualElement();
        inputList.AddToClassList("item-list");
        Add(inputList);
        outputList = new VisualElement();
        outputList.AddToClassList("item-list");
        Add(outputList);
        cataList = new VisualElement();
        cataList.AddToClassList("item-list");
        Add(cataList);
        RegisterCallback<MouseDownEvent>(_ => OnClick?.Invoke());
    }

    public void Update(EntityManager mgr, ComponentLookup<Identifieable> idBuffer, ComponentLookup<Stackable> stackBuffer)
    {
        Craftable = true;
        var queue = mgr.GetComponentData<CraftingQueue>(Crafter);

        outputList.Clear();
        if (mgr.HasComponent<CraftingOutput>(Recipe))
        {
            var outputs = mgr.GetBuffer<CraftingOutput>(Recipe);
            foreach (var item in outputs)
            {
                var btn = new CraftingInputButton();
                btn.Input = new CraftingInput {Amount = item.Amount, ItemId = item.ItemId};
                btn.Name = mgr.GetComponentData<Name>(IdentifiableMappingSystem.EntitiesById[item.ItemId]).Value.Value;
                btn.Info = $"{item.Amount}";
                btn.HasItems = true;
                outputList.Add(btn);
            }
        }

        inputList.Clear();
        if (mgr.HasComponent<CraftingInput>(Recipe))
        {
            var inputs = mgr.GetBuffer<CraftingInput>(Recipe);
            foreach (var item in inputs)
            {
                var btn = new CraftingInputButton();
                btn.Input = new CraftingInput {Amount = item.Amount, ItemId = item.ItemId};
                btn.Name = mgr.GetComponentData<Name>(IdentifiableMappingSystem.EntitiesById[item.ItemId]).Value.Value;
                btn.Info = $"{item.Amount}";
                if (!mgr.GetBuffer<Inventory>(queue.InputInventory)
                        .Contains(idBuffer, stackBuffer, item.ItemId, item.Amount))
                {
                    Craftable = false;
                    btn.HasItems = false;
                } else
                    btn.HasItems = true;

                inputList.Add(btn);
            }
        }

        cataList.Clear();
        if (mgr.HasComponent<CraftingCatalyst>(Recipe))
        {
            var catalysts = mgr.GetBuffer<CraftingCatalyst>(Recipe);
            foreach (var item in catalysts)
            {
                var btn = new CraftingInputButton();
                btn.Input = new CraftingInput {Amount = 0, ItemId = item.ItemId};
                btn.Name = mgr.GetComponentData<Name>(IdentifiableMappingSystem.EntitiesById[item.ItemId]).Value.Value;
                btn.Info = "";
                if (!mgr.GetBuffer<Inventory>(queue.CatalystInventory)
                        .Contains(idBuffer, stackBuffer, item.ItemId, 1))
                {
                    Craftable = false;
                    btn.HasItems = false;
                } else
                    btn.HasItems = true;

                cataList.Add(btn);
            }
        }

        if (Craftable)
        {
            RemoveFromClassList("crafting-recipe__uncraftable");
            var timeToCraft = mgr.GetComponentData<CraftingRecipe>(Recipe).Duration;
            OnClick = () =>
            {
                mgr.GetBuffer<CraftingBuffer>(Crafter).Add(new CraftingBuffer
                    {Recipe = Recipe, TimeLeft = timeToCraft});
            };
        }
        else
        {
            AddToClassList("crafting-recipe__uncraftable");
            OnClick = null;
        }
    }

    public new class UxmlFactory : UxmlFactory<CraftingRecipeElement, UxmlTraits> { }
}
