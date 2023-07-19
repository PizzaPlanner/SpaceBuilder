using Unity.Entities;
using Unity.Transforms;

[WithNone(typeof(Parent))]
public partial struct TravelSectorChangeJob : IJobEntity
{
    public EntityCommandBuffer ecb;
    void Execute(Entity e, ref LocalToGalaxy ltg, ref CanTravelSectors cts)
    {
        if (ltg != cts.PreviousSector && cts.CurrentTravelType != SectorChangeType.FTL)
        {
            var update = ecb.CreateEntity();
            ecb.AddComponent(update, new SectorChange
            {
                Entity = e,
                Previous = cts.PreviousSector,
                New = ltg,
                ChangeType = cts.CurrentTravelType
            });
        }

        cts.PreviousSector = ltg;
    }
}