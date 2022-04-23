using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[GenerateAuthoringComponent]
public struct OrbitalObject : IComponentData
{
    public float RotationSpeed;
    public float3 OrbitUp;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(Unity.Physics.Systems.BuildPhysicsWorld))]
public partial class OrbitSimulationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var time = (float)Time.ElapsedTime;
        // Entities.ForEach((ref Rotation rot, ref OrbitalObject obj) =>
        // {
        //     rot.Value = quaternion.AxisAngle(math.normalize(obj.OrbitUp), time * obj.RotationSpeed);
        // }).Schedule();

        Entities.ForEach((ref Rotation r, in Sun sun) =>
        {
            r.Value = quaternion.AxisAngle(math.up(), 4.71239f);
        }).Schedule();
    }
}
