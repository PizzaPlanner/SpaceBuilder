using System.Linq;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
using Material = UnityEngine.Material;


public partial class DebugOccupationSystem : SystemBase
{
    private Mesh mesh;
    private Material occupationMat, ghostMat;

    public struct Settings : IComponentData
    {
        public bool showAABBs, showOccupations, showRooms, showTarget;
    }
    
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
        
        RequireForUpdate<Settings>();
        EntityManager.CreateEntity(typeof(Settings));    
    }
    
    protected override void OnStartRunning()
    {
        // var ui = Object.FindObjectOfType<UIDocument>().rootVisualElement.Q("InfoTexts").Q("Grids");
        // ui.Q<Toggle>("target").RegisterValueChangedCallback(x =>
        // {
        //     var settings = SystemAPI.GetSingletonRW<Settings>();
        //     settings.ValueRW.showTarget = x.newValue;
        // });
        //
        // ui.Q<Toggle>("aabbs").RegisterValueChangedCallback(x =>
        // {
        //     var settings = SystemAPI.GetSingletonRW<Settings>();
        //     settings.ValueRW.showAABBs = x.newValue;
        // });
        //
        // ui.Q<Toggle>("rooms").RegisterValueChangedCallback(x =>
        // {
        //     var settings = SystemAPI.GetSingletonRW<Settings>();
        //     settings.ValueRW.showRooms = x.newValue;
        // });
        //
        // ui.Q<Toggle>("occupations").RegisterValueChangedCallback(x =>
        // {
        //     var settings = SystemAPI.GetSingletonRW<Settings>();
        //     settings.ValueRW.showOccupations = x.newValue;
        // });
    }

    protected override void OnUpdate()
    {
        var settings = SystemAPI.GetSingleton<Settings>();

        if (settings.showOccupations)
            ShowOccupations();

        if (settings.showAABBs)
            ShowAABBs();

        if (settings.showRooms)
            ShowRooms();
        
        if(settings.showTarget)
            ShowTargetBoundingBoxes();
    }

    void ShowOccupations()
    {
        // var roomsLookup = SystemAPI.GetSingleton<RoomsLookup>();
        // NativeList<float4x4> matrices = new NativeList<float4x4>(Allocator.Temp);
        // foreach(var (ltw, e) in SystemAPI.Query<LocalToWorld>().WithAll<GridStructure>().WithEntityAccess())
        // {
        //     foreach(var wall in roomsLookup.Walls.GetValuesForKey(e))
        //         matrices.Add(float4x4.TRS(GridStructure.PositionFromCell(ltw, wall), ltw.Rotation, 1));
        // }
        //
        // Draw(matrices, occupationMat);
        
        // matrices.Clear();
        // Entities.ForEach((in CellMap cells, in GridStructure grid, in LocalToWorld ltw) =>
        // {
        //     foreach(var item in cells.Walls)
        //         matrices.Add(float4x4.TRS(GridStructure.PositionFromCell(ltw, item.Min), ltw.Rotation, item.Max - item.Min));
        // }).Run();
        //
        // Draw(matrices, ghostMat);
        //
        // matrices.Clear();
        // Entities.ForEach((in Ghost ghost, in GridItem item) =>
        // {
        //     if (item.Grid == Entity.Null)
        //         return;
        //
        //     var parentLtw = GetComponent<LocalToWorld>(item.Grid);
        //     for(int x = item.Area.Min.x; x <= item.Area.Max.x; x++)
        //     for(int y = item.Area.Min.y; y <= item.Area.Max.y; y++)
        //     for(int z = item.Area.Min.z; z <= item.Area.Max.z; z++)
        //         matrices.Add(float4x4.TRS(GridStructure.PositionFromCell(parentLtw, item.Area.Max), parentLtw.Rotation, item.Area.Max - item.Area.Min));
        // }).Run();
        //
        // Draw(matrices, ghostMat);
    }
    
    void ShowRooms()
    {
        var roomsLookup = SystemAPI.GetSingleton<RoomsLookup>();
        NativeList<float4x4> matricesUnexposed = new NativeList<float4x4>(Allocator.Temp);
        NativeList<float4x4> matricesExposed = new NativeList<float4x4>(Allocator.Temp);
        foreach(var (ltw, e) in SystemAPI.Query<LocalToWorld>().WithAll<GridStructure>().WithEntityAccess())
        {
            foreach(var (_, room) in roomsLookup.GridRooms.GetValuesForKey(e))
                matricesUnexposed.Add(float4x4.TRS(
                    GridStructure.CellToWorld(ltw, room.BoundingBox.Min), 
                    ltw.Rotation, 
                    room.BoundingBox.Max - room.BoundingBox.Min));
        }
        
        Draw(matricesExposed, occupationMat);
        Draw(matricesUnexposed, ghostMat);
    }

    void ShowTargetBoundingBoxes()
    {
        var target = SystemAPI.GetComponent<Target>(SystemAPI.GetSingletonEntity<LocalPlayer>());
        if (target.GridHit.Grid == Entity.Null) return;
                
        var gridItem = SystemAPI.GetComponent<GridItem>(target.Value);
        var gridLtw = SystemAPI.GetComponent<LocalToWorld>(target.GridHit.Grid);
        BoundingBox.Draw(gridItem.Area, gridLtw, target.GridHit.Normal, Color.white, Color.blue);
        BoundingBox.Draw(new BoundingBox(){Origin = target.GridHit.Cell}, gridLtw, target.GridHit.Normal, Color.white, Color.blue);
    }
    
    void ShowAABBs()
    {
        NativeList<float4x4> matrices = new NativeList<float4x4>(Allocator.Temp);
        GridStructure inGrid = default;
        float4x4 inGridItem = default;
        var playerPos = SystemAPI.GetComponent<LocalToWorld>(SystemAPI.GetSingletonEntity<LocalPlayer>()).Position;
        
        foreach(var grid in SystemAPI.Query<GridStructure>())
        {
            if (grid.WorldAABB.Contains(playerPos))
                inGrid = grid;
            else
                matrices.Add(float4x4.TRS(grid.WorldAABB.Center - grid.WorldAABB.Extents, 
                    quaternion.identity, 
                    grid.WorldAABB.Size / GridStructure.CELL_SIZE));
        }
        Draw(matrices, ghostMat);
        
        matrices.Clear();
        foreach(var (gridItem, parent) in SystemAPI.Query<GridItem, Parent>())
        {
            var parentLtw = SystemAPI.GetComponent<LocalToWorld>(parent.Value);
            var bbox = gridItem.Area;
            var mtx = float4x4.TRS(GridStructure.CellToWorld(parentLtw, bbox.Min), parentLtw.Rotation,
                bbox.Size);
            if (math.any(inGrid.BoundsMax != 0) && bbox.Contains(GridStructure.CellFromPosition(parentLtw, playerPos)))
                inGridItem = mtx;
            else
                matrices.Add(mtx);
                
        }
        Draw(matrices, ghostMat);
        
        matrices.Clear();
        if (!inGridItem.Equals(default))
        {
            matrices.Add(inGridItem);
            Draw(matrices, occupationMat);
        }
    }

    void Draw(NativeList<float4x4> matrices, Material mat)
    {
        if (matrices.Length == 0)
            return;

        var mats = matrices.ToArrayNBC().Select(x => new Matrix4x4(x.c0, x.c1, x.c2, x.c3)).ToArray();
        while (mats.Length > 1000)
        {
            Graphics.DrawMeshInstanced(mesh, 0, mat, mats, 1000);
            mats = mats.Skip(1000).ToArray();
        }
        Graphics.DrawMeshInstanced(mesh, 0, mat, mats, mats.Length);
    }
}
