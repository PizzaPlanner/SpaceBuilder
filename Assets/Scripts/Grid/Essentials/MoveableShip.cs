using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct MoveableShip : IComponentData
{
    public const float DAMP_FACTOR_LINEAR = 9000;
    public const float DAMP_FACTOR_ANGULAR = 2000;
    
    public double3 LinearAcceleration;
    public float3 AngularAcceleration;
    public float Mass;

    public double3 LinearVelocity;
    public float3 AngularVelocity;
}

public partial struct MoveShipSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        foreach (var (ship, transform, ltg) in SystemAPI.Query<RefRW<MoveableShip>, RefRW<LocalTransform>, RefRW<LocalToGalaxy>>())
        {
            if (ship.ValueRW.Mass == 0 || double.IsNaN(ship.ValueRW.Mass))
                continue;

            var lAcc = ship.ValueRW.LinearAcceleration * deltaTime / ship.ValueRW.Mass;
            lAcc = math.rotate(transform.ValueRW.ToMatrix(), lAcc);
            var aAcc = ship.ValueRW.AngularAcceleration * deltaTime / ship.ValueRW.Mass;
            var ldampen = math.clamp(deltaTime * MoveableShip.DAMP_FACTOR_LINEAR / ship.ValueRW.Mass, 0, 1);
            var adampen = math.clamp(deltaTime * MoveableShip.DAMP_FACTOR_ANGULAR / ship.ValueRW.Mass, 0, 1);
            
            var lvelocity = ship.ValueRW.LinearVelocity * (1 - ldampen) + lAcc;
            var avelocity = ship.ValueRW.AngularAcceleration * (1 - adampen) + aAcc;

            if(math.any(ship.ValueRW.LinearVelocity != 0))
                ltg.ValueRW += ship.ValueRW.LinearVelocity;

            if (math.any(ship.ValueRW.AngularVelocity != 0))
            {
                var rotation = quaternion.EulerXYZ(ship.ValueRW.AngularVelocity);
                if(math.any(math.isnan(rotation.value)))
                    rotation = quaternion.identity;
                transform.ValueRW.Rotation = math.mul(transform.ValueRW.Rotation, rotation);
            }
            
            ship.ValueRW.LinearVelocity = lvelocity;
            ship.ValueRW.AngularVelocity = avelocity;
            ship.ValueRW.AngularAcceleration = 0;
            ship.ValueRW.LinearAcceleration = 0;
        }
    }
}