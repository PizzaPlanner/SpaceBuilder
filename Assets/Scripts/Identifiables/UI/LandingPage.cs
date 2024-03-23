using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Identifiables.UI
{
    public class LandingPage : ScrollView
    {
        VisualElement quickNav, searches;
        Tab searchTab;
        Label searchHint;
        public LandingPage()
        {
            quickNav = new VisualElement() { name = "navigate" };
            Add(new Label($"Welcome to the Wiki"){name = "Title"});
            Add(new Label($"0 Entries loaded"){name = "EntriesHint"});
            Add(quickNav);
            
            searchTab = new Tab("Find");
            searches = new ScrollView();
            searchTab.Add(searches);
            searchHint = new Label();
            searches.Add(searchHint);
            searches.Add(new VisualElement(){ name = "navigate" });
        }

        public void Init()
        {
            var all = Identifieable.All;
            var itemsPerTag = new List<IdTag>();
            var target = searches.Q("navigate");
            foreach (var id in all)
            {
                target.Add(new WikiSearchResultButton(id, false));
                if(!itemsPerTag.Contains(id.Tags))
                    itemsPerTag.Add(id.Tags);
            }

            foreach (var tag in itemsPerTag.Distinct())
            {
                quickNav.Add(new WikiTagSearchButton(tag));
            }
            GetFirstAncestorOfType<TabView>().activeTabChanged += (x,y)
                => {
                if(y != searchTab)
                    searchTab.RemoveFromHierarchy();
            };
            this.Q<Label>("EntriesHint").text = $"{all.Length} Entries loaded";
            ResetSearch();
        }

        public void ResetSearch()
        {
            searchTab.RemoveFromHierarchy();
            GetFirstAncestorOfType<TabView>().activeTab = GetFirstAncestorOfType<Tab>();
        }

        public void Search(string content)
        {
            GetFirstAncestorOfType<TabView>().Add(searchTab);
            GetFirstAncestorOfType<TabView>().activeTab = searchTab;
            searchHint.text = content.Length > 2 ? $"Searching for '{content}'" : "Search string is too short";
            searches.Query<WikiSearchResultButton>().ForEach(x => x.ApplySearch(content));
        }
        
        public new class UxmlFactory : UxmlFactory<LandingPage, UxmlTraits> { }
    }
}