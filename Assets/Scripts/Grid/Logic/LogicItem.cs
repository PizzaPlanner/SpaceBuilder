using Unity.Entities;
using Unity.Mathematics;

namespace Grid.Logic
{
    public struct LogicItem : IComponentData
    {
        public float2 SavedUIPosition;
    }

    // TODO: In 2022.2, The VisualElement itself was an IComponentData, but this seems to not work anymore in 2023.2
    // Maybe works again in the future
    public class LogicItemManaged : IComponentData
    {
        public LogicEditorElement VisualElement;
    }
}