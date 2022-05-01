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
        public Texture2D texture;
        public void Generate()
        {
            texture = new Texture2D(End.x - Start.x, End.y - Start.y);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var colors = new Color[texture.width * texture.height];
            for(int x = Start.x; x < End.x; x++)
            for (int y = Start.y; y < End.y; y++)
            {

                var current = (x - Start.x) * texture.height + (y - Start.y);
                var data = GalaxyTools.GetSectorData(new LocalToGalaxy{XInTenKm = x, YInTenKm = y});
                colors[current] =
                    (data.Type == SectorType.Star ? Color.red :
                    data.Type == SectorType.Nebula ? Color.blue :
                    data.Type == SectorType.SubStar ? Color.yellow :
                    data.Type == SectorType.AsteroidField ? Color.cyan :
                    data.Type == SectorType.StarCompanion ? Color.green : Color.black) * new Color(1,1,1, data.Perlin);
                if (x == 1 && ((y >= 2 && y <= 7) || y == 11))
                    colors[current] = Color.black;
                else if (x == 2 && (y == 10 || y == 12))
                    colors[current] = Color.black;
                else if(x == 0 && y == 11)
                    colors[current] = Color.black;
                if(y % 500 == 0)
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
