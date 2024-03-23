using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class CraftingQueueElement : VisualElement
{
    public Entity Crafter;
    public int IdInQueue;

    private VisualElement progressbarFg;
    public CraftingQueueElement()
    {
        AddToClassList("crafting-queue-item");
        Add(new CraftingInputButton());
        var progressbarBg = new VisualElement();
        progressbarBg.AddToClassList("progress-background");
        progressbarFg = new VisualElement();
        progressbarFg.AddToClassList("progress-foreground");
        progressbarBg.Add(progressbarFg);
        Add(progressbarBg);
    }

    public void Update(EntityManager mgr)
    {
        var buffer = mgr.GetBuffer<CraftingQueue>(Crafter);
        if (buffer.Length <= IdInQueue)
            return;

        var item = buffer[IdInQueue];
        var recipe = mgr.GetComponentData<CraftingRecipe>(item.Recipe);
        var progress = 1 - (item.TimeLeft / recipe.Duration);
        var id = mgr.GetComponentData<Identifieable>(item.Recipe);
        this.Q<CraftingInputButton>().Input = new CraftingInput{ItemId = id, Amount = 0};
        progressbarFg.style.width = new StyleLength(new Length(progress * 100, LengthUnit.Percent));
    }

    public new class UxmlFactory : UxmlFactory<CraftingQueueElement, UxmlTraits> { }
}