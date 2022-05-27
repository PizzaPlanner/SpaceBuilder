using System;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


public partial class DebugOccupationSystem : SystemBase
{
    private Mesh mesh;
    private Material occupationMat, ghostMat;

    protected override void OnCreate()
    {
        Vector3[] vertices = {
            new Vector3 (0, 0, 0),
            new Vector3 (0.25f, 0, 0),
            new Vector3 (0.25f, 0.25f, 0),
            new Vector3 (0, 0.25f, 0),
            new Vector3 (0, 0.25f, 0.25f),
            new Vector3 (0.25f, 0.25f, 0.25f),
            new Vector3 (0.25f, 0, 0.25f),
            new Vector3 (0, 0, 0.25f),
        };

        int[] triangles = {
            0, 2, 1, //face front
            0, 3, 2,
            2, 3, 4, //face top
            2, 4, 5,
            1, 2, 5, //face right
            1, 5, 6,
            0, 7, 4, //face left
            0, 4, 3,
            5, 4, 7, //face back
            5, 7, 6,
            0, 6, 7, //face bottom
            0, 1, 6
        };

        mesh = new Mesh();
        mesh.Clear ();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.Optimize ();
        mesh.RecalculateNormals ();

        occupationMat = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings").DebugOccupationMaterial;
        ghostMat = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings").DebugGhostMaterial;
    }

    private bool showDebug;
    protected override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
            showDebug = !showDebug;

        if(!showDebug)
            return;

        NativeList<float4x4> matrices = new NativeList<float4x4>(Allocator.Temp);
        Entities.ForEach((DynamicBuffer<OccupationBuffer> buffer, Grid grid, in LocalToWorld ltw) =>
        {
            foreach(var item in buffer)
                matrices.Add(float4x4.TRS(Grid.PositionFromCell(ltw, item.Location), ltw.Rotation, 1));
        }).Run();

        Draw(matrices, occupationMat);

        matrices.Clear();
        Entities.ForEach((in Ghost ghost, in GridItem item) =>
        {
            var parentLtw = GetComponent<LocalToWorld>(item.Grid);
            for(int x = item.Area.Min.x; x <= item.Area.Max.x; x++)
            for(int y = item.Area.Min.y; y <= item.Area.Max.y; y++)
            for(int z = item.Area.Min.z; z <= item.Area.Max.z; z++)
                matrices.Add(float4x4.TRS(Grid.PositionFromCell(parentLtw, new int3(x,y,z)), parentLtw.Rotation, 1));
        }).Run();

        Draw(matrices, ghostMat);

        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
        if (target.Value != Entity.Null && HasComponent<GridItem>(target.Value))
        {
            matrices.Clear();

            var grid = GetComponent<GridItem>(target.Value).Grid;
            var ltw = GetComponent<LocalToWorld>(grid);
            foreach(var cell in GetBuffer<CellPathBuffer>(GetSingletonEntity<CellPathBuffer>()))
                matrices.Add(float4x4.TRS(Grid.PositionFromCell(ltw, cell.Value), ltw.Rotation, 1));

            Draw(matrices, ghostMat);
        }
    }

    private void Draw(NativeList<float4x4> matrices, Material mat)
    {
        if (matrices.Length == 0)
            return;

        var mats = matrices.ToArray().Select(x => new Matrix4x4(x.c0, x.c1, x.c2, x.c3)).ToArray();
        while (mats.Length > 1000)
        {
            Graphics.DrawMeshInstanced(mesh, 0, mat, mats, 1000);
            mats = mats.Skip(1000).ToArray();
        }
        Graphics.DrawMeshInstanced(mesh, 0, mat, mats, mats.Length);
    }
}
