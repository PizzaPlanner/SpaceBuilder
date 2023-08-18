using Grid.Essentials;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Grid.Logic
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class LogicEditorWindow : VisualElement, IComponentData
    {
        public Entity Grid;
        public static LogicEditorWindow Instance;
        
        public LogicEditorWindow()
        {
            _cellToElementMap = new Dictionary<Entity, LogicEditorElement>();
            _content = new VisualElement { name = "Content" };
            hierarchy.Add(_content);
            _overlay = new VisualElement { name = "Overlay" };
            _overlay.pickingMode = PickingMode.Ignore;
            _overlay.generateVisualContent += OnDrawConnections;
            hierarchy.Add(_overlay);
        }

        VisualElement _overlay, _content;
        Dictionary<Entity, LogicEditorElement> _cellToElementMap;

        public override VisualElement contentContainer => _content;

        public void AddLogicElement(LogicEditorElement editor, Entity origin)
        {
            editor.style.position = Position.Absolute;
            _content.Add(editor);
            _cellToElementMap[origin] = editor;
            
        }
        
        public new class UxmlFactory : UxmlFactory<LogicEditorWindow, UxmlTraits> { }

        public LogicEditorElement GetItemFor(Entity e)
        {
            return _cellToElementMap[e];
        }

        public Entity GetEntityFor(LogicEditorElement e)
        {
            foreach(var kvp in _cellToElementMap)
                if (kvp.Value == e)
                    return kvp.Key;
            return Entity.Null;
        }

        public void UpdateOverlay()
        {
            _overlay.MarkDirtyRepaint();
        }
        
        void OnDrawConnections(MeshGenerationContext mgc)
        {
            var paint2D = mgc.painter2D;

            var i = 1;
            foreach (var port in this.Query<LogicPortElement>().Build())
            {
                i++;
                if (port.InputPort != null)
                {
                    var p1 = port.InputPort.PortAnchor;
                    var p2 = port.PortAnchor;
                    p1 = this.WorldToLocal(p1);
                    p2 = this.WorldToLocal(p2);

                    DrawConnection(paint2D, p1, p2, i, false);
                }

                if (port.IsDragging)
                {
                    var p1 = this.WorldToLocal(port.PortAnchor);
                    var p2 = this.WorldToLocal((float2)port.PortAnchor + port.DragTarget);
                    DrawConnection(paint2D, p1, p2, i, true);
                }
            }
        }

        void DrawConnection(Painter2D paint2D, Vector2 p1, Vector2 p2, int i, bool isDragging)
        {
            var p21 = p2 - new Vector2(4, 0) * i * math.sign(p2.x - p1.x);
            var p12 = new Vector2(p21.x, p1.y);
            var center = math.lerp(p1, p2, 0.5f);
            paint2D.strokeColor = isDragging ? Color.yellow : Color.cyan;
            paint2D.lineCap = LineCap.Round;
            paint2D.lineWidth = 2f;
            paint2D.BeginPath();
            paint2D.MoveTo(p1);
            paint2D.LineTo(p12);
            paint2D.LineTo(p21);
            paint2D.LineTo(p2);
            paint2D.Stroke();
        }
    }
}