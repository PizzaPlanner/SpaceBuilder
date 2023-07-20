using Unity.Entities;
using UnityEngine;

public class AdminScriptAuthoring : MonoBehaviour
{
    [Multiline]
    public string Script;

    class Baker : Baker<AdminScriptAuthoring>
    {
        public override void Bake(AdminScriptAuthoring authoring)
        {
            AddComponent(new ExecuteAdminScript
            {
                Script = authoring.Script
            });
        }
    }
}