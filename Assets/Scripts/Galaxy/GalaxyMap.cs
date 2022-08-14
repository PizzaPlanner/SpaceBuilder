#if UNITY_EDITOR
using System.Data;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Galaxy
{
    [CustomEditor(typeof(GalaxyMap))]
    public class GMEditor : Editor
    {
        private bool genSec;
        private Vector3Int sector;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            genSec = GUILayout.Toggle(genSec, "Generate Sector Map");
            if (genSec)
            {
                sector = EditorGUILayout.Vector3IntField("Sector", sector);
                var type = GalaxyTools.GetSectorType(new int3(sector.x, sector.y, sector.z));
                if(GUILayout.Button($"Generate ({type})"))
                    (serializedObject.targetObject as GalaxyMap).Generate(new int3(sector.x, sector.y, sector.z));
            }
            else
            {
                if(GUILayout.Button("Generate (Galaxy)"))
                    (serializedObject.targetObject as GalaxyMap).Generate(int3.zero);
            }
        }
    }

    public class GalaxyMap : MonoBehaviour
    {
        public int3 Center, Extents;
        public Texture3D texture;

        Color GetColor(int3 sector, int3 subsector)
        {
            if (math.all(subsector == int3.zero))
            {
                var type = GalaxyTools.GetSectorType(sector);
                return (type == SectorType.Star ? Color.yellow :
                    type == SectorType.Nebula ? Color.blue :
                    type == SectorType.BlackHole ? Color.magenta :Color.clear);
            }
            else
            {
                var type = GalaxyTools.GetSubSectorData(new LocalToGalaxy {Sector = sector, SubSector = subsector}).Type;
                return type == SubSectorType.Planet ? Color.green :
                    type == SubSectorType.AsteroidField ? Color.red :
                    type == SubSectorType.POI ? Color.cyan :
                    Color.clear;
            }
        }

        public void Generate(int3 sector)
        {
            texture = new Texture3D(2*Extents.x, 2*Extents.y, 2*Extents.z, TextureFormat.RGBA4444, 1);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var colors = new Color[texture.width * texture.height * texture.depth];
            for (int z = Center.z - Extents.z; z < Center.z + Extents.z; z++)
            for (int y = Center.y - Extents.y; y < Center.y + Extents.y; y++)
            for (int x = Center.x - Extents.x; x < Center.x + Extents.x; x++)
            {
                var current = (x + Extents.x - Center.x) + (y + Extents.y - Center.y) * texture.height + (z + Extents.z - Center.z) * texture.height * texture.depth;
                colors[current] = math.all(sector == 0) ? GetColor(new int3(x, y, z), int3.zero) : GetColor(sector, new int3(x, y, z));
                if(current % 10000 == 0)
                    EditorUtility.DisplayProgressBar("Generating Map...",
                        $"{current}/{texture.width * texture.height * texture.depth}",
                        current * 1f / (texture.width * texture.height * texture.depth));
            }
            EditorUtility.ClearProgressBar();
            texture.SetPixels(colors);
            texture.Apply();
            var path = Application.dataPath + "/map.png";
            AssetDatabase.CreateAsset(texture, "Assets/Map3D.asset");
            AssetDatabase.ImportAsset("Assets/Map3D.asset");
            Debug.Log("Created Assets/Map3D.asset");
        }
    }
}
#endif
