using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 路面上的搬运小人：沿着既定的路面格子一跳一跳地走，走到终点就消失。
    /// 四个方向用四张图（前左/前右/后右/后左），网格移动正好只有这四个方向。
    /// </summary>
    public class RoadWorker : MonoBehaviour
    {
        public Sprite[] sprites = new Sprite[4];   // 0 正面 / 1 前右 / 2 后右 / 3 背面
        public List<Vector2Int> path = new List<Vector2Int>();
        public float hopDuration = 0.24f;
        public float hopHeight = 0.09f;
        public int variant = 1;

        SpriteRenderer _sr;
        int _index;
        float _t;

        /// <summary>
        /// 网格只有四个斜向：前右/前左/后右/后左。
        /// 素材给了「正面 · 前右 · 后右 · 背面」，所以左向一律用右侧那张图水平翻转。
        /// 返回 sprite 下标，flip=true 表示要 flipX。
        /// </summary>
        static void DirSprite(int di, int dj, out int index, out bool flip)
        {
            flip = false;
            if (di > 0) { index = 1; return; }       // +i：屏幕右下 = 前右
            if (dj > 0) { index = 1; flip = true; return; }  // +j：屏幕左下 = 前左（镜像）
            if (dj < 0) { index = 2; return; }       // -j：屏幕右上 = 后右
            if (di < 0) { index = 2; flip = true; return; }  // -i：屏幕左上 = 后左（镜像）
            index = 0;                                // 没动就正面站着
        }

        void Start()
        {
            Init();
        }

        /// <summary>建好精灵并放到路径起点（Spawn 时也会立刻调用一次，编辑期也能看到）。</summary>
        public void Init()
        {
            if (_sr != null) return;
            if (sprites == null || sprites.Length == 0 || sprites[0] == null)
            {
                Debug.LogWarning("[RoadWorker] 贴图为空，无法生成小人");
                return;
            }

            var go = new GameObject("sprite");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sprite = sprites[0];
            _sr.sortingOrder = 900;
            _sr.transform.localScale = Vector3.one * 0.5f;   // 半人高，看着更合适      // 小人压在建筑之上，避免被挡住

            if (path == null || path.Count == 0) { Destroy(gameObject); return; }
            transform.position = CellPos(path[0], 0f);
            if (path.Count > 1) ApplyDir(path[0], path[1]);
        }

        void Update()
        {
            if (path == null || _index >= path.Count - 1) return;

            _t += Time.deltaTime / Mathf.Max(0.02f, hopDuration);
            if (_t >= 1f)
            {
                _t -= 1f;
                _index++;
                if (_index >= path.Count - 1)
                {
                    Destroy(gameObject);
                    return;
                }
                ApplyDir(path[_index], path[_index + 1]);
            }

            // 一跳一跳的弧线
            float arc = Mathf.Sin(_t * Mathf.PI) * hopHeight;
            Vector3 a = CellPos(path[_index], arc);
            Vector3 b = CellPos(path[_index + 1], arc);
            transform.position = Vector3.Lerp(a, b, _t);
        }

        void ApplyDir(Vector2Int from, Vector2Int to)
        {
            if (_sr == null) return;
            DirSprite(to.x - from.x, to.y - from.y, out int idx, out bool flip);
            if (idx < sprites.Length && sprites[idx] != null) _sr.sprite = sprites[idx];
            _sr.flipX = flip;
        }

        static Vector3 CellPos(Vector2Int cell, float lift)
        {
            return PlacedTile.CellToWorld(cell.x, cell.y) + new Vector3(0f, 0.66f + lift, 0f);
        }

        /// <summary>生成一个小人走这条路面路径。</summary>
        public static void Spawn(Transform parent, List<Vector2Int> path, int variant)
        {
            if (path == null || path.Count < 2) return;

            var go = new GameObject("Worker");
            go.transform.SetParent(parent, false);
            var worker = go.AddComponent<RoadWorker>();
            worker.path = path;
            worker.variant = variant;

            string[] dirs = { "正面", "前右", "后右", "背面" };
            worker.sprites = new Sprite[4];
            for (int k = 0; k < 4; k++)
                worker.sprites[k] = Resources.Load<Sprite>($"Image/工人/工人{variant}_{dirs[k]}");

            worker.Init();
        }
    }
}
