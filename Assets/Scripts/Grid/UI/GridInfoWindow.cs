using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace Grid.UI
{
    public class GridInfoWindow : VisualElement
    {
        public GridInfoWindow()
        {
            Add(new TextField { name = "Name", value = "My Blueprint"});
            Add(new Button(OnExport) { name = "Export", text = "Export as Blueprint" });
            Add(new Label { name = "Stats" });
        }

        public Entity GridEntity;

        public void OnExport()
        {
            BlueprintUtils.Save(GridEntity);
        }

        public string Name
        {
            set => this.Q<TextField>("Name").value = value;
            get => this.Q<TextField>("Name").value;
        }
        
        public new class UxmlFactory : UxmlFactory<GridInfoWindow, UxmlTraits> { }

        public void SetStats(GridStructure grid, IEnumerable<Identifieable> blocks)
        {
            var text = $@"Stats:
- Size: {grid.LocalAABB.Size}
- Content: 
    {string.Join("\n    ", blocks
                           .GroupBy(x => x.ItemId)
                           .Select(x => $"{x.Key} x {x.Count()}"))}";

            this.Q<Label>("Stats").text = text;
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    partial class GridInfoUISystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<Target>();
            RequireForUpdate<ControlScheme.UI.TabMenu>();
        }

        Entity _currentGrid;
        GridInfoWindow root;
        
        protected override void OnUpdate()
        {
            var target = SystemAPI.GetComponent<Target>(SystemAPI.GetSingletonEntity<LocalPlayer>());
            var isNewTarget = _currentGrid != target.GridHit.Grid;
            _currentGrid = target.GridHit.Grid;
            if (_currentGrid == Entity.Null)
            {
                OnHideUI();
                return;
            }

            if (isNewTarget)
                OnShowUI();

            var name = SystemAPI.GetComponentRW<Name>(_currentGrid);
            name.ValueRW.Value = root.Name;
        }

        protected override void OnStopRunning()
        {
            OnHideUI();
        }

        protected override void OnStartRunning()
        {
            root = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<GridInfoWindow>();
        }

        void OnHideUI()
        {
            root.style.display = DisplayStyle.None;
            _currentGrid = Entity.Null;
        }

        void OnShowUI()
        {
            root.GridEntity = _currentGrid;
            root.Name = SystemAPI.GetComponent<Name>(_currentGrid).Value.Value;
            
            var children = SystemAPI.GetBuffer<Child>(_currentGrid);
            var ids = new List<Identifieable>();
            foreach (var child in children)
            {
                if(!SystemAPI.HasComponent<Identifieable>(child.Value))
                    continue;
                
                var id = SystemAPI.GetComponent<Identifieable>(child.Value);
                ids.Add(id);
            }
            root.SetStats(SystemAPI.GetComponent<GridStructure>(_currentGrid), ids);
            
            root.style.display = DisplayStyle.Flex;
        }
    }
}