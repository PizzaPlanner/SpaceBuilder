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
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if(GUILayout.Button("Generate"))
                (serializedObject.targetObject as GalaxyMap).Generate();
        }
    }

    public class GalaxyMap : MonoBehaviour
    {
        public int2 Start, End;
        public int Y;
        public Texture2D texture;
        public void Generate()
        {
            texture = new Texture2D(End.x - Start.x, End.y - Start.y);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var colors = new Color[texture.width * texture.height];
            for(int x = Start.x; x < End.x; x++)
            for (int z = Start.y; z < End.y; z++)
            {
                var current = (x - Start.x) * texture.height + (z - Start.y);
                var data = GalaxyTools.GetSectorData(new LocalToGalaxy{Sector = new int3(x, Y, z)});
                colors[current] =
                    (data.Type == SectorType.Star ? Color.red :
                    data.Type == SectorType.Nebula ? Color.blue :
                    data.Type == SectorType.SubStar ? Color.yellow :
                    data.Type == SectorType.AsteroidField ? Color.cyan :
                    data.Type == SectorType.StarCompanion ? Color.green : Color.clear);
                if (x == 0 && z == 0)
                    colors[current] += new Color(0,0,0,0.3f);
                else if (x == 0 && z >= 2 && z <= 7)
                    colors[current] = new Color(0,0,1,0.3f);
                else if (z == 0 && x >= 2 && x <= 7)
                    colors[current] = new Color(1,0,0,0.3f);
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
