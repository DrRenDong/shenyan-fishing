using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Shenyan
{
    /// <summary>
    /// 珊瑚海床岸坡（流式）：最左 10 列加高做房子台地，之后 20% 概率随机降格并封顶，
    /// 玩家一直向右航行时按需继续往右生成，永远走不完。
    /// 瓦片上带浅色珊瑚/沙的一侧就是朝空暴露的那一面。
    /// </summary>
    [DisallowMultipleComponent]
    public class SandbarTerrain : MonoBehaviour
    {
        [Header("瓦片（16x16，PPU=16）")]
        [SerializeField] Sprite tileInner;      // 0 面：无暴露，地下填充
        [SerializeField] Sprite tileTop;        // 1 面：只露顶面
        [SerializeField] Sprite tileTopRight;   // 2 面：顶 + 右
        [SerializeField] Sprite tileTopLeft;    // 2 面：顶 + 左（留空则水平翻转 tileTopRight）
        [SerializeField] Sprite tileThree;      // 3 面：顶 + 左 + 右（没有就退化成 2 面）

        [Header("地形")]
        [SerializeField, Min(1)] int prewarmColumns = 400;     // 开局先铺这么多列
        [SerializeField, Min(1)] int lookAheadColumns = 80;    // 相机右侧预留多少列
        [SerializeField] int plateauHeight = -13;              // 岸台高度（负数 = 更高，顶面 = 3 - 该值）
        [SerializeField, Min(0)] int boostColumns = 10;        // 最左边再加高的列数
        [SerializeField] int boostHeight = 2;                  // 加高格数
        [SerializeField, Range(0, 100)] int gentleChance = 10; // 加高段之后的降格概率
        [SerializeField] int maxDrop = 4;                      // 保底：不能降到让木桩悬空
        [SerializeField] int maxDropEndColumn = 131;            // 最后一个木桩所在列，之后解除最低地势限制
        [SerializeField] int fillBottomRow = 24;                // 每列一直填到这个行号，保证实心
        [SerializeField] int tileDepth = 15;                     // 顶面往下铺多少格瓦片，其余纯色实心
        [SerializeField] Color solidFillColor = new Color(0.24f, 0.25f, 0.36f);
        [SerializeField] int solidSortingOrder = -5;             // 纯色层在地形之后、背景之前
        [SerializeField] int startX = 0;
        [SerializeField] int startY = 0;

        [Header("鱼群噪点图（每列一个期望条数，越往右越密）")]
        [SerializeField] int fishNoiseStartColumn = 24;          // 从这一列开始有鱼
        [SerializeField] int fishNoiseRampColumns = 300;         // 到这一列升到满密度
        [SerializeField] float fishNoiseMin = 0.05f;             // 近海每列期望条数
        [SerializeField] float fishNoiseMax = 1.35f;             // 深海每列期望条数
        [SerializeField, Range(0f, 0.9f)] float fishNoiseGrain = 0.35f;   // 噪点起伏幅度

        [Header("随机")]
        [SerializeField] bool randomSeedOnPlay = true;
        [SerializeField] int seed = 0;

        [Header("渲染")]
        [SerializeField] string sortingLayer = "Default";
        [SerializeField] int sortingOrder = 1;   // 要压在码头木桩上面

        [Header("资源")]
        [SerializeField] string tileFolder = "Image/地形/珊瑚/";

        const string GridObjectName = "SandbarGrid";

        Tilemap _tilemap;
        Tilemap _solidMap;          // 地形下方的纯色实心层
        Tile _solidTile;
        Camera _camera;
        System.Random _rng;
        int _nextColumn;
        readonly List<int> _heights = new List<int>();
        readonly List<float> _fishNoise = new List<float>();   // 和 _heights 一一对应：这一列该刷多少鱼
        readonly Dictionary<Sprite, Tile> _tileCache = new Dictionary<Sprite, Tile>();

        public int Seed => seed;
        public IReadOnlyList<int> Heights => _heights;
        public IReadOnlyList<float> FishNoise => _fishNoise;
        public int GeneratedColumns => _nextColumn;

        /// <summary>给一个世界 x，返回那一列的鱼群噪点值（期望条数）。地形还没铺到就返回 false。</summary>
        public bool TryGetFishNoise(float worldX, out float density)
        {
            int col = Mathf.RoundToInt(worldX) - startX;
            if (col < 0 || col >= _fishNoise.Count) { density = 0f; return false; }
            density = _fishNoise[col];
            return true;
        }

        void Awake()
        {
            LoadTilesIfMissing();
            if (randomSeedOnPlay) seed = NewSeed();
            _camera = Camera.main;
            Restart();
        }

        void LateUpdate()
        {
            if (_camera == null) return;
            // 相机往右走多少，就补多少列
            int need = Mathf.CeilToInt(_camera.transform.position.x - startX) + lookAheadColumns;
            if (need > _nextColumn - 1) EnsureColumns(need);
        }

        [ContextMenu("重新生成（沿用种子）")]
        public void Restart()
        {
            EnsureTilemap();
            _tilemap.ClearAllTiles();
            if (_solidMap != null) _solidMap.ClearAllTiles();
            _heights.Clear();
            _fishNoise.Clear();
            _nextColumn = 0;
            _rng = new System.Random(seed);
            EnsureColumns(prewarmColumns - 1);
        }

        [ContextMenu("换种子重新生成")]
        public void RegenerateWithNewSeed()
        {
            seed = NewSeed();
            Restart();
        }

        /// <summary>给一个世界 x，返回该列海床顶面的世界 y（钩子落脚判断用）。</summary>
        public bool TryGetGroundTopY(float worldX, out float topY)
        {
            topY = float.NegativeInfinity;
            if (_heights.Count == 0) return false;
            int col = Mathf.RoundToInt(worldX) - startX;
            if (col < 0 || col >= _heights.Count) return false;
            // 瓦片顶边 = cell y + 1；cell y = startY - row
            topY = startY - _heights[col] + 1f;
            return true;
        }

        /// <summary>保证第 0 ~ upToColumn 列都已生成。</summary>
        public void EnsureColumns(int upToColumn)
        {
            while (_nextColumn <= upToColumn)
            {
                int x = _nextColumn;
                int h = NextHeight(x);
                _heights.Add(h);
                _fishNoise.Add(ComputeFishNoise(x));
                PlaceColumn(x, h);
                _nextColumn++;
            }
        }

        /// <summary>
        /// 鱼群噪点图的一列：越往右期望条数越高（smoothstep 缓升，封顶 fishNoiseMax），
        /// 再叠一条沿 x 的 Perlin 噪点让鱼群有疏有密。
        /// 只跟 x 有关 —— 同一列上下的密度完全一样。
        /// </summary>
        float ComputeFishNoise(int x)
        {
            if (x < fishNoiseStartColumn) return 0f;

            float t = Mathf.Clamp01((x - fishNoiseStartColumn) / (float)Mathf.Max(1, fishNoiseRampColumns));
            t = t * t * (3f - 2f * t);
            float density = Mathf.Lerp(fishNoiseMin, fishNoiseMax, t);

            float phase = (x + seed % 997) * 0.13f;
            float grain = Mathf.PerlinNoise(phase, 0.37f) * 2f - 1f;
            return Mathf.Clamp(density * (1f + grain * fishNoiseGrain), 0f, fishNoiseMax);
        }

        /// <summary>前 10 格保持台地高度 → 第 11 格一次性降 2 格 → 之后 10% 概率降格。</summary>
        int NextHeight(int x)
        {
            if (x < boostColumns) return plateauHeight - boostHeight;   // 0..9 保持 -15
            if (x == boostColumns) return plateauHeight;                // 第 11 格一次性降到 -13

            int prev = _heights[x - 1];
            bool limited = x <= maxDropEndColumn;   // 码头范围内要保底，让木桩还能触地
            bool canDrop = !limited || prev < maxDrop;
            bool drop = canDrop && _rng.Next(0, 100) < gentleChance;
            return prev + (drop ? 1 : 0);
        }

        void PlaceColumn(int x, int top)
        {
            // 顶面往下 tileDepth 格铺瓦片，再往下用纯色实心填满
            int tileEnd = Mathf.Min(top + tileDepth - 1, fillBottomRow);
            for (int row = top; row <= tileEnd; row++)
            {
                var cell = CellOf(x, row);
                if (row == top) PlaceTop(cell, x, row);
                else SetTile(cell, tileInner, false);
            }

            for (int row = tileEnd + 1; row <= fillBottomRow; row++)
                SetSolid(CellOf(x, row));

            // 这一列的右邻现在才知道，回头把左边那格的顶面重算一遍
            if (x > 0) PlaceTop(CellOf(x - 1, _heights[x - 1]), x - 1, _heights[x - 1]);
        }

        /// <summary>纯色实心块（地形瓦片下方的"土体"）。</summary>
        void SetSolid(Vector3Int cell)
        {
            if (_solidMap == null) return;
            _solidMap.SetTile(cell, GetSolidTile());
        }

        Tile GetSolidTile()
        {
            if (_solidTile != null) return _solidTile;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            _solidTile = ScriptableObject.CreateInstance<Tile>();
            _solidTile.sprite = sprite;
            _solidTile.color = solidFillColor;
            _solidTile.colliderType = Tile.ColliderType.None;
            return _solidTile;
        }

        void PlaceTop(Vector3Int cell, int x, int row)
        {
            bool leftOpen = !HasGround(x - 1, row);
            bool rightOpen = !HasGround(x + 1, row);

            if (!leftOpen && !rightOpen) SetTile(cell, tileTop, false);
            else if (!leftOpen) SetTile(cell, tileTopRight, false);
            else if (!rightOpen) SetTile(cell, tileTopLeft != null ? tileTopLeft : tileTopRight, tileTopLeft == null);
            else SetTile(cell, tileThree != null ? tileThree : tileTopLeft, false);
        }

        /// <summary>相邻列在这一行有没有地形（还没生成的列视为没有）。</summary>
        bool HasGround(int x, int row)
        {
            if (x < 0 || x >= _heights.Count) return false;
            return row >= _heights[x] && row <= fillBottomRow;
        }

        void SetTile(Vector3Int cell, Sprite sprite, bool flipX)
        {
            if (sprite == null) return;
            _tilemap.SetTile(cell, GetTile(sprite));
            if (flipX)
            {
                _tilemap.SetTileFlags(cell, TileFlags.None);
                _tilemap.SetTransformMatrix(cell, Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)));
            }
        }

        Vector3Int CellOf(int column, int row)
        {
            // 相对 Grid 的格子坐标，16 PPU 时一格正好 1 个世界单位
            return new Vector3Int(startX + column, startY - row, 0);
        }

        Tile GetTile(Sprite sprite)
        {
            if (_tileCache.TryGetValue(sprite, out var cached)) return cached;
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.Sprite;   // 地形带碰撞箱
            tile.name = sprite.name;
            _tileCache[sprite] = tile;
            return tile;
        }

        void EnsureTilemap()
        {
            if (_tilemap != null) return;

            var gridTr = transform.Find(GridObjectName);
            Grid grid;
            if (gridTr != null)
            {
                grid = gridTr.GetComponent<Grid>();
            }
            else
            {
                var gridGo = new GameObject(GridObjectName);
                gridGo.transform.SetParent(transform, false);
                grid = gridGo.AddComponent<Grid>();
                grid.cellSize = Vector3.one;
                gridTr = gridGo.transform;
            }

            var mapTr = gridTr.Find("SandbarTilemap");
            var mapGo = mapTr != null ? mapTr.gameObject : new GameObject("SandbarTilemap");
            if (mapTr == null) mapGo.transform.SetParent(gridTr, false);

            // GetComponent 缺失时返回「伪 null」，不能用 ?? 判断
            _tilemap = mapGo.GetComponent<Tilemap>();
            if (_tilemap == null) _tilemap = mapGo.AddComponent<Tilemap>();

            var renderer = mapGo.GetComponent<TilemapRenderer>();
            if (renderer == null) renderer = mapGo.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;

            // 地形碰撞箱：合并成一个复合碰撞体，钩子/鱼靠它判断"碰到泥土"
            var body = mapGo.GetComponent<Rigidbody2D>();
            if (body == null) body = mapGo.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            var tileCol = mapGo.GetComponent<TilemapCollider2D>();
            if (tileCol == null) tileCol = mapGo.AddComponent<TilemapCollider2D>();

            var composite = mapGo.GetComponent<CompositeCollider2D>();
            if (composite == null) composite = mapGo.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            tileCol.usedByComposite = true;

            // ---- 纯色实心层：铺在瓦片下方，视觉上是"土体"，不参与碰撞 ----
            var fillTr = gridTr.Find("SandbarFill");
            var fillGo = fillTr != null ? fillTr.gameObject : new GameObject("SandbarFill");
            if (fillTr == null) fillGo.transform.SetParent(gridTr, false);

            _solidMap = fillGo.GetComponent<Tilemap>();
            if (_solidMap == null) _solidMap = fillGo.AddComponent<Tilemap>();

            var fillRenderer = fillGo.GetComponent<TilemapRenderer>();
            if (fillRenderer == null) fillRenderer = fillGo.AddComponent<TilemapRenderer>();
            fillRenderer.sortingLayerName = sortingLayer;
            fillRenderer.sortingOrder = solidSortingOrder;
        }

        void LoadTilesIfMissing()
        {
            if (tileInner == null) tileInner = Load("珊瑚_0面_内部");
            if (tileTop == null) tileTop = Load("珊瑚_1面_上");
            if (tileTopRight == null) tileTopRight = Load("珊瑚_2面_上右");
            if (tileTopLeft == null) tileTopLeft = Load("珊瑚_2面_上左");
        }

        Sprite Load(string fileName)
        {
            var sprite = Resources.Load<Sprite>(tileFolder + fileName);
            if (sprite == null) Debug.LogWarning($"[SandbarTerrain] 找不到瓦片：Resources/{tileFolder}{fileName}");
            return sprite;
        }

        static int NewSeed() => (int)(System.DateTime.Now.Ticks & 0x7FFFFFFF);
    }
}
