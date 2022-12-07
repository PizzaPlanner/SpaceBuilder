using Unity.Entities;
using UnityEngine;

public class ToggleAuthoring : MonoBehaviour
{
    public bool On;
    public Entity OnVisual;
    public Entity OffVisual;
    
    class ToggleBaker : Baker<ToggleAuthoring>
    {
        public override void Bake(ToggleAuthoring authoring)
        {
            throw new System.NotImplementedException();
        }
    }
}