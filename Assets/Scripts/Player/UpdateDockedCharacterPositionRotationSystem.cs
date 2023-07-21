using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(TransformSystemGroup))]
public partial class UpdateDockedCharacterPositionRotationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref Docked docked) =>
        {
            var parentLtw = SystemAPI.GetComponent<LocalToWorld>(docked.Target);
            docked.TargetRotation = parentLtw.Rotation;
            docked.TargetPosition = parentLtw.Position;
        }).ScheduleParallel();

        Entities.ForEach((ref LocalToWorld ltw, in Docked docked, in LocalToGalaxy ltg) =>
        {
            var trs = math.mul(float4x4.TRS(docked.TargetPosition, docked.TargetRotation, 1),
                float4x4.TRS(docked.Position, docked.Rotation, 1));
            ltw = new LocalToWorld { Value = trs };
        }).ScheduleParallel();
    }
}