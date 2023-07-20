using UnityEngine.UIElements;

public class SectorGlyph : VisualElement
{
    public uint Bytemask;

    public SectorGlyph()
    {
        this.AddToClassList("coorsys-segment-input");
        this.AddToClassList($"segment-{Bytemask}");

        var orderOfOperations = new int[] {0, 4, 1, 7, 8, 5, 2, 6, 3};

        for (int i = 0; i < orderOfOperations.Length; i++)
        {
            var elm = new VisualElement();
            elm.style.width = new Length(33, LengthUnit.Percent);
            elm.style.height = new Length(33, LengthUnit.Percent);
            elm.AddToClassList("coorsys-segment-input__button");
            var i1 = orderOfOperations[i];
            elm.RegisterCallback<MouseEnterEvent>(x =>
            {
                if (x.pressedButtons != 1)
                    return;
                Bytemask ^= (uint)(0x1 << i1);
                this.ClearClassList();
                this.AddToClassList("coorsys-segment-input");
                this.AddToClassList($"segment-{Bytemask}");
            });
            elm.RegisterCallback<MouseDownEvent>(x =>
            {
                if (x.button != 0)
                    return;
                Bytemask ^= (uint)(0x1 << i1);
                this.ClearClassList();
                this.AddToClassList("coorsys-segment-input");
                this.AddToClassList($"segment-{Bytemask}");
            });
            this.Add(elm);
        }
    }

    public new class UxmlFactory : UxmlFactory<SectorGlyph, UxmlTraits> { }
}
