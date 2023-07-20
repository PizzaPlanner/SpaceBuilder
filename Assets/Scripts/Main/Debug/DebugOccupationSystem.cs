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
        
        RequireForUpdate<DebugModeSystem.Active>();
    }
    
    protected override void OnStartRunning()
    {
        if (HasSingleton<Settings>())
            return;

        EntityManager.CreateEntity(typeof(Settings));    
        var ui = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("InfoTexts").Q("Grids");
        ui.Q<UnityEngine.UIElements.Toggle>("target").RegisterValueChangedCallback(x =>
        {
            var settings = GetSingleton<Settings>();
            settings.showTarget = x.newValue;
            SetSingleton(settings);
        });
        
        ui.Q<UnityEngine.UIElements.Toggle>("aabbs").RegisterValueChangedCallback(x =>
        {
            var settings = GetSingleton<Settings>();
            settings.showAABBs = x.newValue;
            SetSingleton(settings);
        });
        
        ui.Q<UnityEngine.UIElements.Toggle>("rooms").RegisterValueChangedCallback(x =>
        {
            var settings = GetSingleton<Settings>();
            settings.showRooms = x.newValue;
            SetSingleton(settings);
        });
        
        ui.Q<UnityEngine.UIElements.Toggle>("occupations").RegisterValueChangedCallback(x =>
        {
            var settings = GetSingleton<Settings>();
            settings.showOccupations = x.newValue;
            SetSingleton(settings);
        });
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

    private void ShowOccupations()
    {
        NativeList<float4x4> matrices = new NativeList<float4x4>(Allocator.Temp);
        Entities.ForEach((in CellMap cells, in GridStructure grid, in LocalToWorld ltw) =>
        {
            foreach(var item in cells.Objects)
                matrices.Add(float4x4.TRS(GridStructure.PositionFromCell(ltw, item.Min), ltw.Rotation, item.Max - item.Min));
        }).Run();
        
        Draw(matrices, occupationMat);
        
        matrices.Clear();
        Entities.ForEach((in CellMap cells, in GridStructure grid, in LocalToWorld ltw) =>
        {
            foreach(var item in cells.Walls)
                matrices.Add(float4x4.TRS(GridStructure.PositionFromCell(ltw, item.Min), ltw.Rotation, item.Max - item.Min));
        }).Run();
        
        Draw(matrices, ghostMat);
        
        matrices.Clear();
        Entities.ForEach((in Ghost ghost, in GridItem item) =>
        {
            if (item.Grid == Entity.Null)
                return;
        
            var parentLtw = GetComponent<LocalToWorld>(item.Grid);
            for(int x = item.Area.Min.x; x <= item.Area.Max.x; x++)
            for(int y = item.Area.Min.y; y <= item.Area.Max.y; y++)
            for(int z = item.Area.Min.z; z <= item.Area.Max.z; z++)
                matrices.Add(float4x4.TRS(GridStructure.PositionFromCell(parentLtw, item.Area.Max), parentLtw.Rotation, item.Area.Max - item.Area.Min));
        }).Run();

        Draw(matrices, ghostMat);
    }
    
    private void ShowRooms()
    {
        NativeList<float4x4> matricesUnexposed = new NativeList<float4x4>(Allocator.Temp);
        NativeList<float4x4> matricesExposed = new NativeList<float4x4>(Allocator.Temp);
        Entities.ForEach((DynamicBuffer<Rooms> rooms, in LocalToWorld ltw) =>
        {
            foreach(var item in rooms)
                matricesUnexposed.Add(float4x4.TRS(
                    GridStructure.PositionFromCell(ltw, item.Room.BoundingBox.Min), 
                    ltw.Rotation, 
                    item.Room.BoundingBox.Max - item.Room.BoundingBox.Min));

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
        GridStructure inGrid = default;
        float4x4 inGridItem = default;
        var playerPos = GetComponent<LocalToWorld>(GetSingletonEntity<LocalPlayer>()).Position;
        
        Entities.ForEach((in GridStructure grid) =>
        {
            if (grid.AABB.Contains(playerPos))
                inGrid = grid;
            else
                matrices.Add(float4x4.TRS(grid.AABB.Center - grid.AABB.Extents, quaternion.identity, grid.AABB.Size / GridStructure.CELL_SIZE));
        }).Run();
        Draw(matrices, ghostMat);
        
        matrices.Clear();
        Entities.ForEach((in GridItem gridItem) =>
        {
            var parentLtw = SystemAPI.GetComponent<LocalToWorld>(gridItem.Grid);
            var bbox = gridItem.Area;
            var mtx = float4x4.TRS(GridStructure.PositionFromCell(parentLtw, bbox.Min), parentLtw.Rotation,
                bbox.Size);
            if (math.any(inGrid.BoundsMax != 0) && bbox.Contains(GridStructure.CellFromPosition(parentLtw, playerPos)))
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

        var mats = matrices.ToArrayNBC().Select(x => new Matrix4x4(x.c0, x.c1, x.c2, x.c3)).ToArray();
        while (mats.Length > 1000)
        {
            Graphics.DrawMeshInstanced(mesh, 0, mat, mats, 1000);
            mats = mats.Skip(1000).ToArray();
        }
        Graphics.DrawMeshInstanced(mesh, 0, mat, mats, mats.Length);
    }
}
