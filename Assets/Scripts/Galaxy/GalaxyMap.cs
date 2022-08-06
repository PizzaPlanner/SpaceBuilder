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
                    (serializedObject.targetObject as GalaxyMap).GenerateSectorMap();
            }
            else
            {
                if(GUILayout.Button("Generate (Galaxy)"))
                    (serializedObject.targetObject as GalaxyMap).GenerateGalaxyMap();
            }
        }
    }

    public class GalaxyMap : MonoBehaviour
    {
        public int2 Start, End;
        public int Y;
        public Texture2D texture;
        public void GenerateGalaxyMap()
        {
            texture = new Texture2D(End.x - Start.x, End.y - Start.y);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var colors = new Color[texture.width * texture.height];
            for(int x = Start.x; x < End.x; x++)
            for (int z = Start.y; z < End.y; z++)
            {
                var current = (x - Start.x) * texture.height + (z - Start.y);
                var type = GalaxyTools.GetSectorType(new int3(x, Y, z));
                colors[current] =
                    (type == SectorType.Star ? Color.yellow :
                        type == SectorType.Nebula ? Color.blue :
                        type == SectorType.BlackHole ? Color.magenta :Color.clear);
                if (x == 0 && z == 0)
                    colors[current] += new Color(0,0,0,0.6f);
                else if (x == 0 && z >= 2 && z <= 7)
                    colors[current] = new Color(0,0,1,0.6f);
                else if (z == 0 && x >= 2 && x <= 7)
                    colors[current] = new Color(1,0,0,0.6f);
                if(current % 5000 == 0)
                    EditorUtility.DisplayProgressBar("Generating Map...",
                        $"{current}/{texture.width * texture.height}",
                        current * 1f / (texture.width * texture.height));
            }
            EditorUtility.ClearProgressBar();
            texture.SetPixels(colors);
            texture.Apply();
            var path = Application.dataPath + "/map.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log("Created "+path);
        }

        public void GenerateSectorMap()
        {
            texture = new Texture2D(End.x - Start.x, End.y - Start.y);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var colors = new Color[texture.width * texture.height];
            for(int x = Start.x; x < End.x; x++)
            for (int z = Start.y; z < End.y; z++)
            {
                var current = (x - Start.x) * texture.height + (z - Start.y);
                var type = GalaxyTools.GetSubSectorData(new LocalToGalaxy {Sector = new int3(x, Y, z)}).Type;
                colors[current] =
                    type == SubSectorType.Planet ? Color.green :
                    type == SubSectorType.AsteroidField ? Color.red :
                    type == SubSectorType.POI ? Color.cyan :
                    Color.clear;
                if (x == 0 && z == 0)
                    colors[current] += new Color(0,0,0,0.6f);
                else if (x == 0 && z >= 2 && z <= 7)
                    colors[current] = new Color(0,0,1,0.6f);
                else if (z == 0 && x >= 2 && x <= 7)
                    colors[current] = new Color(1,0,0,0.6f);
                if(current % 5000 == 0)
                    EditorUtility.DisplayProgressBar("Generating Map...",
                        $"{current}/{texture.width * texture.height}",
                        current * 1f / (texture.width * texture.height));
            }
            EditorUtility.ClearProgressBar();
            texture.SetPixels(colors);
            texture.Apply();
            var path = Application.dataPath + "/map.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log("Created "+path);
        }
    }
}
#endif
