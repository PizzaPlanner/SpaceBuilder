using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

public struct BigObjectInPoiRange : IComponentData { }

[UpdateInGroup(typeof(UpdatePresentationSystemGroup))]
public partial class MarkObjectsInRangeSystem : SystemBase
{
    public const double IN_RANGE = 2.0 * Constants.MAX_POSITION *
                                   Constants.MAX_SUBSECTOR;

    public const double IN_RANGE_SQ = IN_RANGE * IN_RANGE;

    private EntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        bufferSystem = World.GetExistingSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var playerLtg = GetComponent<LocalToGalaxy>(GetSingletonEntity<LocalPlayer>());

        var buffer = bufferSystem.CreateCommandBuffer();

        Entities
            .WithAny<Planet, Star>()
            .WithNone<BigObjectInPoiRange>()
            .ForEach((Entity e, in LocalToGalaxy ltg) =>
            {
                if(math.lengthsq(LocalToGalaxy.RelativePosition(ltg, playerLtg)) < IN_RANGE_SQ)
                    buffer.AddComponent<BigObjectInPoiRange>(e);
            }).Schedule();
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
