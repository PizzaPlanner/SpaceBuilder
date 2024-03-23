using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct ExecuteAdminScript : IComponentData
{
    [Multiline]
    public FixedString64Bytes Script;
    public Entity Executor;
}