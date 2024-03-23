using UnityEngine.UIElements;

namespace Identifiables.UI
{
    public class WikiTagSearchButton : VisualElement
    {
        public WikiTagSearchButton(IdTag tag)
        {
            var label  = new Label{text = tag.ToString()};
            Add(label);
            RegisterCallback<MouseDownEvent>(x => GetFirstAncestorOfType<LandingPage>().Search($"tag={tag.ToString()}"));
        }
    }
}