using System.Collections.Generic;
using System.Globalization;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Grid.Logic
{
    public class LogicPortElement : VisualElement
    {
        DragAndDropManipulator _dragger;
        VisualElement _port;
        public LogicPortElement()
        {
            Add(new Label { name = "Name" });
            _port = new VisualElement() { name = "Port" };
            Add(_port);
            Add(new Label { name = "Value" });
            _dragger = new DragAndDropManipulator();
            _dragger.OnStartDrag = () =>
            {
                _dragTarget = 0;
                _port.AddToClassList("dragging");
            };
            _dragger.OnDrag = delta =>
            {
                _dragTarget += delta;
                GetFirstAncestorOfType<LogicEditorWindow>().UpdateOverlay();
            };
            _dragger.OnDrop = delta =>
            {
                _port.RemoveFromClassList("dragging");
                if (_highlightedPort != null && _highlightedPort != this)
                {
                    if (_highlightedPort.ClassListContains("write") && this.ClassListContains("read"))
                        _highlightedPort.ProposedConnection = this;
                    else if(this.ClassListContains("write") && _highlightedPort.ClassListContains("read"))
                        ProposedConnection = _highlightedPort;
                }
            };
            _port.AddManipulator(_dragger);
            RegisterCallback<PointerEnterEvent>(_ => { _highlightedPort = this;});
            RegisterCallback<PointerLeaveEvent>(_ => { if(_highlightedPort == this) _highlightedPort = null;});
        }

        static LogicPortElement _highlightedPort;

        float2 _dragTarget;

        public bool IsDragging => _port.ClassListContains("dragging");
        public float2 DragTarget => _dragTarget;
        
        public Vector2 PortAnchor => this.Q("Port").worldBound.center;
        public LogicPortElement InputPort;

        public LogicPort Port
        {
            set
            {
                ClearClassList();
                this.Q<Label>("Name").text = value.Type.ToString();
                if (value.CanRead)
                {
                    AddToClassList("read");
                    AddToClassList("right");
                }
                if (value.CanWrite)
                {
                    AddToClassList("write");
                    AddToClassList("left");
                }
            }
        }

        public LogicPortElement ProposedConnection;
        public LogicInput Connection
        {
            set
            {
                if (value.FromEntity == Entity.Null)
                    InputPort = null;
                else
                    InputPort = GetFirstAncestorOfType<LogicEditorWindow>().GetItemFor(value.FromEntity)
                                                                           .GetPort(value.FromPort);
            }
        }

        public LogicData Data
        {
            set
            {
                this.Q<Label>("Value").text = value.UseFractions 
                    ? value.AsDouble.ToString("g2", CultureInfo.InvariantCulture) 
                    : value.AsLong.ToString("g4");
            }
        }
    }
}