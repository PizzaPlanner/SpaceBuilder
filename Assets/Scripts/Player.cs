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

    private EntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<CameraAnchor>();
        RequireSingletonForUpdate<InputSystem.ControlPlayer>();
        RequireSingletonForUpdate<ControlScheme.OnFeet>();
        RequireForUpdate(GetEntityQuery(typeof(LocalPlayer), typeof(LocalToWorld)));
        RequireForUpdate(GetEntityQuery(typeof(CameraAnchor), typeof(LocalToWorld)));

        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
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

        var player = GetComponent<Player>(localPlayer);
        player.Running = Input.GetKey(KeyCode.LeftShift);
        SetComponent(localPlayer, player);

        var deltaTime = Time.DeltaTime;

        var ecb = bufferSystem.CreateCommandBuffer();
        Entities
            .WithNone<Docked>()
            .ForEach((Entity e, ref Rotation r, ref CanTravelSectors cts, ref LocalToGalaxy ltg, ref Player player) =>
            {
                r.Value = math.mul(r.Value, quaternion.EulerXYZ(rotation));
                var totalMovement = (movement * (double3)(player.Running ? player.MovementSpeedRun : player.MovementSpeedDefault) * deltaTime);
                ltg = LocalToGalaxy.OffsetBy(ltg, totalMovement);

                cts.CurrentTravelType = player.Running && player.MovementSpeedRun > 1e9
                    ? SectorChangeType.FTL
                    : SectorChangeType.Normal;
                if (cts.PreviousSector != ltg)
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

                cts.CurrentSpeed = math.length(LocalToGalaxy.RelativePosition(ltg, cts.PreviousSector)) / deltaTime;
            }).Schedule();

        Entities
            .ForEach((ref Docked docked, in Player player) =>
            {
                var movementInversedByParent = math.mul(math.inverse(docked.TargetRotation), movement); 
                docked.Rotation = math.mul(docked.Rotation, quaternion.EulerXYZ(new float3(0, rotation.y, 0)));
                docked.Position += movementInversedByParent * (player.Running ? player.MovementSpeedRun : player.MovementSpeedDefault) * deltaTime;
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
            docked.TargetPosition = parentLtw.Position;
        }).ScheduleParallel();

        Entities.ForEach((ref LocalToWorld ltw, in Docked docked, in LocalToGalaxy ltg) =>
        {
            ltw.Value = math.mul(float4x4.TRS(docked.TargetPosition, docked.TargetRotation, 1), float4x4.TRS(docked.Position, docked.Rotation, 1));
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
