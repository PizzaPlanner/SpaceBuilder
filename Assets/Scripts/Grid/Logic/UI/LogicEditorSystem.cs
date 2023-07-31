using System.Runtime.InteropServices.ComTypes;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace Grid.Logic
{
    public partial class LogicEditorSystem : SystemBase
    {
        LogicEditorWindow root;
        EntityQuery _displayQuery;

        protected override void OnCreate()
        {
            _displayQuery = GetEntityQuery(typeof(Interacted), typeof(LogicDisplay), typeof(Parent));
            RequireAnyForUpdate(
                GetEntityQuery(typeof(LogicEditorWindow), typeof(ControlScheme.UI.LogicDisplay)), 
                _displayQuery);
        }

        protected override void OnStartRunning()
        {
            root = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<LogicEditorWindow>();
            root.style.display = DisplayStyle.Flex;
            var window = EntityManager.CreateEntity(typeof(LogicEditorWindow), typeof(ControlScheme.UI.LogicDisplay));
            EntityManager.SetComponentData(window, root);

            var grid = _displayQuery.GetSingleton<Parent>().Value;
            root.Grid = grid;
            root.Clear();
            
            // Add UI stuff for all relevant parts
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (parent, ports, name, e) in 
                     SystemAPI.Query<Parent, DynamicBuffer<LogicPort>, Name>()
                              .WithAll<GridItem>()
                              .WithNone<LogicEditorElement>()
                              .WithEntityAccess())
            {
                if(parent.Value != root.Grid)
                    continue;
                
                var element = new LogicEditorElement { Name = name.Value.Value, Ports = ports.AsNativeArray() };
                ecb.AddComponent(e, element);
            }
            ecb.Playback(EntityManager);
            
            foreach (var (editor, parent, e) in 
                     SystemAPI.Query<LogicEditorElement, Parent>().WithEntityAccess())
            {
                if(parent.Value != root.Grid)
                    continue;

                if(root.Contains(editor))
                    continue;
                
                root.AddLogicElement(editor, e);
            }
        }

        protected override void OnStopRunning()
        {
            root.style.display = DisplayStyle.None;
        }

        protected override void OnUpdate()
        {
            if(SystemAPI.HasSingleton<ControlScheme.UI.TabMenu>())
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<ControlScheme.UI.LogicDisplay>());

            foreach (var (editor, ports, parent, e) in
                     SystemAPI.Query<LogicEditorElement, DynamicBuffer<LogicPort>, Parent>().WithAll<GridItem>().WithEntityAccess())
            {
                if(parent.Value != root.Grid)
                    continue;
                
                var portsRW = ports;
                for (int i = 0; i < ports.Length; i++)
                {
                    // Apply new connections from UI
                    if (editor.GetPort(i).ProposedConnection != null)
                    {
                        var port = editor.GetPort(i);
                        var target = port.ProposedConnection;
                        var index = target.parent.IndexOf(target);
                        var newConnection = new LogicInput
                        {
                            FromEntity = root.GetEntityFor(target.GetFirstAncestorOfType<LogicEditorElement>()),
                            FromPort = (byte)index
                        };
                        var portData = ports[i];
                        portData.Connection = newConnection;
                        portsRW[i] = portData;
                        root.UpdateOverlay();
                    }
                    // Propagate current state to UI
                    editor.GetPort(i).Connection = ports[i].Connection;
                    editor.GetPort(i).Data = ports[i].Data;
                }
            }
        }
    }
}