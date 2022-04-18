using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

[GenerateAuthoringComponent]
public struct Player : IComponentData
{

}

[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public class MovePlayerSystem : SystemBase
{
    public const float MOVEMENT_SPEED_DEFAULT = 5;
    public const float MOVEMENT_SPEED_RUNNING = 15;
    private const float ROTATION_SENSIVITY = 0.4f;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<CameraAnchor>();
        RequireForUpdate(GetEntityQuery(typeof(LocalPlayer), typeof(Parent), typeof(LocalToWorld), typeof(CanMove)));
        RequireForUpdate(GetEntityQuery(typeof(CameraAnchor), typeof(LocalToWorld)));
    }

    protected override void OnUpdate()
    {
        var player = GetSingletonEntity<LocalPlayer>();
        var ltw = GetComponent<LocalToWorld>(GetSingletonEntity<CameraAnchor>());
        var parentLtw = GetComponent<LocalToWorld>(GetComponent<Parent>(player).Value);

        // --- Movement ---
        var movement = new float3(Input.GetAxis("MoveXAxis"), Input.GetAxis("MoveYAxis"), Input.GetAxis("MoveZAxis"));
        // Normalized * Speed
        movement = math.normalizesafe(movement) * (Input.GetKey(KeyCode.LeftShift) ? MOVEMENT_SPEED_RUNNING : MOVEMENT_SPEED_DEFAULT);
        // Local -> World
        movement = math.mul(ltw.Rotation, movement);
        // World -> Parent
        movement = math.mul(math.inverse(parentLtw.Rotation), movement);

        // --- Rotation ---
        var rotation =  new float3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), Input.GetAxis("RotateZAxis"))* ROTATION_SENSIVITY;

        Entities
            .WithAll<Player>()
            .WithNone<Docked>()
            .ForEach((ref Rotation r, ref PhysicsVelocity velocity) =>
            {
                r.Value = math.mul(r.Value, quaternion.EulerXYZ(rotation));
                velocity.Linear = movement;
            }).Schedule();
        Entities
            .WithAll<Player, Docked>()
            .ForEach((ref Rotation r, ref PhysicsVelocity velocity, in LocalToParent ltp) =>
            {
                r.Value = math.mul(r.Value, quaternion.EulerXYZ(new float3(rotation.x, rotation.y, 0)));
                velocity.Linear = movement;
            }).Schedule();
    }
}
