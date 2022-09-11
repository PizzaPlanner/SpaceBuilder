using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class IconReference : IComponentData
{
    public Texture2D Icon;
}

public class ItemAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [Range(1, 9999)]
    public ushort MaxStackSize;
    [Range(1, 9999)]
    public ushort Stack;

    public Texture2D Icon;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new Item{ });
        if (MaxStackSize > 0)
            dstManager.AddComponentData(entity, new Stackable {MaxSize = (ushort)MaxStackSize, Size = Stack});

        if (Icon == null)
            return;

        dstManager.AddComponentData(entity, new IconReference {Icon = Icon});
        dstManager.AddComponentData(entity, new Parent());
    }
}
