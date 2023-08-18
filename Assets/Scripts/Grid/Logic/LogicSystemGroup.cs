using Unity.Entities;

namespace Grid.Logic
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class LogicSystemGroup : ComponentSystemGroup { }
}