using Rival;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

[GenerateAuthoringComponent]
public struct Player : IComponentData
{

}

[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial class MovePlayerSystem : SystemBase
{
    public const float MOVEMENT_SPEED_DEFAULT = 5;
    public const float MOVEMENT_SPEED_RUNNING = 15;
    private const float ROTATION_SENSIVITY = 0.2f;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<CameraAnchor>();
        RequireSingletonForUpdate<InputSystem.ControlPlayer>();
        RequireSingletonForUpdate<ControlScheme.OnFeet>();
        RequireForUpdate(GetEntityQuery(typeof(LocalPlayer), typeof(LocalToWorld)));
        RequireForUpdate(GetEntityQuery(typeof(CameraAnchor), typeof(LocalToWorld)));
    }

    protected override void OnUpdate()
    {
        var player = GetSingletonEntity<LocalPlayer>();
        var camera = GetSingletonEntity<CameraAnchor>();
        var cameraLtw = GetComponent<LocalToWorld>(camera);

        // --- Movement ---
        var movement = new float3(Input.GetAxis("MoveXAxis"), Input.GetAxis("MoveYAxis"), Input.GetAxis("MoveZAxis"));
        // Normalized * Speed
        movement = math.normalizesafe(movement) * (Input.GetKey(KeyCode.LeftShift) ? MOVEMENT_SPEED_RUNNING : MOVEMENT_SPEED_DEFAULT);
        // Local -> World
        movement = math.mul(cameraLtw.Rotation, movement);

        // --- Rotation ---
        var rotation =  new float3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), Input.GetAxis("RotateZAxis") * 5) * ROTATION_SENSIVITY;

        var camAnchor = GetSingleton<CameraAnchor>();
        if (HasComponent<Docked>(player))
        {
            camAnchor.XRotation += math.degrees(rotation.x);
            camAnchor.XRotation = math.clamp(camAnchor.XRotation, -90, 75);
        }
        else
            camAnchor.XRotation = 0;
        SetSingleton(camAnchor);

        var deltaTime = Time.DeltaTime;

        Entities
            .WithAll<Player>()
            .WithNone<Docked>()
            .ForEach((ref Rotation r, ref PhysicsVelocity velocity) =>
            {
                r.Value = math.mul(r.Value, quaternion.EulerXYZ(rotation));
                velocity.Linear = movement;
            }).Schedule();

        Entities
            .WithAll<Player>()
            .ForEach((ref Docked docked, ref PhysicsVelocity velocity, in KinematicCharacterBody body) =>
            {
                docked.Rotation = math.mul(docked.Rotation, quaternion.EulerXYZ(new float3(0, rotation.y, 0)));
                velocity.Linear = movement;
            }).Schedule();
    }
}

[UpdateAfter(typeof(MovePlayerSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial class UpdateDockedCharacterPositionRotationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref Docked docked) =>
        {
            docked.TargetRotation = GetComponent<Rotation>(docked.Target).Value;
        }).Run();

        Entities.ForEach((ref Rotation r, in Translation t, in Docked docked) =>
        {
            r.Value = math.mul(docked.TargetRotation, docked.Rotation);
        }).Run();

        Entities.ForEach((ref ThirdPersonCharacterComponent character, in Docked docked) =>
        {
            character.Gravity = math.mul(docked.TargetRotation, math.up()) * -5;
        }).Run();

        Entities
            .WithNone<Docked>()
            .ForEach((ref ThirdPersonCharacterComponent character, ref StoredKinematicCharacterBodyProperties props) =>
        {
            character.Gravity = 0;
            props.RelativeVelocity = float3.zero;
        }).Run();
    }
}
