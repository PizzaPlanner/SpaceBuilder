using Unity.Entities;

[GenerateAuthoringComponent]
public struct SteeringController : IComponentData
{
    public GridItem.Side Direction;
}

public partial class SteeringSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((in SteeringController steer, in DynamicBuffer<ControlledItems> items) =>
        {
            for (int i = 0; i < items.Length; i++)
            {

            }
        }).Schedule();
    }
}
