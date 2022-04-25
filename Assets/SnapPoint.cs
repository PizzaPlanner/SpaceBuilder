using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace SnapPoints
{

    public interface ISnapComponent : IComponentData { }

    public class PowerShowSnapPointSystem : ShowSnapPointSystem<PowerShowSnapPointSystem.Tag> { public struct Tag : ISnapComponent { } }
    public class GasShowSnapPointSystem : ShowSnapPointSystem<GasShowSnapPointSystem.Tag> { public struct Tag : ISnapComponent { } }
    public class WallShowSnapPointSystem : ShowSnapPointSystem<WallShowSnapPointSystem.Tag> { public struct Tag : ISnapComponent { } }

    public enum SnapType
    {
        Power,
        Gas,
        Wall
    }

    public struct IsSnapPoint : IComponentData
    {
        public GridItem.Side Side;
        public Entity GridItem;
    }

    public struct SnapPointConnected : IComponentData { }

    public class SnapPoint : MonoBehaviour, IConvertGameObjectToEntity
    {
        public SnapType Type;
        public GridItem.Side Side;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            var parent = transform.parent.gameObject;
            while (!parent.TryGetComponent<BuildingPart>(out _) && !parent.TryGetComponent<BuildingSubPart>(out _))
                parent = transform.parent.gameObject;
            dstManager.AddComponentData(entity, new IsSnapPoint{Side = Side, GridItem = conversionSystem.GetPrimaryEntity(parent)});
            switch(Type)
            {
                case SnapType.Power:
                    dstManager.AddComponent<PowerShowSnapPointSystem.Tag>(entity);
                    break;
                case SnapType.Gas:
                    dstManager.AddComponent<GasShowSnapPointSystem.Tag>(entity);
                    break;
                case SnapType.Wall:
                    dstManager.AddComponent<WallShowSnapPointSystem.Tag>(entity);
                    break;
            }
        }
    }

    [UpdateInGroup(typeof(GridRecalculationSystemGroup))]
    public partial class SnapPointRecalculationSystem : SystemBase
    {
        private EndInitializationEntityCommandBufferSystem bufferSystem;

        protected override void OnCreate()
        {
            bufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var buffer = bufferSystem.CreateCommandBuffer();
            Entities
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                .ForEach((Entity e, in IsSnapPoint sp) =>
            {
                var rotation = GetComponent<Rotation>(sp.GridItem);
                var rotatedSide = sp.Side.Rotate(rotation.Value);
                var gridConnections = GridItem.Side.Center;
                var connections = GetBuffer<ConnectionBuffer>(sp.GridItem);
                for (int i = 0; i < connections.Length; i++)
                    gridConnections |= connections[i].Side;
                var isConnected = (gridConnections & rotatedSide) > 0;
                if (isConnected && !HasComponent<SnapPointConnected>(e))
                {
                    buffer.AddComponent<SnapPointConnected>(e);
                    if(!HasComponent<Disabled>(e))
                        buffer.AddComponent<Disabled>(e);
                }
                else if(!isConnected && HasComponent<SnapPointConnected>(e))
                    buffer.RemoveComponent<SnapPointConnected>(e);
            }).Run();
            bufferSystem.AddJobHandleForProducer(Dependency);
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(HideSnapPointSystem))]
    public abstract partial class ShowSnapPointSystem<T> : SystemBase where T: unmanaged, ISnapComponent
    {
        private EntityQuery showQuery, hideQuery;
        protected override void OnCreate()
        {
            RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<Ghost>(), ComponentType.ReadOnly<T>()));
            hideQuery = GetEntityQuery(ComponentType.Exclude<Ghost>(), ComponentType.Exclude<Disabled>(), ComponentType.ReadOnly<T>());
            showQuery = GetEntityQuery(ComponentType.Exclude<Ghost>(), ComponentType.Exclude<SnapPointConnected>(), ComponentType.ReadOnly<Disabled>(), ComponentType.ReadOnly<T>());
        }

        protected override void OnStartRunning()
        {
            EntityManager.RemoveComponent<Disabled>(showQuery.ToEntityArray(Allocator.Temp));
            World.GetExistingSystem<HideSnapPointSystem>().Enabled = false;
        }

        protected override void OnUpdate() { }

        protected override void OnStopRunning()
        {
            EntityManager.AddComponent<Disabled>(hideQuery.ToEntityArray(Allocator.Temp));
            World.GetExistingSystem<HideSnapPointSystem>().Enabled = true;
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class HideSnapPointSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            EntityManager.AddComponent<Disabled>(GetEntityQuery(ComponentType.Exclude<Disabled>(), ComponentType.Exclude<Ghost>(), typeof(IsSnapPoint)).ToEntityArray(Allocator.Temp));
        }
    }
}
