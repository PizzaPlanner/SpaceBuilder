using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Main
{
    public class ActionButton : VisualElement
    {
        public ActionButton()
        {
            Add(new Label { name = "Hotkey", text = "X"});
        }
        
        public new class UxmlFactory : UxmlFactory<ActionButton, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            UxmlStringAttributeDescription _defaultHotkey = new UxmlStringAttributeDescription { name = "Hotkey" };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                ((ActionButton)ve).Q<Label>().text = _defaultHotkey.GetValueFromBag(bag, cc);
            }
        }
    }
}