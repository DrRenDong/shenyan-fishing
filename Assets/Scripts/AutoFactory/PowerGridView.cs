using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 电力图（P 键或 HUD「电力 P」）：在主场景上盖一层半透明蒙版，看地块的供电状态。
    /// - 灰 = 这块地没有电
    /// - 绿 = 有电力供应（接电网的地块）
    /// - 红 = 被远程电流束穿过（激光式输电，从远处的海岛直接把电送过来）
    /// 横向、纵向各有一条电流束，必定经过至少一格可建造的空地；每 refreshInterval 回合重掷一次。
    /// 电流束上跑粒子，方向就是电流的方向（横向由西往东、纵向由北往南）。
    /// </summary>
    public class PowerGridView : MonoBehaviour
    {
        public static PowerGridView Instance { get; private set; }

        [Header("开关")]
        public KeyCode toggleKey = KeyCode.P;
        public bool shown;

        [Header("电流束")]
        [Tooltip("多少回合重新随机一次电流束")]
        public int refreshInterval = 30;
        [Tooltip("横束所在行（j），-1 = 还没掷过")]
        public int horizontalRow = -1;
        [Tooltip("竖束所在列（i），-1 = 还没掷过")]
        public int verticalColumn = -1;
        public int turnsSinceRefresh;

        [Header("外观")]
        [Tooltip("无电：暗中性灰，盖上去让地块明显发灰发暗（太淡会被绿草地吃掉、看成有电）")]
        public Color noPowerColor = new Color(0.28f, 0.30f, 0.32f, 0.52f);
        public Color poweredColor = new Color(0.25f, 1f, 0.35f, 0.42f);
        public Color beamColor = new Color(0.98f, 0.20f, 0.34f, 0.52f);
        public Color sparkColor = new Color(1f, 0.95f, 0.70f, 1f);
        public int blockOrder = 1900;
        public int sparkOrder = 1911;
        [Tooltip("俯视模式下蒙版要压在俯视地形（2000+）之上")]
        public int topBlockOrder = 2400;
        public int topSparkOrder = 2411;
        public float sparkSize = 0.075f;
        public float sparkSpeed = 4.5f;

        /// <summary>
        /// 有电的格子。目前没有任何供电源（发电厂/电网还没做），所以这里是空的，
        /// 电力图上只会看到灰色和红色；以后接上电力就往这里登记格子。
        /// </summary>
        public readonly HashSet<Vector2Int> poweredCells = new HashSet<Vector2Int>();

        public bool IsShown => shown;
        public int TurnsUntilRefresh => Mathf.Max(0, refreshInterval - turnsSinceRefresh);

        // ---------------- 电网计算结果 ----------------

        /// <summary>被电流束实际流过的格子（棱镜塔分出来的支束也算）。</summary>
        readonly HashSet<Vector2Int> _beamCells = new HashSet<Vector2Int>();

        /// <summary>每个被打中的接收塔：格 → 电流束射来的方向。</summary>
        readonly Dictionary<Vector2Int, Vector2Int> _receiverFrom = new Dictionary<Vector2Int, Vector2Int>();

        /// <summary>实际画粒子的束段（起点格 → 终点格）。</summary>
        readonly List<Segment> _segments = new List<Segment>();

        /// <summary>已经分叉过的棱镜塔（每个塔只分一次，避免死循环）。</summary>
        readonly HashSet<Vector2Int> _prismDone = new HashSet<Vector2Int>();

        struct Segment
        {
            public Vector2Int From;
            public Vector2Int To;
        }

        public int BeamCellCount => _beamCells.Count;
        public int PoweredCellCount => poweredCells.Count;
        public int SegmentCount => _segments.Count;

        /// <summary>这一格有没有电：接收塔的 7×7 供电区，或者被电流束直接穿过（红色地块）。</summary>
        public bool IsCellPowered(Vector2Int cell)
        {
            return poweredCells.Contains(cell) || _beamCells.Contains(cell);
        }

        /// <summary>
        /// 全局查询：某格是否通电。场景里没装电力系统时一律返回 true（不限制生产）。
        /// </summary>
        public static bool GridPowered(Vector2Int cell)
        {
            PowerGridView view = Instance;
            if (view == null) return true;
            return view.IsCellPowered(cell);
        }

        GameObject _root;
        CanvasGroup _hintGroup;
        Text _hintText;
        bool _builtTopView;
        Sprite _diamond;
        Sprite _square;
        int _hintTurn = int.MinValue;
        int _hintRefresh = int.MinValue;

        readonly List<BeamFlow> _beams = new List<BeamFlow>();

        void Awake()
        {
            Instance = this;
            EnsureSprites();
        }

        void Start()
        {
            // 开局先算一遍：接收塔的朝向要按当前光束摆好，不用等玩家开电力图
            NotifyWorldChanged();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnDisable()
        {
            if (shown) Hide();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
            if (!shown) return;

            // 俯视/等距切换时蒙版要按新的坐标重新铺
            if (_builtTopView != EngineeringView.Active) Build();

            float dt = Time.deltaTime;
            for (int k = 0; k < _beams.Count; k++) _beams[k].Tick(dt);

            RefreshHint(false);
        }

        public void Toggle()
        {
            if (shown) Hide();
            else Show();
        }

        public void Show()
        {
            shown = true;
            GameAudio.Ensure().PlayPowerSfx();   // 电力图开启
            EnsureSprites();
            BuildHint();
            Recalculate();
            Build();
            if (_hintGroup != null) _hintGroup.gameObject.SetActive(true);
            RefreshHint(true);
            FactoryTutorial.OnPowerViewOpened();   // 第一次打开电力图时讲一次电力
            Debug.Log($"[电力图] 打开：横向 j={horizontalRow}、纵向 i={verticalColumn}，{TurnsUntilRefresh} 回合后变动，" +
                      $"电流束 {_beamCells.Count} 格 / {_segments.Count} 段，供电 {poweredCells.Count} 格");
        }

        public void Hide()
        {
            shown = false;
            DestroyRoot();
            if (_hintGroup != null) _hintGroup.gameObject.SetActive(false);
        }

        // ---------------- 电流束 ----------------

        /// <summary>每回合由 TurnManager 调用：到点就重新随机两条电流束。</summary>
        public void AdvanceTurn()
        {
            turnsSinceRefresh++;
            if (turnsSinceRefresh < refreshInterval) return;

            turnsSinceRefresh = 0;
            RollBeams();
            Recalculate();
            if (shown) Build();
            RefreshHint(true);
        }

        /// <summary>建造 / 拆除能源设施后调用：重算电网（接收塔朝向也会跟着更新）。</summary>
        public void NotifyWorldChanged()
        {
            var map = FindObjectOfType<IsoTileMap>();
            if (map == null) return;
            EnsureBeams(map);
            Recalculate();
            if (shown) Build();
            RefreshHint(true);
        }

        public void RollBeams(IsoTileMap map = null)
        {
            if (map == null) map = FindObjectOfType<IsoTileMap>();
            if (map == null) return;

            // 只从「确实穿过可建造空地」的行/列里抽，保证电流束落在能盖建筑的地方
            var rows = new List<int>();
            for (int j = map.MinJ; j <= map.MaxJ; j++)
            {
                for (int i = map.MinI; i <= map.MaxI; i++)
                {
                    if (!IsBuildableGround(map, i, j)) continue;
                    rows.Add(j);
                    break;
                }
            }

            var cols = new List<int>();
            for (int i = map.MinI; i <= map.MaxI; i++)
            {
                for (int j = map.MinJ; j <= map.MaxJ; j++)
                {
                    if (!IsBuildableGround(map, i, j)) continue;
                    cols.Add(i);
                    break;
                }
            }

            horizontalRow = rows.Count > 0 ? rows[Random.Range(0, rows.Count)] : Random.Range(map.MinJ, map.MaxJ + 1);
            verticalColumn = cols.Count > 0 ? cols[Random.Range(0, cols.Count)] : Random.Range(map.MinI, map.MaxI + 1);

            Debug.Log($"[电力图] 电流束重掷：横向 j={horizontalRow}（被横穿的可建格 {CountBuildableOnRow(map, horizontalRow)} 个），" +
                      $"纵向 i={verticalColumn}（被纵穿的可建格 {CountBuildableOnColumn(map, verticalColumn)} 个）");
        }

        void EnsureBeams(IsoTileMap map)
        {
            if (horizontalRow >= 0 && verticalColumn >= 0) return;
            RollBeams(map);
        }

        // ---------------- 电网计算 ----------------

        /// <summary>
        /// 从两条基础光束出发，逐格推进：碰到棱镜塔就分叉出两条垂直支束，碰到接收塔就被吸收截断。
        /// 算完后把被打中的接收塔标记朝向、并把 7×7 的地块加入供电集合。
        /// </summary>
        void Recalculate()
        {
            _beamCells.Clear();
            _receiverFrom.Clear();
            _segments.Clear();
            _prismDone.Clear();
            poweredCells.Clear();

            var map = FindObjectOfType<IsoTileMap>();
            if (map == null) return;
            EnsureBeams(map);

            // 束只跑在「有地形」的那一段上（地图是稀疏可延伸的，边缘可能是虚空）
            int rowFrom = map.MinI, rowTo = map.MaxI;
            while (rowFrom <= rowTo && string.IsNullOrEmpty(map.TileAt(rowFrom, horizontalRow))) rowFrom++;
            while (rowTo >= rowFrom && string.IsNullOrEmpty(map.TileAt(rowTo, horizontalRow))) rowTo--;

            int colFrom = map.MinJ, colTo = map.MaxJ;
            while (colFrom <= colTo && string.IsNullOrEmpty(map.TileAt(verticalColumn, colFrom))) colFrom++;
            while (colTo >= colFrom && string.IsNullOrEmpty(map.TileAt(verticalColumn, colTo))) colTo--;

            var queue = new Queue<Segment>();
            if (rowFrom <= rowTo)
                queue.Enqueue(new Segment
                {
                    From = new Vector2Int(rowFrom, horizontalRow),
                    To = new Vector2Int(rowTo, horizontalRow),
                });
            if (colFrom <= colTo)
                queue.Enqueue(new Segment
                {
                    From = new Vector2Int(verticalColumn, colFrom),
                    To = new Vector2Int(verticalColumn, colTo),
                });

            while (queue.Count > 0) WalkSegment(map, queue.Dequeue(), queue);

            UpdateReceivers(map);
        }

        void WalkSegment(IsoTileMap map, Segment seg, Queue<Segment> queue)
        {
            Vector2Int dir = new Vector2Int(
                seg.To.x == seg.From.x ? 0 : (seg.To.x > seg.From.x ? 1 : -1),
                seg.To.y == seg.From.y ? 0 : (seg.To.y > seg.From.y ? 1 : -1));

            int steps = Mathf.Max(Mathf.Abs(seg.To.x - seg.From.x), Mathf.Abs(seg.To.y - seg.From.y));
            Vector2Int last = seg.From;

            for (int k = 0; k <= steps; k++)
            {
                var cell = new Vector2Int(seg.From.x + dir.x * k, seg.From.y + dir.y * k);
                if (!Inside(map, cell)) continue;      // 虚空（填海留下的缺口）直接跳过

                _beamCells.Add(cell);
                last = cell;

                PlacedTile occ = FactoryWorld.Instance != null ? FactoryWorld.Instance.OccupantAt(cell) : null;

                // 接收塔：电流到这里就被吃掉，后面的格子上没有电
                if (occ is ReceiverTower)
                {
                    _receiverFrom[cell] = new Vector2Int(-dir.x, -dir.y);
                    break;
                }

                // 棱镜塔：向两侧（垂直于行进方向）各射出一条新电流束
                if (occ is PrismTower && _prismDone.Add(cell))
                {
                    Vector2Int perp = new Vector2Int(dir.y, dir.x);
                    int reach = map.Columns + map.Rows + 4;
                    queue.Enqueue(new Segment
                    {
                        From = cell,
                        To = new Vector2Int(cell.x + perp.x * reach, cell.y + perp.y * reach),
                    });
                    queue.Enqueue(new Segment
                    {
                        From = cell,
                        To = new Vector2Int(cell.x - perp.x * reach, cell.y - perp.y * reach),
                    });
                }
            }

            // 一段被完全挡在地图外的束没有可视化意义，就不画了
            if (last != seg.From || steps == 0) _segments.Add(new Segment { From = seg.From, To = last });
        }

        void UpdateReceivers(IsoTileMap map)
        {
            // 先全部清零：没被束打中的塔不供电、朝向保持不变
            foreach (var tower in FindObjectsOfType<ReceiverTower>()) tower.beamFrom = Vector2Int.zero;

            foreach (var kv in _receiverFrom)
            {
                PlacedTile occ = FactoryWorld.Instance != null ? FactoryWorld.Instance.OccupantAt(kv.Key) : null;
                var tower = occ as ReceiverTower;
                if (tower == null) continue;

                tower.SetBeamFrom(kv.Value);

                int r = Mathf.Max(0, tower.powerRadius);
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        var c = new Vector2Int(tower.gridX + dx, tower.gridY + dy);
                        if (!Inside(map, c)) continue;
                        if (map.IsWaterAt(c.x, c.y)) continue;    // 海面不通电
                        poweredCells.Add(c);
                    }
                }
            }
        }

        static bool Inside(IsoTileMap map, Vector2Int c)
        {
            // 有地形才算在地图里（坐标可以无限延伸，没有地形就是虚空）
            return !string.IsNullOrEmpty(map.TileAt(c.x, c.y));
        }

        /// <summary>这一格能不能盖建筑：陆地、没被地块/建筑占着（和建造规则同一套判定）。</summary>
        static bool IsBuildableGround(IsoTileMap map, int i, int j)
        {
            if (map == null) return false;
            string ground = map.TileAt(i, j);
            if (string.IsNullOrEmpty(ground)) return false;
            if (map.IsWaterAt(i, j)) return false;
            if (BuildController.IsOccupiedGround(ground)) return false;
            if (FactoryWorld.Instance != null && FactoryWorld.Instance.OccupantAt(new Vector2Int(i, j)) != null) return false;
            return true;
        }

        static int CountBuildableOnRow(IsoTileMap map, int j)
        {
            int n = 0;
            for (int i = map.MinI; i <= map.MaxI; i++) if (IsBuildableGround(map, i, j)) n++;
            return n;
        }

        static int CountBuildableOnColumn(IsoTileMap map, int i)
        {
            int n = 0;
            for (int j = map.MinJ; j <= map.MaxJ; j++) if (IsBuildableGround(map, i, j)) n++;
            return n;
        }

        // ---------------- 蒙版 ----------------

        void Build()
        {
            DestroyRoot();

            var map = FindObjectOfType<IsoTileMap>();
            if (map == null) return;
            if (_beamCells.Count == 0) Recalculate();

            _builtTopView = EngineeringView.Active;
            _root = new GameObject("PowerGridOverlay");
            _root.transform.SetParent(transform, false);
            _root.transform.position = Vector3.zero;

            int grey = 0, red = 0, green = 0;
            for (int j = map.MinJ; j <= map.MaxJ; j++)
            {
                for (int i = map.MinI; i <= map.MaxI; i++)
                {
                    string ground = map.TileAt(i, j);
                    if (string.IsNullOrEmpty(ground)) continue;

                    var cell = new Vector2Int(i, j);
                    bool inBeam = _beamCells.Contains(cell);
                    bool water = map.IsWaterAt(i, j);
                    // 海面只有电流束经过时才盖（灰蒙版只铺陆地地块）
                    if (!inBeam && water) continue;

                    Color color;
                    if (inBeam) { color = beamColor; red++; }
                    else if (poweredCells.Contains(cell)) { color = poweredColor; green++; }
                    else { color = noPowerColor; grey++; }

                    MakeBlock(i, j, color);
                }
            }

            foreach (var seg in _segments)
                MakeBeam(BlockPos(seg.From.x, seg.From.y), BlockPos(seg.To.x, seg.To.y));

            Debug.Log($"[电力图] 蒙版：灰 {grey} 格 / 红 {red} 格 / 绿 {green} 格，" +
                      $"光束 {_segments.Count} 段（{(_builtTopView ? "俯视" : "等距")}）");
        }

        void MakeBlock(int i, int j, Color color)
        {
            var go = new GameObject($"pw_{i}_{j}");
            go.transform.SetParent(_root.transform, false);
            go.transform.position = BlockPos(i, j);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _builtTopView ? _square : _diamond;
            sr.color = color;
            sr.sortingOrder = _builtTopView ? topBlockOrder : blockOrder;
        }

        void MakeBeam(Vector3 start, Vector3 end)
        {
            if (_square == null) return;
            int order = _builtTopView ? topSparkOrder : sparkOrder;
            var flow = new BeamFlow(_square, sparkColor, order, sparkSize, sparkSpeed, start, end, _root.transform);
            _beams.Add(flow);
        }

        /// <summary>格子在当前视图下的「格块中心」：等距是顶面中心（格点 + 0.66），俯视是方格中心。</summary>
        Vector3 BlockPos(int i, int j)
        {
            Vector3 p = EngineeringView.CellToWorld(i, j);
            if (!EngineeringView.Active) p.y += 0.66f;
            return p;
        }

        void DestroyRoot()
        {
            _beams.Clear();
            if (_root == null) return;
            if (Application.isPlaying) Destroy(_root);
            else DestroyImmediate(_root);
            _root = null;
        }

        // ---------------- 顶部提示条 ----------------

        void RefreshHint(bool force)
        {
            if (_hintText == null) return;
            int turn = TurnManager.Instance != null ? TurnManager.Instance.Turn : 1;
            if (!force && turn == _hintTurn && turnsSinceRefresh == _hintRefresh) return;
            _hintTurn = turn;
            _hintRefresh = turnsSinceRefresh;

            _hintText.text = $"电力图　灰 = 没有电　绿 = 有电力供应　红 = 被电流束穿过\n" +
                             $"横向电流束：第 {horizontalRow} 行　纵向电流束：第 {verticalColumn} 列　" +
                             $"{TurnsUntilRefresh} 回合后变动　P 关闭";
        }

        void BuildHint()
        {
            if (_hintGroup != null) return;

            Transform existing = transform.Find("PowerHintCanvas");
            if (existing != null)
            {
                _hintGroup = existing.GetComponent<CanvasGroup>();
                if (_hintGroup != null)
                {
                    Transform t = existing.Find("Bg/Text");
                    if (t != null) _hintText = t.GetComponent<Text>();
                    return;
                }
            }

            var canvasGo = new GameObject("PowerHintCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5043;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            _hintGroup = canvasGo.AddComponent<CanvasGroup>();
            _hintGroup.blocksRaycasts = false;
            _hintGroup.interactable = false;

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            var bg = bgGo.GetComponent<RectTransform>();
            bg.SetParent(canvasGo.transform, false);
            // 贴右上角，避开左上角的回合面板
            bg.anchorMin = new Vector2(1f, 1f);
            bg.anchorMax = new Vector2(1f, 1f);
            bg.pivot = new Vector2(1f, 1f);
            bg.sizeDelta = new Vector2(980f, 84f);
            bg.anchoredPosition = new Vector2(-24f, -24f);
            bgGo.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 1f);

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(bg, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accentGo.GetComponent<Image>().color = new Color(0.95f, 0.72f, 0.30f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rect = textGo.GetComponent<RectTransform>();
            rect.SetParent(bg, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(20f, 4f);
            rect.offsetMax = new Vector2(-20f, -4f);
            var text = textGo.GetComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
            text.fontSize = 20;
            text.color = new Color(0.95f, 0.97f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            _hintText = text;

            canvasGo.SetActive(false);
        }

        // ---------------- 素材 ----------------

        void EnsureSprites()
        {
            if (_diamond == null) _diamond = MakeDiamond(256);
            if (_square == null) _square = MakeSquare(8);
        }

        /// <summary>2:1 的菱形（PPU = 宽，所以世界尺寸 1 × 0.5，正好盖住一格顶面）。</summary>
        static Sprite MakeDiamond(int width)
        {
            int height = Mathf.Max(2, width / 2);
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = Mathf.Abs((x + 0.5f) / width - 0.5f) * 2f;
                    float ny = Mathf.Abs((y + 0.5f) / height - 0.5f) * 2f;
                    float a = Mathf.Clamp01((1f - (nx + ny)) * width * 0.5f);
                    px[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width);
        }

        /// <summary>1×1 世界单位的白色方块（俯视模式的格块 + 粒子都用它）。</summary>
        static Sprite MakeSquare(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var px = new Color32[size * size];
            for (int k = 0; k < px.Length; k++) px[k] = new Color32(255, 255, 255, 255);

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }

    /// <summary>一条电流束上的电火花流：沿 start → end 匀速前进，首尾淡出形成脉冲感。</summary>
    class BeamFlow
    {
        readonly Vector3 _start;
        readonly Vector3 _end;
        readonly float _length;
        readonly float _speed;
        readonly SpriteRenderer[] _dots;
        readonly float[] _t;

        public BeamFlow(Sprite dotSprite, Color color, int order, float size, float speed,
                        Vector3 start, Vector3 end, Transform parent)
        {
            _start = start;
            _end = end;
            _length = Vector3.Distance(start, end);
            _speed = speed;

            int count = Mathf.Clamp(Mathf.RoundToInt(_length / 0.42f), 8, 80);
            _dots = new SpriteRenderer[count];
            _t = new float[count];

            for (int k = 0; k < count; k++)
            {
                var go = new GameObject("spark");
                go.transform.SetParent(parent, false);
                go.transform.localScale = Vector3.one * size;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = dotSprite;
                sr.color = color;
                sr.sortingOrder = order;

                _dots[k] = sr;
                _t[k] = k / (float)count;
                go.transform.position = Vector3.Lerp(start, end, _t[k]);
            }
        }

        public void Tick(float dt)
        {
            if (_length <= 0.001f) return;
            float step = _speed * dt / _length;

            for (int k = 0; k < _dots.Length; k++)
            {
                _t[k] += step;
                if (_t[k] > 1f) _t[k] -= 1f;

                SpriteRenderer sr = _dots[k];
                if (sr == null) continue;

                sr.transform.position = Vector3.Lerp(_start, _end, _t[k]);

                Color c = sr.color;
                c.a = Mathf.Sin(_t[k] * Mathf.PI);
                sr.color = c;
            }
        }
    }
}
