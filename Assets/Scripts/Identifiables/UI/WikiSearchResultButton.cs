using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Identifiables.UI
{
    public class WikiSearchResultButton : VisualElement
    {
        Identifieable _target;
        public WikiSearchResultButton(Identifieable target, bool allowRecurse)
        {
            _target = target;
            var icon = new VisualElement{name = "Icon"};
            icon.style.backgroundImage = target.Icon;
            Add(icon);
            RegisterCallback<MouseDownEvent>(x => GetFirstAncestorOfType<Wiki>().OpenTab(target));
            if (allowRecurse && (target.Tags & IdTag.Recipe) != 0)
            {
                var recipeContent = new VisualElement{name = "RecipeContent"};
                Add(recipeContent);
                foreach(var link in target.References)
                    recipeContent.Add(new WikiIdLinkButton(link, false));
                
            } else
                Add(new Label{text = target.DefaultName.Value});
        }

        public void ApplySearch(string text)
        {
            text = text.ToLower();
            var isIdSearch = text.StartsWith("id=");
            var isTagSearch = text.StartsWith("tag=");
            var isNormalSearch = !text.Contains("=");
            text = text.Trim().Split('=', StringSplitOptions.RemoveEmptyEntries).Last();
            var found = false;
            found |= isIdSearch && text.Length > 2 && _target.ItemId.ToString().Contains(text);
            found |= isTagSearch && text.Length > 2 && _target.Tags.ToString().ToLower().Contains(text);
            found |= isNormalSearch && text.Length > 2 && _target.DefaultName.ToString().ToLower().Contains(text);
            style.display = found
                ? DisplayStyle.Flex 
                : DisplayStyle.None;
        }
    }
}