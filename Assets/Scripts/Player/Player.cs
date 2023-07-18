using Unity.Entities;


public struct Player : IComponentData
{
    public float MovementSpeedDefault;
    public float MovementSpeedRun;
    public bool Running;
}