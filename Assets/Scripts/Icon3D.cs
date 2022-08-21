using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;

public class Icon3D : Image
{
    private IconDisplayManager displayManager;
    public Icon3D()
    {
        image = new RenderTexture(128, 128, 24, GraphicsFormat.R8G8B8A8_SRGB);
        displayManager = GameObject.FindObjectOfType<IconDisplayManager>();
    }

    public void SetObject(Entity prefab, World fromWorld)
    {
        displayManager.Display(prefab, fromWorld, float3.zero, image as RenderTexture);
    }

    public new class UxmlFactory : UxmlFactory<Icon3D, UxmlTraits> { }
}

// [UpdateInGroup(typeof(PresentationSystemGroup))]
// public partial class MinimapRenderSystem : SystemBase
// {
//     private Icon3D MapIcon;
//     private Entity prefab;
//     protected override void OnCreate()
//     {
//         RequireSingletonForUpdate<IdentifiableMappingSystem.Loaded>();
//         RequireSingletonForUpdate<LocalPlayer>();
//     }
//
//     protected override void OnStartRunning()
//     {
//         base.OnCreate();
//         MapIcon = UIDocument.FindObjectOfType<UIDocument>().rootVisualElement.Q("Coordinates").Q<Icon3D>();
//     }
//
//     protected override void OnUpdate()
//     {
//         if (prefab == Entity.Null)
//         {
//             prefab = GetEntityQuery(typeof(Prefab), typeof(Star)).GetSingletonEntity();
//         }
//
//         MapIcon.SetObject(prefab, World);
//     }
// }
