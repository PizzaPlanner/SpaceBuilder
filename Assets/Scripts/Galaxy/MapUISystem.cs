using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public partial class MapUISystem : SystemBase
{
    VisualElement uiRoot;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnStartRunning()
    {
        if(uiRoot == null)
            uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("MapInfo");
    }

    protected override void OnUpdate()
    {
        Entities
            .WithAll<LocalPlayer>()
            .ForEach((in LocalToGalaxy ltg, in PhysicsVelocity v) =>
            {
                uiRoot.Q<Label>("Sector").text =
                    $"Sector: ({ltg.Sector.x:X}:{ltg.SubSector.x:X}, {ltg.Sector.y:X}:{ltg.SubSector.y:X}, {ltg.Sector.z:X}:{ltg.SubSector.z:X})";
                uiRoot.Q<Label>("Coordinates").text =
                    $"Position: ({ltg.LocalPosition.x:F0}, {ltg.LocalPosition.y:F0}, {ltg.LocalPosition.z:F0})";
                uiRoot.Q<Label>("Forward").text =
                    $"Speed: {math.length(v.Linear):F1}m/s";
                uiRoot.Q<Label>("Forward").text =
                    $"From Origin: ({LocalToGalaxy.RelativePosition(new LocalToGalaxy(), ltg):F0})";
            }).WithoutBurst().Run();
    }
}
