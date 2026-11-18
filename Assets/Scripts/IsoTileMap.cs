using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 2:1 等距（伪 3D 正交）瓦片拼装。
    /// 瓦片 PNG 统一为 384×384、底面菱形 304×152、锚点固定在底面「前角」（最下方顶点），
    /// 所以只要把方块放在 CellToWorld(i, j) 上就能严丝合缝地拼合。
    /// 遮挡：每个瓦片精灵自带「顶面 + 左前/右前两个侧面」，按 (i + j) 递增设置 sortingOrder
    /// （远的先画、近的后画），近处方块的整个剪影会盖住远处方块的两个侧面，顶面则永远完整可见。
    /// 民居 / 树林这类自带底座的物件，底座比普通方块薄（30~55px vs 129px），
    /// 按常规落位会陷进地里，所以用 padPixels 记录底座厚度、把它抬到与地面齐平（不垫台基）。
    /// </summary>
    [DisallowMultipleComponent]
    public class IsoTileMap : MonoBehaviour
    {
        public const int TileWidthPx = 304;
        public const int TileHeightPx = 152;
        public const float PixelsPerUnit = 304f;
        public const float HalfTileWidth = 0.5f;      // 世界单位下：一格宽 1
        public const float QuarterTileHeight = 0.25f;

        /// <summary>底座比普通方块薄的物件：抬到地面齐平（padPixels = 底座侧壁实际高度）。</summary>
        [System.Serializable]
        public class RaisedTile
        {
            public string tile;
            [Tooltip("该瓦片底座侧壁的像素高度；抬升量 = 地面高度 - 这个值")]
            public float padPixels;
        }

        [Header("布局")]
        [Tooltip("每行一条（j 自上而下递增），行内空格分隔（i 递增），'.' 表示空格子")]
        [SerializeField] string[] layout = System.Array.Empty<string>();

        /// <summary>存档用：当前 layout 快照（先调 SnapshotToLayout() 再读）。</summary>
        public string[] LayoutSnapshot => layout;

        /// <summary>layout[0][0] 对应的格子坐标（负坐标地图偏移用）。</summary>
        public int LayoutMinI { get; private set; }
        public int LayoutMinJ { get; private set; }

        [Header("资源")]
        [Tooltip("Sprite 所在的 Resources 子目录")]
        [SerializeField] string resourceFolder = "Image/地图瓦片";

        [Header("排序（遮挡）")]
        [Tooltip("排序号起点")]
        [SerializeField] int baseSortingOrder = 0;
        [Tooltip("每层等距斜列（i+j）之间的排序号步长，留出空位给同格的附属物体")]
        [SerializeField] int orderStepPerDiagonal = 4;

        [Header("贴地物件（民居 / 树林 / 棚屋这类自带底座的物件）")]
        [Tooltip("普通地形方块：底部到顶面的像素高度，也就是「地面」高度")]
        [SerializeField] float surfaceHeightPixels = 129f;
        [Tooltip("这里列出的瓦片会抬到顶面与地面齐平；没列出的按原样落位")]
        [SerializeField] RaisedTile[] raisedTiles =
        {
            new RaisedTile { tile = "单层民居", padPixels = 32f },
            new RaisedTile { tile = "两层民居", padPixels = 26f },
            new RaisedTile { tile = "木板棚屋", padPixels = 36f },
            new RaisedTile { tile = "树林", padPixels = 74f },
        };

        [Header("构建")]
        [Tooltip("场景里还没有烘焙好的子物体时，运行期自动拼一次")]
        [SerializeField] bool buildOnAwakeIfEmpty = true;

        readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        // ---------------- 地形数据（可无限延伸） ----------------
        // 场景里的 layout 只当初始种子；运行时地形存在按坐标索引的字典里，
        // 所以填海造陆 / 水陆互换可以写在地图原本范围之外（含负坐标）。

        readonly Dictionary<Vector2Int, string> _tiles = new Dictionary<Vector2Int, string>();
        readonly Dictionary<Vector2Int, float> _heights = new Dictionary<Vector2Int, float>();
        readonly Dictionary<Vector2Int, GameObject> _tileObjects = new Dictionary<Vector2Int, GameObject>();
        bool _seeded;

        [Header("地势")]
        [Tooltip("每高一层往上抬多少像素（layout 里写 「草地@1」 就是高一层，支持 @0.5）")]
        [SerializeField] float heightStepPixels = 84f;

        /// <summary>这一格的地势层数（0 = 平地，可以是 0.5 这种半层）。</summary>
        public float HeightAt(int i, int j)
        {
            EnsureSeeded();
            return _heights.TryGetValue(new Vector2Int(i, j), out float h) ? h : 0f;
        }

        /// <summary>石头类地形：抬升时垫层也用石头自己，堆起来才像石堆而不是土墙。</summary>
        static bool IsStoneLike(string tileName)
        {
            return tileName == "巨石" || tileName == "碎石" || tileName == "洞穴";
        }

        [Header("手工装饰")]
        [Tooltip("不属于地形数据、但要跟着地图一起重建的精灵（比如洞口顶盖石）。位置用格子 + 世界 y 记录")]
        [SerializeField] List<ExtraSprite> extras = new List<ExtraSprite>();

        /// <summary>手工加的装饰精灵（洞口顶盖之类）。</summary>
        [System.Serializable]
        public class ExtraSprite
        {
            public string tile;
            public int i;
            public int j;
            public float y;
            public float scale = 1f;
        }

        /// <summary>手工装饰列表（工程模式俯视时也要把它们画出来）。</summary>
        public List<ExtraSprite> Extras => extras;

        /// <summary>手工摆过的精灵位置（重建地图时还原，不按公式归位）。</summary>
        [System.Serializable]
        public class SpriteOverride
        {
            public string name;
            public Vector3 localPosition;
            public Vector3 localScale;
        }

        [Tooltip("手动摆过的精灵位置记录：重建地图时会按这里还原")]
        [SerializeField] List<SpriteOverride> spriteOverrides = new List<SpriteOverride>();

        Dictionary<string, SpriteOverride> _overrideLookup;

        Dictionary<string, SpriteOverride> OverrideLookup
        {
            get
            {
                if (_overrideLookup == null || _overrideLookup.Count != spriteOverrides.Count)
                {
                    _overrideLookup = new Dictionary<string, SpriteOverride>(spriteOverrides.Count + 1);
                    foreach (var o in spriteOverrides)
                        if (o != null && !string.IsNullOrEmpty(o.name)) _overrideLookup[o.name] = o;
                }
                return _overrideLookup;
            }
        }

        /// <summary>把场景里当前所有精灵的位置/缩放记下来（之后重建地图会照这个还原）。</summary>
        public int CaptureSpriteOverrides()
        {
            spriteOverrides.Clear();
            _overrideLookup = null;

            for (int k = 0; k < transform.childCount; k++)
            {
                Transform ch = transform.GetChild(k);
                spriteOverrides.Add(new SpriteOverride
                {
                    name = ch.name,
                    localPosition = ch.localPosition,
                    localScale = ch.localScale,
                });
            }

            return spriteOverrides.Count;
        }

        /// <summary>创建精灵后套用手工摆位（有记录就盖掉公式算出来的位置）。</summary>
        void ApplyOverride(GameObject go)
        {
            if (spriteOverrides == null || spriteOverrides.Count == 0) return;
            if (!OverrideLookup.TryGetValue(go.name, out SpriteOverride o) || o == null) return;

            go.transform.localPosition = o.localPosition;
            go.transform.localScale = o.localScale;
        }

        /// <summary>把当前场景里「跟地形对不上」的精灵收进 extras，并删掉原件（之后由 Build 统一重建）。</summary>
        public int CaptureExtras()
        {
            EnsureSeeded();
            extras.Clear();

            for (int k = transform.childCount - 1; k >= 0; k--)
            {
                Transform ch = transform.GetChild(k);
                if (ch.name.StartsWith("extra_")) continue;       // 自己生成的装饰，跳过
                string[] parts = ch.name.Split('_');
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[0], out int i) || !int.TryParse(parts[1], out int j)) continue;

                string rest = string.Join("_", parts, 2, parts.Length - 2);
                if (rest.Contains("_base")) continue;

                string tile = rest.EndsWith("_top") ? rest.Substring(0, rest.Length - 4) : rest;
                if (_tiles.Count > 0 && _tiles.TryGetValue(new Vector2Int(i, j), out string ground) && ground == tile) continue;
                if (_tiles.Count == 0) continue;                  // 地形数据没加载时不要乱收

                var sr = ch.GetComponent<SpriteRenderer>();
                extras.Add(new ExtraSprite
                {
                    tile = tile,
                    i = i,
                    j = j,
                    y = ch.localPosition.y,
                    scale = ch.localScale.x,
                });

                if (Application.isPlaying) Destroy(ch.gameObject);
                else DestroyImmediate(ch.gameObject);
            }

            return extras.Count;
        }

        void BuildExtras()
        {
            for (int k = 0; k < extras.Count; k++)
            {
                ExtraSprite e = extras[k];
                if (e == null || string.IsNullOrEmpty(e.tile)) continue;

                Sprite sprite = GetSprite(e.tile);
                if (sprite == null) continue;

                var go = new GameObject($"extra_{e.i}_{e.j}_{e.tile}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(CellToWorld(e.i, e.j).x, e.y, 0f);
                go.transform.localScale = Vector3.one * (e.scale <= 0f ? 1f : e.scale);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = CellSortingOrder(e.i, e.j);
                ApplyOverride(go);
            }
        }

        /// <summary>地势带来的世界坐标抬升（物体摆放时要加上）。</summary>
        public float HeightOffsetWorld(int i, int j)
        {
            return HeightAt(i, j) * heightStepPixels / PixelsPerUnit;
        }

        /// <summary>已有地形的坐标包围盒（随 SetTileAt 扩大）。</summary>
        int _minI, _maxI = -1, _minJ, _maxJ = -1;
        public int MinI { get { EnsureSeeded(); return _minI; } }
        public int MaxI { get { EnsureSeeded(); return _maxI; } }
        public int MinJ { get { EnsureSeeded(); return _minJ; } }
        public int MaxJ { get { EnsureSeeded(); return _maxJ; } }

        /// <summary>地形跨度（含已经长出去的部分）。</summary>
        public int Rows => MaxJ - MinJ + 1;

        /// <summary>地形跨度（含已经长出去的部分）。</summary>
        public int Columns => MaxI - MinI + 1;

        /// <summary>取某个格子铺的地形名字（没有地形返回空串）。</summary>
        public string TileAt(int i, int j)
        {
            EnsureSeeded();
            return _tiles.TryGetValue(new Vector2Int(i, j), out string t) ? t : "";
        }

        /// <summary>把场景里的 layout 灌进坐标字典（只做一次）。</summary>
        void EnsureSeeded()
        {
            // 注意：_seeded 可能是被中途设成 true 但字典还没填（比如 layout 当时为空），
            // 所以这里再判一次字典，避免「标记已加载、其实什么都没导入」的死状态。
            if (_seeded && _tiles.Count > 0) return;
            if (layout == null || layout.Length == 0) return;

            _seeded = true;
            _tiles.Clear();
            _heights.Clear();

            for (int j = 0; j < layout.Length; j++)
            {
                string[] row = layout[j].Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < row.Length; i++)
                {
                    string name = row[i];
                    if (name == "." || name == "-") continue;

                    // 支持「草地@1」「巨石@0.5」：@ 后面的数字是地势层数（台地 / 小山 / 悬石）
                    float height = 0f;
                    int at = name.LastIndexOf('@');
                    if (at > 0 && float.TryParse(name.Substring(at + 1),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                    {
                        height = Mathf.Max(0f, parsed);
                        name = name.Substring(0, at);
                    }

                    var key = new Vector2Int(LayoutMinI + i, LayoutMinJ + j);
                    _tiles[key] = name;
                    if (height > 0.001f) _heights[key] = height;
                    ExtendBounds(key.x, key.y);   // 用带偏移的真实坐标，否则负坐标那几行不会进包围盒、也不会被拼出来
                }
            }
        }

        void ExtendBounds(int i, int j)
        {
            if (_maxI < _minI || _maxJ < _minJ)
            {
                _minI = _maxI = i;
                _minJ = _maxJ = j;
                return;
            }
            if (i < _minI) _minI = i;
            if (i > _maxI) _maxI = i;
            if (j < _minJ) _minJ = j;
            if (j > _maxJ) _maxJ = j;
        }

        /// <summary>该格子是不是水体（深水 / 浅水 / 水面A / 水面B）。</summary>
        public bool IsWaterAt(int i, int j)
        {
            string t = TileAt(i, j);
            return t == "深水" || t == "浅水" || t == "水面A" || t == "水面B";
        }

        /// <summary>
        /// 这格水体是不是被陆地围死了：从它开始四邻接洪水填充，
        /// 只要有一格摸到地图边界就算连通大海；全被地形围住则返回 true。
        /// </summary>
        public bool IsEnclosedWater(int i, int j, int maxCells = 2000)
        {
            if (!IsWaterAt(i, j)) return false;
            EnsureSeeded();

            // 地图外的空格就是开放海域：以前只认「有水地形」的格子，
            // 岛缘那一圈浅水会被外侧的空格「围死」，港口直接建不了。
            bool Passable(Vector2Int c)
            {
                string t = TileAt(c.x, c.y);
                if (string.IsNullOrEmpty(t)) return true;
                return IsWaterAt(c.x, c.y);
            }

            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var start = new Vector2Int(i, j);
            queue.Enqueue(start);
            visited.Add(start);

            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };

            while (queue.Count > 0 && visited.Count < maxCells)
            {
                Vector2Int cur = queue.Dequeue();

                // 摸到地形范围的边缘 = 连着大海
                if (cur.x <= MinI || cur.y <= MinJ || cur.x >= MaxI || cur.y >= MaxJ)
                    return false;

                foreach (var d in dirs)
                {
                    Vector2Int n = cur + d;
                    if (visited.Contains(n)) continue;
                    if (!Passable(n)) continue;
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }
            return true;
        }

        /// <summary>两格水体是否在同一片连通水域里（四邻接洪水填充）。</summary>
        public bool SameWaterRegion(Vector2Int a, Vector2Int b, int maxCells = 4000)
        {
            if (!IsWaterAt(a.x, a.y) || !IsWaterAt(b.x, b.y)) return false;
            if (a == b) return true;

            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(a);
            visited.Add(a);

            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };

            while (queue.Count > 0 && visited.Count < maxCells)
            {
                Vector2Int cur = queue.Dequeue();
                foreach (var d in dirs)
                {
                    Vector2Int n = cur + d;
                    if (n == b) return true;
                    if (visited.Contains(n)) continue;
                    if (!IsWaterAt(n.x, n.y)) continue;
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }
            return false;
        }

        /// <summary>
        /// 只走水面的最短路径（四邻接 BFS）。起点若是陆地，自动从它旁边的水格出发。
        /// 找不到返回 null。
        /// </summary>
        /// <summary>
        /// 找一条只走水面的四邻接路径。
        /// allowOpenSea = true 时，地图外的空格（一片漆黑的海面）也算可航行，
        /// 用于给船划远洋航线；捕捞港算距离时不要开。
        /// </summary>
        public List<Vector2Int> FindWaterPath(Vector2Int start, Vector2Int goal, bool allowOpenSea = false)
        {
            bool Passable(Vector2Int c)
            {
                string t = TileAt(c.x, c.y);
                if (string.IsNullOrEmpty(t)) return allowOpenSea;    // 地图外 / 没铺地形的海
                return t == "深水" || t == "浅水" || t == "水面A" || t == "水面B";
            }

            if (!Passable(goal)) return null;

            var seeds = new List<Vector2Int>();
            if (Passable(start)) seeds.Add(start);
            else
            {
                var dirs0 = new[]
                {
                    new Vector2Int(1, 0), new Vector2Int(-1, 0),
                    new Vector2Int(0, 1), new Vector2Int(0, -1),
                };
                foreach (var d in dirs0)
                {
                    Vector2Int n = start + d;
                    if (Passable(n)) seeds.Add(n);
                }
            }
            if (seeds.Count == 0) return null;

            foreach (var seed in seeds)
                if (seed == goal) return new List<Vector2Int> { seed };

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, int>();
            var open = new WaterNodeHeap();
            foreach (var s in seeds)
            {
                cameFrom[s] = s;
                gScore[s] = 0;
                open.Push(s, Mathf.Abs(s.x - goal.x) + Mathf.Abs(s.y - goal.y));
            }

            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };

            bool found = false;
            int guard = 0;

            // 开放海域时把搜索限制在一个够大的方框里，避免往无边无际的黑海一路铺开
            int minX = Mathf.Min(start.x, goal.x) - 40, maxX = Mathf.Max(start.x, goal.x) + 40;
            int minY = Mathf.Min(start.y, goal.y) - 40, maxY = Mathf.Max(start.y, goal.y) + 40;

            // A*：目标可能在几百格外的远海，普通 BFS 会在 20000 步内铺不完（岛屿挡住后要绕一大圈），
            // 结果「同一片海」被判成不连通、捕捞港速率 0。带启发式朝目标搜，几百格也能瞬间算完。
            while (open.Count > 0 && !found && guard++ < 60000)
            {
                Vector2Int cur = open.Pop();
                if (cur == goal) { found = true; break; }

                int gc = gScore[cur];
                for (int k = 0; k < dirs.Length; k++)
                {
                    Vector2Int n = cur + dirs[k];
                    if (allowOpenSea && (n.x < minX || n.x > maxX || n.y < minY || n.y > maxY)) continue;
                    if (!Passable(n)) continue;

                    int ng = gc + 1;
                    if (gScore.TryGetValue(n, out int old) && ng >= old) continue;

                    gScore[n] = ng;
                    cameFrom[n] = cur;
                    open.Push(n, ng + Mathf.Abs(n.x - goal.x) + Mathf.Abs(n.y - goal.y));
                }
            }
            if (!found) return null;

            var path = new List<Vector2Int>();
            Vector2Int walk = goal;
            while (walk != cameFrom[walk])
            {
                path.Add(walk);
                walk = cameFrom[walk];
            }
            path.Add(walk);
            path.Reverse();
            return path;
        }

        /// <summary>水面寻路用的最小堆（按优先级取最小）。远距离 A* 用它才不会退化。</summary>
        sealed class WaterNodeHeap
        {
            readonly List<Vector2Int> _items = new List<Vector2Int>();
            readonly List<int> _priority = new List<int>();

            public int Count => _items.Count;

            public void Push(Vector2Int v, int p)
            {
                _items.Add(v);
                _priority.Add(p);
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_priority[parent] <= _priority[i]) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public Vector2Int Pop()
            {
                Vector2Int root = _items[0];
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _priority[0] = _priority[last];
                _items.RemoveAt(last);
                _priority.RemoveAt(last);

                int i = 0;
                int n = _items.Count;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, best = i;
                    if (l < n && _priority[l] < _priority[best]) best = l;
                    if (r < n && _priority[r] < _priority[best]) best = r;
                    if (best == i) break;
                    Swap(i, best);
                    i = best;
                }
                return root;
            }

            void Swap(int a, int b)
            {
                Vector2Int tv = _items[a]; _items[a] = _items[b]; _items[b] = tv;
                int tp = _priority[a]; _priority[a] = _priority[b]; _priority[b] = tp;
            }
        }

        /// <summary>
        /// 运行时换掉某一格的地形（修路 / 拆路 / 填海造陆），同步更新数据与那一格的精灵。
        /// 坐标不限于初始布局：地图可以往任意方向长出去。
        /// </summary>
        public void SetTileAt(int i, int j, string tileName)
        {
            EnsureSeeded();
            var key = new Vector2Int(i, j);

            if (string.IsNullOrEmpty(tileName))
            {
                _tiles.Remove(key);
            }
            else
            {
                _tiles[key] = tileName;
                ExtendBounds(i, j);
            }

            RemoveTileObject(key);
            if (string.IsNullOrEmpty(tileName)) return;

            Sprite sprite = GetSprite(tileName);
            if (sprite == null) return;

            float height = HeightAt(i, j);
            float stepWorld = heightStepPixels / PixelsPerUnit;
            int order = CellSortingOrder(i, j);

            int baseLayers = Mathf.CeilToInt(height);
            string baseTile = IsStoneLike(tileName) ? tileName : "泥土整块";
            for (int k = 0; k < baseLayers; k++)
            {
                Sprite baseSprite = GetSprite(baseTile);
                if (baseSprite == null) break;
                var baseGo = new GameObject($"{i}_{j}_{baseTile}_base{k}");
                baseGo.transform.SetParent(transform, false);
                baseGo.transform.localPosition = CellToWorld(i, j) + new Vector3(0f, k * stepWorld, 0f);
                var baseSr = baseGo.AddComponent<SpriteRenderer>();
                baseSr.sprite = baseSprite;
                baseSr.sortingOrder = order - 1;      // 永远压在本格地表下面
                ApplyOverride(baseGo);
            }

            var go = new GameObject(height > 0.001f ? $"{i}_{j}_{tileName}_top" : $"{i}_{j}_{tileName}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = CellToWorld(i, j) +
                new Vector3(0f, height * stepWorld + LiftPixelsFor(tileName) / PixelsPerUnit, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            _tileObjects[key] = go;

            // 这一格的地形变了，它自己和四邻的岸线泡沫都要重算
            RefreshShoreFoamAround(i, j);
            ApplyOverride(go);
        }

        /// <summary>删掉某一格的瓦片精灵（字典 + 名字兜底，热重载后也不会漏）。</summary>
        void RemoveTileObject(Vector2Int key)
        {
            if (_tileObjects.TryGetValue(key, out GameObject go) && go != null)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            _tileObjects.Remove(key);

            string prefix = key.x + "_" + key.y + "_";
            for (int c = transform.childCount - 1; c >= 0; c--)
            {
                Transform child = transform.GetChild(c);
                if (child == null || !child.name.StartsWith(prefix)) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>网格坐标 → 世界坐标（该格方块底面「前角」的位置）。</summary>
        public Vector3 CellToWorld(int i, int j)
        {
            return new Vector3((i - j) * HalfTileWidth, -(i + j) * QuarterTileHeight, 0f);
        }

        /// <summary>网格坐标 → 排序号：i+j 越大越靠近镜头，画在越上层。</summary>
        public int CellSortingOrder(int i, int j)
        {
            return baseSortingOrder + (i + j) * orderStepPerDiagonal;
        }

        /// <summary>该瓦片要抬多少像素才能让底座顶面落在「地面」上；普通方块返回 0。</summary>
        public float LiftPixelsFor(string tileName)
        {
            if (raisedTiles == null) return 0f;
            foreach (var r in raisedTiles)
                if (r != null && r.tile == tileName)
                    return surfaceHeightPixels - r.padPixels;
            return 0f;
        }

        public Sprite GetSprite(string tileName)
        {
            if (_spriteCache.TryGetValue(tileName, out Sprite cached)) return cached;
            Sprite sprite = Resources.Load<Sprite>(resourceFolder + "/" + tileName);
            if (sprite == null)
                Debug.LogWarning($"[IsoTileMap] 找不到瓦片 Sprite：{resourceFolder}/{tileName}");
            _spriteCache[tileName] = sprite;
            return sprite;
        }

        /// <summary>
        /// 整张换掉地形（编辑工具 / 重做开局地图用）：重新导入种子并重建精灵。
        /// </summary>
        public void ReplaceLayout(string[] newLayout, int minI = 0, int minJ = 0)
        {
            layout = newLayout;
            LayoutMinI = minI;
            LayoutMinJ = minJ;
            _seeded = false;
            _tiles.Clear();
            _heights.Clear();
            _tileObjects.Clear();
            _minI = _minJ = 0;
            _maxI = _maxJ = -1;
            EnsureSeeded();
            Build();
        }

        /// <summary>
        /// 把当前地形字典写回 layout 字段（编辑期做了地形改动后，保存场景才能带上）。
        /// 只支持包围盒左上角不越过 (0,0) 的情况——目前地图都是从 0 开始的。
        /// </summary>
        public bool SnapshotToLayout()
        {
            EnsureSeeded();
            if (_maxI < _minI || _maxJ < _minJ) return false;

            // 地图可以长到负坐标（往左上填海）：快照从包围盒左上角开始，
            // 起点记进 LayoutMinI/LayoutMinJ，ReplaceLayout 时按同样的偏移还原。
            // 以前这里直接 return false，负坐标的存档会沿用旧 layout ——
            // 读档后「地上没地形、附属物浮在海面上」。
            LayoutMinI = _minI;
            LayoutMinJ = _minJ;

            int cols = _maxI - _minI + 1;
            int rowCount = _maxJ - _minJ + 1;
            var rows = new string[rowCount];

            for (int r = 0; r < rowCount; r++)
            {
                int j = _minJ + r;
                var toks = new string[cols];
                for (int c = 0; c < cols; c++)
                {
                    int i = _minI + c;
                    if (!_tiles.TryGetValue(new Vector2Int(i, j), out string t) || string.IsNullOrEmpty(t))
                    {
                        toks[c] = ".";
                        continue;
                    }

                    float h = _heights.TryGetValue(new Vector2Int(i, j), out float hv) ? hv : 0f;
                    toks[c] = h > 0.001f
                        ? t + "@" + h.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                        : t;
                }
                rows[r] = string.Join(" ", toks);
            }

            layout = rows;
            return true;
        }

        void Awake()
        {
            // 先把 layout 灌进坐标字典：之后所有查询/写入都走字典，地图才能无限延伸
            EnsureSeeded();
            if (buildOnAwakeIfEmpty && transform.childCount == 0)
                Build();
        }

        [ContextMenu("重新拼装")]
        public void Build()
        {
            Clear();
            EnsureSeeded();

            int count = 0;
            float stepWorld = heightStepPixels / PixelsPerUnit;

            foreach (var kv in _tiles)
            {
                int i = kv.Key.x, j = kv.Key.y;
                string tileName = kv.Value;
                float height = HeightAt(i, j);
                int order = CellSortingOrder(i, j);

                // 高出来的部分用垫层填在下面，最上面才是地表瓦片。
                // 石头堆出来的柱体要用石头自己垫，否则会变成「一截泥土 + 一块石头」的怪样子。
                int baseLayers = Mathf.CeilToInt(height);
                string baseTile = IsStoneLike(tileName) ? tileName : "泥土整块";
                for (int k = 0; k < baseLayers; k++)
                {
                    if (CreateTile(i, j, baseTile, k * stepWorld, order - 1, "_base" + k) != null) count++;
                }

                float lift = height * stepWorld + LiftPixelsFor(tileName) / PixelsPerUnit;
                var top = CreateTile(i, j, tileName, lift, order, height > 0.001f ? "_top" : null);
                if (top != null) count++;

                // 民居/棚屋/树林这类「立在地块上的物件」脚底铺一层接触阴影。
                // 影子挂在瓦片下面，但局部 y 要把抬升量抵掉，才会落在地表而不是浮在半空。
                float liftPixels = LiftPixelsFor(tileName);
                if (top != null && liftPixels > 0.01f)
                {
                    float w = Mathf.Max(0.62f, top.bounds.size.x * 0.62f);
                    IsoShadow.Attach(top.transform,
                                     new Vector2(IsoShadow.LightOffset.x,
                                                 IsoShadow.SurfaceCenterY - lift + IsoShadow.LightOffset.y),
                                     order, w, IsoShadow.DefaultAlpha);
                }
            }

            Debug.Log($"[IsoTileMap] 拼装完成：{count} 块，范围 i {MinI}..{MaxI} / j {MinJ}..{MaxJ}，手工装饰 {extras.Count} 个");
            BuildExtras();
            RefreshShoreFoam();
        }

        // ---------------- 岸线泡沫 ----------------

        static bool IsWaterTileName(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            return n == "深水" || n == "浅水" || n.StartsWith("水面");
        }

        /// <summary>这一格是「有地形的非水体」＝岸。</summary>
        bool IsShore(int i, int j)
        {
            string n = TileAt(i, j);
            return !string.IsNullOrEmpty(n) && !IsWaterTileName(n);
        }

        /// <summary>
        /// 给所有「挨着岸的水格」贴泡沫：水格哪条边挨着岸就在那条边铺一张。
        /// 四张贴图在 Assets/Resources/Image/地图瓦片/泡沫_*.png，几何和其他瓦片一致，
        /// 所以直接挂成瓦片的子物体即可对齐。
        /// </summary>
        public void RefreshShoreFoam()
        {
            EnsureSeeded();
            IndexTileObjectsFromChildren();
            foreach (var key in new System.Collections.Generic.List<Vector2Int>(_tiles.Keys))
                RefreshTileFoam(key.x, key.y);
        }

        /// <summary>
        /// 场景里的地表精灵是按 `i_j_名字` 命名的。场景重新加载后运行时字典是空的，
        /// 这里按名字把已有的地表精灵登记回去（跳过垫层 `_baseN`）。
        /// </summary>
        void IndexTileObjectsFromChildren()
        {
            for (int c = 0; c < transform.childCount; c++)
            {
                Transform ch = transform.GetChild(c);
                if (ch == null || ch.name.Contains("_base")) continue;
                string[] parts = ch.name.Split('_');
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[0], out int i) || !int.TryParse(parts[1], out int j)) continue;
                var key = new Vector2Int(i, j);
                if (!_tileObjects.ContainsKey(key)) _tileObjects[key] = ch.gameObject;
            }
        }

        /// <summary>只重算某一格和它四邻的泡沫（地形改动后调用，避免全图重建）。</summary>
        public void RefreshShoreFoamAround(int i, int j)
        {
            RefreshTileFoam(i, j);
            RefreshTileFoam(i - 1, j);
            RefreshTileFoam(i + 1, j);
            RefreshTileFoam(i, j - 1);
            RefreshTileFoam(i, j + 1);
        }

        void RefreshTileFoam(int i, int j)
        {
            var key = new Vector2Int(i, j);
            GameObject go = FindTileObject(i, j);
            if (go == null) return;
            AddShoreFoam(go, key, TileAt(i, j), CellSortingOrder(i, j));
        }

        /// <summary>
        /// 取某一格的地表精灵物体。_tileObjects 是运行时字典，场景重新加载后是空的，
        /// 这时按 `i_j_名字` 的命名约定在子物体里兜底找一次（跳过垫层 `_baseN`）。
        /// </summary>
        GameObject FindTileObject(int i, int j)
        {
            var key = new Vector2Int(i, j);
            if (_tileObjects.TryGetValue(key, out GameObject go) && go != null) return go;

            string prefix = i + "_" + j + "_";
            for (int c = 0; c < transform.childCount; c++)
            {
                Transform ch = transform.GetChild(c);
                if (ch == null || !ch.name.StartsWith(prefix)) continue;
                if (ch.name.Contains("_base")) continue;
                _tileObjects[key] = ch.gameObject;
                return ch.gameObject;
            }
            return null;
        }

        void AddShoreFoam(GameObject tileGo, Vector2Int key, string tileName, int order)
        {
            // 先清掉旧泡沫，避免重复叠层
            for (int c = tileGo.transform.childCount - 1; c >= 0; c--)
            {
                Transform ch = tileGo.transform.GetChild(c);
                if (ch == null || !ch.name.StartsWith("foam_")) continue;
                if (Application.isPlaying) Destroy(ch.gameObject);
                else DestroyImmediate(ch.gameObject);
            }

            if (!IsWaterTileName(tileName)) return;

            int i = key.x, j = key.y;
            // 邻接方向 → 贴图：-i 屏幕左上、-j 屏幕右上、+i 屏幕右下、+j 屏幕左下
            TryFoam(tileGo, order, i - 1, j, "左后");
            TryFoam(tileGo, order, i, j - 1, "右后");
            TryFoam(tileGo, order, i + 1, j, "右前");
            TryFoam(tileGo, order, i, j + 1, "左前");
        }

        void TryFoam(GameObject tileGo, int order, int ni, int nj, string edge)
        {
            if (!IsShore(ni, nj)) return;

            // 每条边烘了 3 个变体，随机取一个来打散重复。
            // 注意**绝对不能 flip**：贴图的白边就在某一条边上，
            // flipX 会把它镜像到对面那条边（左后变右后），而且每次启动都随机。
            Sprite sp = GetSprite($"泡沫_{edge}_{Random.Range(1, 4)}");
            if (sp == null) return;

            var go = new GameObject("foam_" + edge);
            go.transform.SetParent(tileGo.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.003f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = order;
        }

        SpriteRenderer CreateTile(int i, int j, string tileName, float liftWorld, int sortingOrder, string suffix = null)
        {
            Sprite sprite = GetSprite(tileName);
            if (sprite == null) return null;

            var go = new GameObject(suffix == null ? $"{i}_{j}_{tileName}" : $"{i}_{j}_{tileName}{suffix}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = CellToWorld(i, j) + new Vector3(0f, liftWorld, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            if (suffix == null) _tileObjects[new Vector2Int(i, j)] = go;
            ApplyOverride(go);
            return sr;
        }

        [ContextMenu("清空")]
        public void Clear()
        {
            // 只删自己生成的精灵（`i_j_xxx` 和 `extra_...`），
            // 用户手动拖进来的其它物件不要动。
            for (int c = transform.childCount - 1; c >= 0; c--)
            {
                var child = transform.GetChild(c).gameObject;
                if (!IsOwnSprite(child.name)) continue;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            _tileObjects.Clear();
            _spriteCache.Clear();
        }

        /// <summary>名字是不是本系统生成的（`i_j_...` 或 `extra_...`）。</summary>
        static bool IsOwnSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("extra_")) return true;

            string[] parts = name.Split('_');
            return parts.Length >= 3
                && int.TryParse(parts[0], out _)
                && int.TryParse(parts[1], out _);
        }
    }
}
