using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.VFX;

[GenerateAuthoringComponent]
public struct Nebula : IComponentData { }


// [UpdateInGroup(typeof(UpdatePresentationSystemGroup))]
// public partial class NebulaRelativeToPlayerSystem : SystemBase
// {
//     protected override void OnCreate()
//     {
//         RequireSingletonForUpdate<CameraAnchor>();
//     }
//
//     protected override void OnUpdate()
//     {
//         var farClipPlane = Camera.main.farClipPlane;
//         var camPosition = (float3)Camera.main.transform.position;
//         Entities
//             .WithAll<Nebula>()
//             .ForEach((VisualEffect sfx, ref LocalToWorld ltw, in SectorData data) =>
//             {
//                 var tBefore = ltw.Position - camPosition;
//                 var newScale =
//                     math.max(farClipPlane / math.length(tBefore), 3);
//                 var tNew = math.normalize(tBefore) * (farClipPlane * 0.95f - newScale) + camPosition;
//                 var newLtw = new LocalToWorld{Value = float4x4.TRS(tNew, ltw.Rotation, newScale)};
//                 sfx.startSeed = data.Seed;
//                 // sfx.SetFloat("Distance", 1 / newScale);
//                 sfx.gameObject.transform.SetPositionAndRotation(newLtw.Position, newLtw.Rotation);
//             }).WithoutBurst().Run();
//     }
// }
