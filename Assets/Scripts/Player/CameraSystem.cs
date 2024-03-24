using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct CameraSettings : IComponentData
{
    public bool InvertedY;
    public float3 CloseAnchor;
    public float3 FarAnchor;
    public float ScrollRatio;
    public CollisionFilter CollidesWith;
    public quaternion Orientation;
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
partial class CameraSystem : SystemBase
{
    const float MAX_PITCH_ANGLE = 80;

    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<CameraSettings>();
        RequireForUpdate<InputData>();
        RequireForUpdate<ReferenceFrame>();
    }
    
    public void SetCursorLocked(bool locked)
    {
        if (!locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    protected override void OnStartRunning()
    {
        SetCursorLocked(true);
        var localPLayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var playerLtw = SystemAPI.GetComponent<LocalToWorld>(localPLayer);
        var cam = Camera.main.transform;
        cam.rotation = playerLtw.Rotation;
        cam.position = playerLtw.Position;
    }

    protected override void OnStopRunning()
    {
        SetCursorLocked(false);
    }

    protected override void OnUpdate()
    {
        if (Camera.main == null)
        {
            Debug.LogWarning("No Camera!");
            return;
        }
        var cam = Camera.main.transform;
        var camTarget = SystemAPI.GetSingletonEntity<CameraSettings>();
        var camLtw = SystemAPI.GetComponent<LocalToWorld>(camTarget);
        var camSettings = SystemAPI.GetSingletonRW<CameraSettings>();

        var input = SystemAPI.GetSingleton<InputData>();
        if(input.ScrollMode == ScrollMode.Camera)
            camSettings.ValueRW.ScrollRatio = math.clamp(camSettings.ValueRW.ScrollRatio + input.Scroll * 0.1f, 0, 1);
            
        var lookDelta = input.Rotation * new float3(1, camSettings.ValueRW.InvertedY ? -1 : 1, 1);

        var offsetRotation = math.mul(camLtw.Rotation, camSettings.ValueRW.Orientation);
        var camMin = camLtw.Position + math.mul(offsetRotation, camSettings.ValueRW.CloseAnchor);
        var camMax = camLtw.Position + math.mul(offsetRotation, camSettings.ValueRW.FarAnchor);
        var distance = camSettings.ValueRW.ScrollRatio;
        var raycastInput = new RaycastInput
        {
            Start = camMin,
            End = camMax,
            Filter = camSettings.ValueRW.CollidesWith
        };
        if (SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld) && 
            physicsWorld.PhysicsWorld.CollisionWorld.CastRay(raycastInput, out var raycastHit))
        {
            distance *= raycastHit.Fraction - 0.01f;
        }
        
        cam.rotation = offsetRotation;
        cam.position = math.lerp(camMin, camMax, distance);
        
        // Pitch and yaw, mouse look
        var rotationX = quaternion.AxisAngle(math.right(), lookDelta.x);
        var rotationY = quaternion.AxisAngle(math.up(), lookDelta.y);
        // Roll when in space
        var rotationZ = quaternion.AxisAngle(math.forward(), lookDelta.z);

        // Combine the quaternions to get the final rotation quaternion
        // TODO: When only using mouse look, up vector is modified. up should only change when rolling. 
        //       Transforms solve it by rotating up in Space.World. How to do that with quaternions??
        var combinedRotation = math.mul(rotationZ, math.mul(rotationX, rotationY));
        var totalRotation = math.mul(camSettings.ValueRW.Orientation, combinedRotation);
        // Limit x-axis rotation
        var euler = math.Euler(totalRotation);
        euler.x = math.clamp(euler.x, -MAX_PITCH_ANGLE * math.TORADIANS, MAX_PITCH_ANGLE * math.TORADIANS);
        totalRotation = quaternion.Euler(euler);
        
        var refFrame = SystemAPI.GetSingleton<ReferenceFrame>();
        if(refFrame.DurationSinceLastChange == 0)
            totalRotation = quaternion.identity;

        camSettings.ValueRW.Orientation = totalRotation;
        
    }
}