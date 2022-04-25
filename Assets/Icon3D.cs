using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;

public class Icon3D : Image
{
    public Icon3D()
    {
        image = new RenderTexture(128, 128, 24, GraphicsFormat.R8G8B8A8_SRGB);
    }

    public void SetObject(Entity prefab, World fromWorld)
    {
        GameObject.FindObjectOfType<IconDisplayManager>().Display(prefab, fromWorld, float3.zero, image as RenderTexture);
    }

    public new class UxmlFactory : UxmlFactory<Icon3D, UxmlTraits> { }
}
