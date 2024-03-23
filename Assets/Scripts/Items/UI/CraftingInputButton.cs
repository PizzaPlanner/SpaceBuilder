using UnityEngine.UIElements;

public class CraftingInputButton : ItemButton
{
    public CraftingInput Input
    {
        set
        {
            Name = "";
            Info = value.Amount > 0 ? $"{value.Amount}" : "";
            Image = value.ItemId.Icon;
            _input = value;
        }
        get => _input;
    }
    
    CraftingInput _input;

    public bool HasItems
    {
        set
        {
            if (value) RemoveFromClassList("missing");
            else AddToClassList("missing");
        }
        get => !ClassListContains("missing");
    }

    public new class UxmlFactory : UxmlFactory<CraftingInputButton, UxmlTraits> { }
}