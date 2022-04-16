using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

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
        RequireForUpdate(GetEntityQuery(typeof(LocalPlayer), typeof(Parent), typeof(LocalToWorld)));
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
            .ForEach((ref Rotation r, ref PhysicsVelocity velocity) =>
            {
                r.Value = math.mul(r.Value, quaternion.EulerXYZ(rotation));
                velocity.Linear = movement;
            }).Schedule();
    }
}

[UpdateBefore(typeof(TransformSystemGroup))]
public class DockingSystem : SystemBase
{
    struct OriginAnchor : IComponentData { }
    public Entity Origin;
    private VisualElement swapRoot;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        Origin = EntityManager.CreateEntity(typeof(LocalToWorld), typeof(OriginAnchor));
        SetComponent(Origin, new LocalToWorld{Value = float4x4.identity});
    }

    protected override void OnStartRunning()
    {
        swapRoot = UIDocument.FindObjectOfType<UIDocument>().rootVisualElement.Q("DockInfo");
    }

    protected override void OnUpdate()
    {
        var entity = GetSingletonEntity<LocalPlayer>();
        var grids = GetBuffer<CloseGrids>(entity);

        if (grids.Length == 0)
        {
            Dock(entity, Origin);
            swapRoot.Q("Station", "dockingbutton").Q<Label>().text = "<None>";
            swapRoot.Q("Station", "dockingbutton").SetEnabled(false);
            swapRoot.Q("Swap").SetEnabled(false);
        }
        else
        {
            swapRoot.Q("Station", "dockingbutton").Q<Label>().text = GetComponent<Name>(grids[0].Grid).Value.ToString();
            swapRoot.Q("Station", "dockingbutton").SetEnabled(true);
            swapRoot.Q("Swap").SetEnabled(true);
        }

        if (!Input.GetKeyDown(KeyCode.G))
            return;

        var dockedGrid = GetComponent<Parent>(entity).Value == Origin && grids.Length > 0 ? grids[0].Grid : Origin;
        Dock(entity, dockedGrid);
    }

    void Dock(Entity docker, Entity grid)
    {
        var playerLtw = GetComponent<LocalToWorld>(docker);
        var parentLtw = GetComponent<LocalToWorld>(grid);

        var newLocalPos = playerLtw.Position;
        var newLocalRot = playerLtw.Rotation;
        // World --> Parent
        newLocalPos = math.mul(math.inverse(parentLtw.Rotation), newLocalPos);
        newLocalRot = math.mul(math.inverse(parentLtw.Rotation), newLocalRot);

        SetComponent(docker, new Parent{Value = grid});
        SetComponent(docker, new Translation{Value = newLocalPos});
        SetComponent(docker, new Rotation{Value = newLocalRot});

        swapRoot.Q(grid != Origin ? "System" : "Station", "dockingbutton").RemoveFromClassList("dockingbutton-on");
        swapRoot.Q(grid == Origin ? "System" : "Station", "dockingbutton").AddToClassList("dockingbutton-on");
    }
}
