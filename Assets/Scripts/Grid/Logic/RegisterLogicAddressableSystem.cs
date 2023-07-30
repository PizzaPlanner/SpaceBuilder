using Unity.Entities;
using Unity.Transforms;

namespace Grid.Logic
{
    public struct RegisterAsLogicAddressable : IComponentData { }
    
    [UpdateInGroup(typeof(GridRecalculationSystemGroup))]
    partial struct RegisterLogicAddressableSystem : ISystem
    {
        EntityQuery _logicQuery;
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RegisterAsLogicAddressable>();
            _logicQuery = SystemAPI.QueryBuilder().WithAll<RegisterAsLogicAddressable>().Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var rooms = SystemAPI.GetSingletonRW<RoomsLookup>();
            foreach (var (gridItem, parent, e) in
                     SystemAPI.Query<GridItem, Parent>()
                              .WithAll<RegisterAsLogicAddressable>()
                              .WithNone<Ghost>().WithEntityAccess())
            {
                rooms.ValueRW.LogicLookup.Add((parent.Value, gridItem.Area.Origin), e);
            }

            state.EntityManager.RemoveComponent<RegisterAsLogicAddressable>(_logicQuery);
        }
    }
}