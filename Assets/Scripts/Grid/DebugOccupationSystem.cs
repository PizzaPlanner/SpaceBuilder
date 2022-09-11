using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Material = UnityEngine.Material;


public partial class DebugOccupationSystem : SystemBase
{
    private Mesh mesh;
    private Material occupationMat, ghostMat;

    private bool showAABBs, showOccupations, showRooms;

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

    protected override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
            showOccupations = !showOccupations;

        if (showOccupations)
            ShowOccupations();
        
        if (Input.GetKeyDown(KeyCode.Alpha9))
            showAABBs = !showAABBs;
        
        if (showAABBs)
            ShowAABBs();
        
        if (Input.GetKeyDown(KeyCode.Alpha8))
            showRooms = !showRooms;
        
        if (showRooms)
            ShowRooms();
        
        ShowTargetBoundingBoxes();
    }

    private void ShowOccupations()
    {
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
            if (item.Grid == Entity.Null)
                return;
        
            var parentLtw = GetComponent<LocalToWorld>(item.Grid);
            for(int x = item.Area.Min.x; x <= item.Area.Max.x; x++)
            for(int y = item.Area.Min.y; y <= item.Area.Max.y; y++)
            for(int z = item.Area.Min.z; z <= item.Area.Max.z; z++)
                matrices.Add(float4x4.TRS(Grid.PositionFromCell(parentLtw, new int3(x,y,z)), parentLtw.Rotation, 1));
        }).Run();

        Draw(matrices, ghostMat);
    }
    
    private void ShowRooms()
    {
        NativeList<float4x4> matricesUnexposed = new NativeList<float4x4>(Allocator.Temp);
        NativeList<float4x4> matricesExposed = new NativeList<float4x4>(Allocator.Temp);
        Entities.ForEach((DynamicBuffer<Rooms> rooms, Grid grid, in LocalToWorld ltw) =>
        {
            foreach(var item in rooms)
                if(item.IsExposedToSpace)
                    matricesExposed.Add(float4x4.TRS(Grid.PositionFromCell(ltw, item.Cell), ltw.Rotation, 1));
                else
                    matricesUnexposed.Add(float4x4.TRS(Grid.PositionFromCell(ltw, item.Cell), ltw.Rotation, 1));
        }).Run();
        
        Draw(matricesExposed, occupationMat);
        Draw(matricesUnexposed, ghostMat);
    }

    private void ShowTargetBoundingBoxes()
    {
        Entities.ForEach((in Ghost ghost, in GhostPlacementBuilder builder, in GridItem item) =>
        {
            if (builder.TargetGrid == Entity.Null)
                return;
            
            var gridLtw = GetComponent<LocalToWorld>(builder.TargetGrid);
            var shownItem = builder.Original.Scalable ? builder.DragArea : item.Area;
            BoundingBox.Draw(shownItem, gridLtw, 0, ghost.Buildable ? Color.green : Color.red, Color.blue);
        }).WithoutBurst().Run();
        
        Entities
            .WithAll<LocalPlayer>()
            .ForEach((in Target target) =>
            {
                if (target.GridHit.Grid == Entity.Null) return;
                
                var gridItem = GetComponent<GridItem>(target.Value);
                var gridLtw = GetComponent<LocalToWorld>(target.GridHit.Grid);
                BoundingBox.Draw(gridItem.Area, gridLtw, target.GridHit.Normal, Color.white, Color.blue);
                BoundingBox.Draw(new BoundingBox(){Origin = target.GridHit.Cell}, gridLtw, target.GridHit.Normal, Color.white, Color.blue);
            }).WithoutBurst().Run();
    }
    
    private void ShowAABBs()
    {
        NativeList<float4x4> matrices = new NativeList<float4x4>(Allocator.Temp);
        Grid inGrid = default;
        float4x4 inGridItem = default;
        var playerPos = GetComponent<LocalToWorld>(GetSingletonEntity<LocalPlayer>()).Position;
        
        Entities.ForEach((in Grid grid) =>
        {
            if (grid.AABB.Contains(playerPos))
                inGrid = grid;
            else
                matrices.Add(float4x4.TRS(grid.AABB.Center - grid.AABB.Extents, quaternion.identity, grid.AABB.Size / Grid.CELL_SIZE));
        }).Run();
        Draw(matrices, ghostMat);
        
        matrices.Clear();
        Entities.ForEach((in GridItem gridItem) =>
        {
            var parentLtw = GetComponent<LocalToWorld>(gridItem.Grid);
            var bbox = gridItem.Area;
            var mtx = float4x4.TRS(Grid.PositionFromCell(parentLtw, bbox.Min), parentLtw.Rotation,
                bbox.Size);
            if (math.any(inGrid.BoundsMax != 0) && (bbox.Contains(Grid.CellFromPosition(parentLtw, playerPos)) || false))
                inGridItem = mtx;
            else
                matrices.Add(mtx);
                
        }).Run();
        Draw(matrices, ghostMat);
        
        matrices.Clear();
        if (!inGridItem.Equals(default))
        {
            matrices.Add(inGridItem);
            Draw(matrices, occupationMat);
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
