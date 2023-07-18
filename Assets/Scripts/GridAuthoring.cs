using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(LocalToGalaxyAuthoring))]
public class GridAuthoring : MonoBehaviour
{
    public string FilePath;

    class Baker : Baker<GridAuthoring>
    {
        public override void Bake(GridAuthoring authoring)
        {
            AddComponent(new ExecuteAdminScript {Script = $"spawn \"{authoring.FilePath}\"", Executor = GetEntity(authoring)});
        }
    }
}
