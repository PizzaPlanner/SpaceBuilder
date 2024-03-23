using System.Linq;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Identifiables.UI
{
    public class Wiki : VisualElement
    {
        public Wiki()
        {
            var landing = new LandingPage();
            var search = new TextField{name = "Search"};
            search.RegisterCallback<InputEvent>(x =>
            {
                landing.Search(x.newData.Trim());
            });
            var tabs = new TabView();
            var landingTab = new Tab("Home");
            landingTab.closeable = false;
            landingTab.Add(landing);
            tabs.Add(landingTab);
            Add(search);
            Add(tabs);
        }
        
        public void OpenTab(Identifieable id)
        {
            var openTabs = this.Q<TabView>().Query<DetailPage>().Where(x => x.Content == id);
            if (openTabs.ToList().Count == 0)
            {
                var tab = new Tab(id.DefaultName.Value);
                tab.closeable = true;
                tab.closed += (e) => e.RemoveFromHierarchy();
                tab.Add(new DetailPage { Content = id });
                this.Q<TabView>().Add(tab);
                this.Q<TabView>().activeTab = tab;
            }
            else
                this.Q<TabView>().activeTab = openTabs.First().GetFirstAncestorOfType<Tab>();
        }
        
        public new class UxmlFactory : UxmlFactory<Wiki, UxmlTraits> { }

        bool didInit;
        public void Init(World world)
        {
            if(didInit)
                return;
            
            this.Q<LandingPage>().Init();
            didInit = true;
        }
    }
}