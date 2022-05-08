using Rival;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;

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

    FixedStepSimulationSystemGroup fixedStepGroup;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<CameraAnchor>();
        RequireSingletonForUpdate<InputSystem.ControlPlayer>();
        RequireForUpdate(GetEntityQuery(typeof(LocalPlayer), typeof(LocalToWorld)));
        RequireForUpdate(GetEntityQuery(typeof(CameraAnchor), typeof(LocalToWorld)));
        fixedStepGroup = World.GetExistingSystem<FixedStepSimulationSystemGroup>();
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
        var rotation =  new float3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), Input.GetAxis("RotateZAxis")) * ROTATION_SENSIVITY;

        var camAnchor = GetSingleton<CameraAnchor>();
        if (HasComponent<Docked>(player))
        {
            camAnchor.XRotation += math.degrees(rotation.x);
            camAnchor.XRotation = math.clamp(camAnchor.XRotation, -90, 55);
        }
        else
            camAnchor.XRotation = 0;
        SetSingleton(camAnchor);

        var deltaTime = Time.DeltaTime;
        var fixedDeltaTime = fixedStepGroup.RateManager.Timestep;

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
            .ForEach((ref Rotation r, ref PhysicsVelocity velocity, in KinematicCharacterBody body, in Docked docked) =>
            {
                var targetLtw = GetComponent<LocalToWorld>(docked.Target);
                r.Value = math.mul(r.Value, quaternion.EulerXYZ(new float3(0, rotation.y, 0)));
                if (math.lengthsq(movement) > 0f)
                {
                    CharacterControlUtilities.SlerpRotationTowardsDirectionAroundUp(
                        ref r.Value, deltaTime,
                        math.normalizesafe(movement),
                        targetLtw.Up,
                        0);
                }

                // Add rotation from parent body to the character rotation
                // (this is for allowing a rotating moving platform to rotate your character as well, and handle interpolation properly)
                KinematicCharacterUtilities.ApplyParentRotationToTargetRotation(ref r.Value, in body, fixedDeltaTime, deltaTime);

                velocity.Linear = movement;
            }).Schedule();
    }
}
