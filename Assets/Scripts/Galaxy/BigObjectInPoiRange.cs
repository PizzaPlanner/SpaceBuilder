using Galaxy;
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

    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        var playerLtg = SystemAPI.GetComponent<LocalToGalaxy>(SystemAPI.GetSingletonEntity<LocalPlayer>());

        var buffer = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                              .CreateCommandBuffer(World.Unmanaged);

        Entities
            .WithAll<CelestialBody>()
            .WithNone<BigObjectInPoiRange>()
            .ForEach((Entity e, in LocalToGalaxy ltg) =>
            {
                if(math.lengthsq(LocalToGalaxy.RelativePosition(ltg, playerLtg)) < IN_RANGE_SQ)
                    buffer.AddComponent<BigObjectInPoiRange>(e);
            }).Schedule();
    }
}
