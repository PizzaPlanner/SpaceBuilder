using Unity.Entities;
using UnityEngine;

public class ToGenerateAuthoring : MonoBehaviour
{
    class Baker : Baker<ToGenerateAuthoring>
    {
        public override void Bake(ToGenerateAuthoring authoring)
        {
            AddComponent<ToGenerate>();
        }
    }
}