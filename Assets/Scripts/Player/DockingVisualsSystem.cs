using Galaxy.SectorLoading;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Hash128 = Unity.Entities.Hash128;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class DockingVisualsSystem : SystemBase
{
    private VisualElement swapRoot;
    Entity _lastRefFrame;
    Hash128 _lastCluster;
    LocalToGalaxy _lastLtg;
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<ReferenceFrameOrigin>();
    }

    protected override void OnStartRunning()
    {
        swapRoot = UIDocument.FindObjectOfType<UIDocument>().rootVisualElement.Q("DockInfo");
    }

    protected override void OnUpdate()
    {
        var refFrame = SystemAPI.GetSingletonEntity<ReferenceFrameOrigin>();
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var cts = SystemAPI.GetComponent<CanTravelSectors>(localPlayer);
        if(cts.SectorChanged < SectorUpdateType.Star && refFrame == _lastRefFrame)
            return;
        
        var ltg = SystemAPI.GetComponent<LocalToGalaxy>(refFrame);
        var cluster = GalaxyTools.GetLightSectorCluster(ltg);

        _lastLtg = ltg;
        _lastCluster = cluster;
        _lastRefFrame = refFrame;
        var isDocked = refFrame != localPlayer;
        var name = "<None>";
        var dockBtn = swapRoot.Q("Station", "dockingbutton");
        if (isDocked)
        {
            if(SystemAPI.HasComponent<Name>(refFrame))
                name = SystemAPI.GetComponent<Name>(refFrame).Value.Value;
            dockBtn.AddToClassList("undocked");
            dockBtn.RemoveFromClassList("docked");   
        }
        else
        {
            name = GalaxyTools.GetClusterName(ltg).Value;
            dockBtn.AddToClassList("docked");
            dockBtn.RemoveFromClassList("undocked");
        }
        dockBtn.Q<Label>().text = name;
    }
}