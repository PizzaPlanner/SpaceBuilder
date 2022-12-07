using Unity.Entities;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public float MovementSpeedDefault;
    public float MovementSpeedRun;

    class PlayerBaker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            AddComponent(new Player
            {
                MovementSpeedDefault = authoring.MovementSpeedDefault, 
                MovementSpeedRun = authoring.MovementSpeedRun
            });
        }
    }
}