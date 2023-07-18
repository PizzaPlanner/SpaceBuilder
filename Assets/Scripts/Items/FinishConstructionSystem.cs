using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace Parts
{
    [UpdateBefore(typeof(ProgressConstructionSystem))]
    public partial class FinishConstructionSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<IdentifiableMappingSystem.Loaded>();
        }

        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);
            var ecbNow = new EntityCommandBuffer(Allocator.Temp);
            EntityManager.DestroyEntity(GetEntityQuery(typeof(Disabled), typeof(FinishConstruction)));
            foreach (var (transform, bp) in SystemAPI.Query<LocalTransform>()
                                                     .WithAll<FinishConstruction, UnderConstruction>()
                                                     .WithEntityAccess())
            {
                var pp = ecbNow.CreateEntity();
                var rotation = transform.Rotation;
                ecbNow.AddComponent<UnderConstruction>(pp);
                ecbNow.AddComponent(pp, new PendingPart
                {
                    Data = new BuildingPartInfo
                    {
                        Id = GetComponent<Identifieable>(bp).ItemId,
                        Area = GetComponent<GridItem>(bp).Area,
                        Orientation = new RightAngleRotation(rotation),
                        BuildStatus = ushort.MaxValue
                    },
                    Grid = GetComponent<GridItem>(bp).Grid
                });
                ecbNow.AddComponent<Disabled>(bp);
            }

            ecbNow.Playback(EntityManager);
            
            var ppquery = GetEntityQuery(typeof(UnderConstruction), typeof(PendingPart));
            new BuildingSystem.BuildJob
            {
                buffer = ecb,
                grids = GetComponentLookup<GridStructure>(true),
                gridItems = GetComponentLookup<GridItem>(true),
                colliders = GetComponentLookup<PhysicsCollider>(true),
                occupations = GetComponentLookup<CellMap>(true),
                prefabMap = IdentifiableMappingSystem.EntitiesById,
                IsFinishConstructionJob = true
            }.Run(ppquery);
            EntityManager.DestroyEntity(ppquery);
        }
    }
}