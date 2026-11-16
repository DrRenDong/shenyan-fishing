using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 烘焙好的点击轮廓（由精灵 alpha 通道离线生成，存在 Resources/Image/ClickShapes.json）。
    /// 坐标已换算成「相对精灵锚点的世界单位」，直接可以喂给 PolygonCollider2D。
    /// </summary>
    public static class ClickShapeDatabase
    {
        [System.Serializable]
        class Entry
        {
            public string name;
            public float[] points;
            [Tooltip("true = 坐标已经是世界单位（精灵有自己的 pivot，提前换算好）")]
            public bool world;
        }

        [System.Serializable]
        class File
        {
            public int canvas;
            public float pivotX;
            public float pivotY;
            public Entry[] shapes;
        }

        const string ResourcePath = "Image/ClickShapes";
        const float PixelsPerUnit = 304f;

        static Dictionary<string, Vector2[]> _map;
        static bool _loaded;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _map = new Dictionary<string, Vector2[]>();

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[ClickShapeDatabase] 找不到 {ResourcePath}.json，点击层将回退成菱形");
                return;
            }

            File file = JsonUtility.FromJson<File>(asset.text);
            if (file == null || file.shapes == null) return;

            foreach (var entry in file.shapes)
            {
                if (entry.points == null || entry.points.Length < 6) continue;
                var pts = new Vector2[entry.points.Length / 2];
                for (int k = 0; k < pts.Length; k++)
                {
                    float px = entry.points[k * 2];
                    float py = entry.points[k * 2 + 1];
                    pts[k] = entry.world
                        ? new Vector2(px, py)
                        : new Vector2((px - file.pivotX) / PixelsPerUnit,
                                      (py - file.pivotY) / PixelsPerUnit);
                }
                _map[entry.name] = pts;
            }
        }

        public static bool TryGet(string spriteName, out Vector2[] points)
        {
            Load();
            return _map.TryGetValue(spriteName, out points);
        }

        public static int Count
        {
            get { Load(); return _map.Count; }
        }
    }
}
