using Rival;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

[GenerateAuthoringComponent]
public struct Player : IComponentData
{
    public float MovementSpeedDefault;
    public float MovementSpeedRun;
    public bool Running;
}

[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public unsafe partial class MovePlayerSystem : SystemBase
{
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
        var localPlayer = GetSingletonEntity<LocalPlayer>();
        var camera = GetSingletonEntity<CameraAnchor>();
        var cameraLtw = GetComponent<LocalToWorld>(camera);

        // --- Movement ---
        var movement = new float3(Input.GetAxis("MoveXAxis"), Input.GetAxis("MoveYAxis"), Input.GetAxis("MoveZAxis"));
        // Normalized * Speed
        movement = math.normalizesafe(movement);
        // Local -> World
        movement = math.mul(cameraLtw.Rotation, movement);

        // --- Rotation ---
        var rotation =  new float3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), Input.GetAxis("RotateZAxis") * 5) * ROTATION_SENSIVITY;

        var camAnchor = GetSingleton<CameraAnchor>();
        if (HasComponent<Docked>(localPlayer))
        {
            camAnchor.XRotation += math.degrees(rotation.x);
            camAnchor.XRotation = math.clamp(camAnchor.XRotation, -90, 75);
        }
        else
            camAnchor.XRotation = 0;
        SetSingleton(camAnchor);

        var deltaTime = Time.DeltaTime;

        Entities.WithAll<LocalPlayer>()
            .ForEach((ref Player player) =>
            {
                player.Running = Input.GetKey(KeyCode.LeftShift);
            }).Run();

        Entities
            .WithNone<Docked>()
            .ForEach((ref Rotation r, ref LocalToGalaxy ltg, ref Translation t, in Player player) =>
            {
                r.Value = math.mul(r.Value, quaternion.EulerXYZ(rotation));
                var totalMovement = (movement * (double3)(player.Running ? player.MovementSpeedRun : player.MovementSpeedDefault) * deltaTime);
                ltg = LocalToGalaxy.OffsetBy(ltg, totalMovement);
                t.Value = ltg.LocalPosition;
            }).Schedule();

        Entities
            .ForEach((ref Docked docked, ref PhysicsVelocity velocity, in Player player) =>
            {
                docked.Rotation = math.mul(docked.Rotation, quaternion.EulerXYZ(new float3(0, rotation.y, 0)));
                velocity.Linear = movement * (player.Running ? player.MovementSpeedRun : player.MovementSpeedDefault);
            }).Schedule();
    }
}

[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(TRSToLocalToWorldSystem))]
[UpdateBefore(typeof(LocalToParentSystem))]
public partial class UpdateDockedCharacterPositionRotationSystem : SystemBase
{

    protected override void OnUpdate()
    {
        Entities.ForEach((ref Docked docked) =>
        {
            var parentLtw = GetComponent<LocalToWorld>(docked.Target);
            docked.TargetRotation = parentLtw.Rotation;
            // docked.TargetPosition = parentLtw.Position;
        }).ScheduleParallel();

        Entities.ForEach((ref LocalToWorld ltw, in Docked docked) =>
        {
            ltw.Value = math.mul(float4x4.TRS(ltw.Position, docked.TargetRotation, 1), float4x4.TRS(0, docked.Rotation, 1));
        }).ScheduleParallel();

        // Entities
        //     .WithAll<AlignUpVector>()
        //     .ForEach((ref ThirdPersonCharacterComponent character, ref KinematicCharacterBody props, in Docked docked) =>
        // {
        //     // character.Gravity = math.mul(docked.TargetRotation, math.up()) * -5;
        // }).Run();

        // Entities
        //     .WithNone<Docked>()
        //     .ForEach((ref ThirdPersonCharacterComponent character, ref KinematicCharacterBody props) =>
        // {
        //     character.Gravity = 0;
        //     props.RelativeVelocity *= 0.95f;
        //     if (math.all(math.abs(props.RelativeVelocity) < 0.001f))
        //         props.RelativeVelocity = 0;
        // }).Run();
    }
}
