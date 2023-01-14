using Unity.Entities;
using UnityEngine;

public class ToggleAuthoring : MonoBehaviour
{
    public bool On;
    public GameObject OnVisual;
    public GameObject OffVisual;
    
    class ToggleBaker : Baker<ToggleAuthoring>
    {
        public override void Bake(ToggleAuthoring authoring)
        {
            AddComponent(new Toggle{On = authoring.On, OffVisual = GetEntity(authoring.OffVisual), OnVisual = GetEntity(authoring.OnVisual)});
        }
    }
}