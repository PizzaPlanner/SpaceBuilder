using System.Linq;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Material = UnityEngine.Material;


public partial class DebugOccupationSystem : SystemBase
{
    private Mesh mesh;
    private Material occupationMat, ghostMat;
    Toggle _target;
    Toggle _rooms;
    Toggle _aabbs;
    Toggle _occupations;

    protected override void OnCreate()
    {
        mesh = Addressables.LoadAssetAsync<Mesh>("Cube").WaitForCompletion();

        occupationMat = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings").DebugOccupationMaterial;
        ghostMat = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings").DebugGhostMaterial;
    }
    
    protected override void OnStartRunning()
    {
        var ui = Object.FindObjectOfType<UIDocument>().rootVisualElement.Q("InfoTexts").Q("Grids");
        _target = ui.Q<Toggle>("target");
        _rooms = ui.Q<Toggle>("rooms");
        _aabbs = ui.Q<Toggle>("aabbs");
        _occupations = ui.Q<Toggle>("occupations");
    }

    protected override void OnUpdate()
    {
        if (_occupations.value)
            ShowOccupations();

        if (_aabbs.value)
            ShowAABBs();

        if (_rooms.value)
            ShowRooms();
        
        if(_target.value)
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
                matrices.Add(float4x4.TRS(grid.WorldAABB.Min, 
                    quaternion.identity, 
                    grid.WorldAABB.Size / GridStructure.CELL_SIZE));
        }
        Draw(matrices, ghostMat);
        
        matrices.Clear();
        foreach(var (gridItem, parent) in SystemAPI.Query<GridItem, Parent>().WithNone<Ghost>())
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
