using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

[GenerateAuthoringComponent]
public struct Toggle : IComponentData
{
    public bool On;
    public Entity Target;
}

public struct RequestToggleChange : IComponentData { }

public partial class ToggleSystem : SystemBase
{
    private GhostPlacementSettings settings;

    protected override void OnCreate()
    {
        settings = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings");
    }

    protected override void OnUpdate()
    {
        Entities
            .WithAll<RequestToggleChange>()
            .ForEach((ref Toggle toggle) =>
            {
                toggle.On = !toggle.On;
                var port = GetComponent<ElectricityPort>(toggle.Target);
                port.On = toggle.On;
                SetComponent(toggle.Target, port);
            }).Run();
        
        Entities
            .WithAll<RequestToggleChange>()
            .ForEach((RenderMesh mesh, in Toggle toggle) =>
            {
                mesh.material = toggle.On ? settings.GhostPlacementMaterial : settings.GhostRemovalMaterial;
            }).WithStructuralChanges().WithoutBurst().Run();

        EntityManager.RemoveComponent<RequestToggleChange>(GetEntityQuery(typeof(RequestToggleChange)));
    }
}
