using Grid.Essentials;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Grid.Logic
{
    public class LogicEditorElement : VisualElement
    {
        public float2 Position
        {
            set
            {
                _position = value;
                this.style.left = _position.x;
                this.style.top = _position.y;
                LogicEditorWindow.Instance.UpdateOverlay();
            }
            get => _position;
        }

        float2 _position;
        
        public string Name
        {
            set
            {
                this.Q<Label>("Name").text = value;
            }
        }

        VisualElement _portsContainer;
        DragAndDropManipulator _dragger;

        public LogicEditorElement()
        {
            Add(new Label { name = "Name" });
            _portsContainer = new VisualElement { name = "Ports" };
            Add(_portsContainer);
            _dragger = new DragAndDropManipulator
            {
                OnDrag = offset => Position += offset,
                OnDrop = _ => Position = math.clamp(Position - Position % 10, 0, 
                    new float2(parent.resolvedStyle.width, parent.resolvedStyle.height) - 30)
            };
            this.Q<Label>("Name").AddManipulator(_dragger);
        }

        public LogicPortElement GetPort(int i) => _portsContainer[i] as LogicPortElement;

        public NativeArray<LogicPort> Ports
        {
            set
            {
                _portsContainer.Clear();
                foreach (var port in value)
                    _portsContainer.Add(new LogicPortElement { Port = port });
            }
        }

        public new class UxmlFactory : UxmlFactory<LogicEditorElement, UxmlTraits> { }
    }
}