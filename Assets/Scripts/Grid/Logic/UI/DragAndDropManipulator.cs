using System;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace Grid.Logic
{
    public class DragAndDropManipulator : PointerManipulator
    {
        public Action OnStartDrag;
        public Action<float2> OnDrag;
        public Action<float2> OnDrop;

        float2 _startPosition;
        public DragAndDropManipulator() { }
        
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerMoveEvent>(OnDragEvent);
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerMoveEvent>(OnDragEvent);
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }

        void OnDragEvent(PointerMoveEvent evt)
        {
            if (!target.HasPointerCapture(evt.pointerId))
                return;
            OnDrag?.Invoke(new float2(evt.deltaPosition.x, evt.deltaPosition.y));
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            target.CapturePointer(evt.pointerId);
            _startPosition = new float2(evt.position.x, evt.position.y);
            OnStartDrag?.Invoke();
        }
        
        void OnPointerUp(PointerUpEvent evt)
        {
            target.ReleasePointer(evt.pointerId);
            var totalDelta = new float2(evt.position.x, evt.position.y) - _startPosition;
            OnDrop?.Invoke(totalDelta);
        }
    }
}