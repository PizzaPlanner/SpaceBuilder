using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

[Flags]
public enum ExposedSides : byte
{
    Forward = 0b0000_0001,
    Up = 0b0000_0010,
    Left = 0b0000_0100,
    Back = 0b0000_1000,
    Down = 0b0001_0000,
    Right = 0b0010_0000,

    POSITIVE = Forward | Up | Left,
    NEGATIVE = Back | Down | Right,
}

[GenerateAuthoringComponent]
public struct GridItem : IComponentData { }

public struct NeighbourData : IBufferElementData
{
    public Entity Neighbour;
}

//
// public class GridStructure
// {
//     public static void Connect(BigGrid grid, BigGridItem a, BigGridItem b)
//     {
//         var compsA = a.GetComponents<IExchanger>();
//         var compsB = b.GetComponents<IExchanger>();
//         if (a.DiscoveryTrigger.size == b.DiscoveryTrigger.size && compsA.Length == compsB.Length && compsB.All(x => compsA.Any(y => y.GetType() == x.GetType())))
//         {
//             // Merge
//             var positionOffset = b.transform.position - a.transform.position;
//             var n = GameObject.Instantiate(a);
//             n.Size = a.Size + b.Size;
//             var newTriggerSize = a.DiscoveryTrigger.size;
//             newTriggerSize.Scale(new Vector3(
//                 positionOffset.x != 0 ? 2 : 1,
//                 positionOffset.y != 0 ? 2 : 1,
//                 positionOffset.z != 0 ? 2 : 1));
//             n.SubItems.Add(b);
//             n.SubItems.Add(a);
//             n.DiscoveryTrigger.size = newTriggerSize;
//             a.gameObject.SetActive(false);
//             b.gameObject.SetActive(false);
//             foreach(var exchanger in n.GetComponents<IExchanger>())
//                 exchanger.Clear();
//             foreach (var exchanger in grid.GetComponentsInChildren<IExchanger>()
//                          .Where(x => compsA.Union(compsB).Any(x.IsRegistered)))
//             {
//                 exchanger.Clear();
//             }
//
//             var newPos = (a.transform.localPosition + positionOffset * 0.5f) / grid.CellSize;
//             grid.Register(n, newPos.x, newPos.y, newPos.z);
//             a.transform.SetParent(n.transform, true);
//             b.transform.SetParent(n.transform, true);
//         }
//         else
//         {
//             // Don't merge
//             foreach(var component in compsA)
//                 component.Register(b);
//             foreach(var component in compsB)
//                 component.Register(a);
//         }
//     }
// }
//
// [RequireComponent(typeof(BoxCollider))]
// public class BigGridItem : MonoBehaviour
// {
//     public BoxCollider DiscoveryTrigger { get; private set; }
//
//     public int Size;
//     public List<BigGridItem> SubItems;
//
//     void Awake()
//     {
//         DiscoveryTrigger = gameObject.GetComponent<BoxCollider>();
//         Size = 1;
//         SubItems = new List<BigGridItem>();
//     }
//
//     public void InitializeOnGrid(BigGrid grid)
//     {
//         if(Size == 1)
//             DiscoveryTrigger.size = Vector3.one * grid.CellSize;
//         DiscoveryTrigger.center = Vector3.zero;
//         DiscoveryTrigger.isTrigger = true;
//         DiscoveryTrigger.enabled = true;
//         foreach (var vector in new Vector3[] { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
//         {
//             if (Physics.Raycast(transform.position, vector, out var hit, grid.CellSize * 1.01f,
//                     grid.GridLayer))
//                 GridStructure.Connect(grid, this, hit.collider.GetComponent<BigGridItem>());
//         }
//     }
// }
