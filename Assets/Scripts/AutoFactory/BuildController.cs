using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 建造模式：从建造面板选一项 → 鼠标处出现半透明幽灵预览 → 单击花钱开工。
    /// 预览外框绿=可建、红=不可建；建筑（港口除外）可以在任意陆地上建，
    /// 只要那一格还没有建筑 / 路面 / 民居 / 农田 / 树林 / 巨石 / 碎石。
    /// 按 R（或点屏幕下方的「镜像」按钮）左右翻转建筑；Esc 取消；也支持拆除模式。
    /// </summary>
    public class BuildController : MonoBehaviour
    {
        public static BuildController Instance { get; private set; }

        public Camera targetCamera;
        public Transform worldRoot;
        public Color okColor = new Color(0.45f, 0.95f, 0.55f, 1f);
        public Color badColor = new Color(0.95f, 0.35f, 0.35f, 1f);

        public bool Active { get; private set; }
        public bool Demolishing { get; private set; }
        public bool Mirrored { get; private set; }
        public BuildDef Current { get; private set; }
        public string LastMessage { get; private set; } = "";

        SpriteRenderer _ghost;
        LineRenderer _outline;
        IsoTileMap _map;

        CanvasGroup _actionGroup;
        Image _mirrorImage;

        void Awake()
        {
            Instance = this;
            if (targetCamera == null) targetCamera = Camera.main;
            BuildGhost();
            BuildActionBar();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------- 幽灵 ----------------

        void BuildGhost()
        {
            if (_ghost != null) return;

            Transform oldGhost = transform.Find("Ghost");
            if (oldGhost != null) _ghost = oldGhost.GetComponent<SpriteRenderer>();
            Transform oldLine = transform.Find("GhostOutline");
            if (oldLine != null) _outline = oldLine.GetComponent<LineRenderer>();
            if (_ghost != null && _outline != null) return;

            if (_ghost == null)
            {
                var go = new GameObject("Ghost");
                go.transform.SetParent(transform, false);
                _ghost = go.AddComponent<SpriteRenderer>();
                _ghost.sortingOrder = 500;
                _ghost.color = new Color(1f, 1f, 1f, 0.5f);
                _ghost.enabled = false;
            }

            if (_outline == null)
            {
                var lineGo = new GameObject("GhostOutline");
                lineGo.transform.SetParent(transform, false);
                _outline = lineGo.AddComponent<LineRenderer>();
                _outline.useWorldSpace = true;
                _outline.loop = true;
                _outline.positionCount = 4;
                _outline.widthMultiplier = 0.03f;
                _outline.numCapVertices = 0;
                _outline.numCornerVertices = 0;
                var mat = new Material(Shader.Find("Sprites/Default"));
                _outline.sharedMaterial = mat;
                _outline.sortingOrder = 501;
                _outline.enabled = false;
            }
        }

        public void Enter(BuildDef def)
        {
            BuildGhost();
            Current = def;
            Demolishing = false;
            Mirrored = false;
            RefreshMirrorSprite();
            Active = def != null;
            LastMessage = "";
            if (!Active) Cancel();
            else SetActionBar(true);
        }

        /// <summary>进入拆除模式：点建筑就拆掉并退一半钱。</summary>
        public void EnterDemolish()
        {
            BuildGhost();
            Current = null;
            Demolishing = true;
            Active = true;
            LastMessage = "";
            SetActionBar(true);
        }

        public void Cancel()
        {
            Active = false;
            Demolishing = false;
            Current = null;
            if (_ghost != null) _ghost.enabled = false;
            if (_outline != null) _outline.enabled = false;
            SetActionBar(false);
            BuildPanel.Instance?.Hide();
        }

        public void ToggleMirror()
        {
            Mirrored = !Mirrored;
            if (_ghost != null) _ghost.flipX = Mirrored;
            RefreshMirrorSprite();
        }

        /// <summary>镜像按钮跟着状态换图（开=实心双块，关=空心双方框）。</summary>
        void RefreshMirrorSprite()
        {
            if (_mirrorImage == null) return;
            _mirrorImage.sprite = Resources.Load<Sprite>(
                "Image/UI/工厂按钮/" + (Mirrored ? "btn_mirror_on" : "btn_mirror_off"));
        }

        void Update()
        {
            if (!Active || targetCamera == null) return;

            if (Input.GetKeyDown(KeyCode.R)) ToggleMirror();
            if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }

            Vector3 screen = Input.touchCount > 0
                ? (Vector3)Input.GetTouch(0).position
                : Input.mousePosition;
            Vector3 world = targetCamera.ScreenToWorldPoint(screen);
            Vector2Int cell = EngineeringView.WorldToCell(world);
            PreviewAt(cell);

            // 连续修建：按住拖动/滑动，走到新格子就继续建
            bool overUI = UiPointer.OverAnyUI();
            bool holding = Input.GetMouseButton(0)
                           || (Input.touchCount > 0 && Input.GetTouch(0).phase != TouchPhase.Ended && Input.touchCount < 2);
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
                _dragCell = new Vector2Int(int.MinValue, int.MinValue);

            if (holding && !overUI && cell != _dragCell)
            {
                _dragCell = cell;
                TryPlace(cell, keepMode: true);
            }
        }

        Vector2Int _dragCell = new Vector2Int(int.MinValue, int.MinValue);

        /// <summary>在指定格子刷新幽灵预览（鼠标用，截图/验证时也可以直接调）。</summary>
        public void PreviewAt(Vector2Int cell)
        {
            if (Demolishing)
            {
                PlacedTile target = FactoryWorld.Instance != null ? FactoryWorld.Instance.OccupantAt(cell) : null;
                bool destructive = target != null && (target.CanDemolish || AbilityData.CanDemolishProps);
                if (target == null) LastMessage = "这一格没有可拆的";
                else if (!destructive) LastMessage = $"「{DisplayTileName(target)}」是地块，不能拆";
                else LastMessage = $"拆除 {DisplayTileName(target)}（退 {Mathf.RoundToInt(target.buildCost * AbilityData.DemolishRefundRate)}）";
                UpdateOutline(cell, destructive);
                if (_ghost != null) _ghost.enabled = false;
                return;
            }

            bool ok = Validate(cell, out string reason);
            LastMessage = reason;
            UpdateGhost(cell, ok);
        }

        void UpdateGhost(Vector2Int cell, bool ok)
        {
            if (_ghost == null) return;

            if (Current == null)
            {
                _ghost.enabled = false;
                _outline.enabled = false;
                return;
            }

            bool topView = EngineeringView.Active;

            // 俯视模式只认俯视素材：接收塔四个朝向共用「接收塔」这一张；
            // 找不到就不画精灵（只留外框），不要把等距斜图塞进俯视图。
            string topId = Current.TileId;
            if (topView && !string.IsNullOrEmpty(topId) && topId.StartsWith("接收塔")) topId = "接收塔";

            Sprite s = topView
                ? Resources.Load<Sprite>("Image/俯视瓦片/" + topId)
                : TileCatalog.LoadSprite(Current.TileId);
            if (s == null && !topView) s = TileCatalog.LoadSprite(Current.TileId);

            _ghost.sprite = s;
            if (s != null)
            {
                TileDef def = TileCatalog.Find(Current.TileId);
                float lift = (!topView && def != null) ? def.LiftPixels : 0f;
                _ghost.transform.position = EngineeringView.CellToWorld(cell.x, cell.y) + new Vector3(0f, lift / 304f, 0f);
                _ghost.flipX = !topView && Mirrored;
                _ghost.enabled = true;
            }
            else
            {
                _ghost.enabled = false;      // 俯视没有对应素材：只显示格子外框
            }

            UpdateOutline(cell, ok);
        }

        void UpdateOutline(Vector2Int cell, bool ok)
        {
            if (_outline == null) return;
            bool topView = EngineeringView.Active;
            Vector3 c = topView
                ? EngineeringView.CellToWorld(cell.x, cell.y)
                : EngineeringView.CellToWorld(cell.x, cell.y) + new Vector3(0f, 0.66f, 0f);

            if (topView)
            {
                _outline.SetPosition(0, c + new Vector3(-0.5f, 0.5f, 0f));
                _outline.SetPosition(1, c + new Vector3(0.5f, 0.5f, 0f));
                _outline.SetPosition(2, c + new Vector3(0.5f, -0.5f, 0f));
                _outline.SetPosition(3, c + new Vector3(-0.5f, -0.5f, 0f));
            }
            else
            {
                _outline.SetPosition(0, c + new Vector3(0f, 0.25f, 0f));
                _outline.SetPosition(1, c + new Vector3(0.5f, 0f, 0f));
                _outline.SetPosition(2, c + new Vector3(0f, -0.25f, 0f));
                _outline.SetPosition(3, c + new Vector3(-0.5f, 0f, 0f));
            }
            Color col = ok ? okColor : badColor;
            if (Demolishing) col = ok ? badColor : new Color(0.6f, 0.6f, 0.6f, 0.6f);
            _outline.startColor = col;
            _outline.endColor = col;
            _outline.enabled = true;
        }

        // ---------------- 校验 / 放置 ----------------

        public bool Validate(Vector2Int cell, out string reason)
        {
            reason = "";
            if (Current == null) { reason = "没选建造项"; return false; }
            if (!BuildCatalog.IsUnlocked(Current)) { reason = "这项工艺还没解锁（完成王老板的订单）"; return false; }
            if (FactoryWorld.Instance == null) { reason = "世界未初始化"; return false; }

            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_map != null)
            {
                string ground = _map.TileAt(cell.x, cell.y);
                if (string.IsNullOrEmpty(ground)) { reason = "超出地图"; return false; }

                if (Current.OnWater)
                {
                    if (!_map.IsWaterAt(cell.x, cell.y)) { reason = "港口只能建在水体里"; return false; }
                    if (!HasAdjacentLand(cell)) { reason = "港口要挨着陆地"; return false; }
                    if (_map.IsEnclosedWater(cell.x, cell.y))
                    {
                        reason = "这片水体被陆地围死了，出不去大海";
                        return false;
                    }
                }
                else
                {
                    if (_map.IsWaterAt(cell.x, cell.y)) { reason = "不能建在水上"; return false; }
                    if (_map.HeightAt(cell.x, cell.y) > 0.001f) { reason = "这块地抬高了，只能当风景（不能建造）"; return false; }
                    if (IsOccupiedGround(ground)) { reason = $"这一格已经有「{ground}」了"; return false; }
                }
            }

            if (!CanOccupy(FactoryWorld.Instance.OccupantAt(cell))) { reason = "这一格已经有东西了"; return false; }

            int price = CostOf(cell);
            if (PlayerProgress.Gold < price)
            {
                reason = $"金币不足（需要 {price}）";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 这些地形已经算「有东西了」，不能直接在上面盖建筑：
        /// 民居 / 棚屋 / 农田 / 树林 / 巨石 / 石板路（以及所有水面，由 IsWaterAt 处理）。
        /// 「碎石」不算——它本来就是「就地取材、建在上面省 10%」的地块。
        /// 其余地形（草地、沙地、泥土块等）都是空地，可以随便建。
        /// </summary>
        static readonly HashSet<string> OccupiedGrounds = new HashSet<string>
        {
            "单层民居", "两层民居", "木板棚屋", "农田", "树林", "巨石", "石砖路",
            // 沙滩可以建（和草地差不多，只是拆掉后不会翻土长草）；洞穴在山体里照旧不能建
            "洞穴",
        };

        public static bool IsOccupiedGround(string ground)
        {
            return !string.IsNullOrEmpty(ground) && OccupiedGrounds.Contains(ground);
        }

        /// <summary>
        /// 这一格的东西能不能被新建筑顶掉：碎石只是铺在地形上的一层点击壳，
        /// 建筑可以直接压上去（压上去就按「就地取材」的碎石规则算）。其它东西一律不行。
        /// </summary>
        static bool IsReplaceableOccupant(PlacedTile tile)
        {
            return tile != null && tile.colliderOnly && tile.tileId == LandmarkBonus.Gravel;
        }

        static bool CanOccupy(PlacedTile tile)
        {
            return tile == null || IsReplaceableOccupant(tile);
        }

        /// <summary>这一格的实际建造费用（碎石地块 -10%）。</summary>
        public int CostOf(Vector2Int cell)
        {
            if (Current == null) return 0;
            return Mathf.RoundToInt(Current.Cost * LandmarkBonus.BuildCostMultiplier(cell));
        }

        bool HasNeighbourRoadOrBuilding(Vector2Int cell)
        {
            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };
            foreach (var d in dirs)
            {
                if (FactoryWorld.Instance.RoadComponentOf(cell + d) >= 0) return true;
                PlacedTile other = FactoryWorld.Instance.OccupantAt(cell + d);
                if (other != null && !(other is RoadTile)) return true;
            }
            return false;
        }

        /// <summary>四邻接里有没有陆地格（港口必须靠岸）。</summary>
        bool HasAdjacentLand(Vector2Int cell)
        {
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_map == null) return false;
            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };
            foreach (var d in dirs)
            {
                Vector2Int n = cell + d;
                string ground = _map.TileAt(n.x, n.y);
                if (!string.IsNullOrEmpty(ground) && !_map.IsWaterAt(n.x, n.y)) return true;
            }
            return false;
        }

        /// <summary>在鼠标所指格子下单建造。keepMode=true 时保持建造模式（连续修建用）。</summary>
        public bool TryPlace(Vector2Int cell, bool keepMode = false)
        {
            if (Demolishing)
            {
                // 拆不动（地块）时留在拆除模式里，方便继续点别的格子
                bool done = TryDemolish(cell);
                if (!keepMode && done) Cancel();
                return done;
            }

            if (!Validate(cell, out string reason))
            {
                LastMessage = reason;
                if (!keepMode) Cancel();
                return false;
            }

            BuildDef def = Current;
            int price = CostOf(cell);
            PlayerProgress.AddGold(-price);

            // 碎石那层点击壳先清掉：建筑直接压上去（-10% 建造费已经算在 CostOf 里了）
            PlacedTile replaceable = FactoryWorld.Instance.OccupantAt(cell);
            if (IsReplaceableOccupant(replaceable))
            {
                FactoryWorld.Instance.Unregister(replaceable);
                if (Application.isPlaying) Destroy(replaceable.gameObject);
                else DestroyImmediate(replaceable.gameObject);
            }

            var go = new GameObject($"{def.Name}_{cell.x}_{cell.y}");
            go.transform.SetParent(worldRoot != null ? worldRoot : FactoryWorld.Instance.transform, false);

            PlacedTile tile;
            switch (def.Category)
            {
                case BuildCategory.Road:
                {
                    var road = go.AddComponent<RoadTile>();
                    road.gridX = cell.x; road.gridY = cell.y; road.tileId = "";
                    if (_map == null) _map = FindObjectOfType<IsoTileMap>();
                    if (_map != null)
                    {
                        road.originalGround = _map.TileAt(cell.x, cell.y);
                        _map.SetTileAt(cell.x, cell.y, "石砖路");    // 真正把地面画成石板路
                    }
                    tile = road;
                    FactoryWorld.Instance.RegisterRoad(cell);
                    break;
                }
                case BuildCategory.Port:
                {
                    if (def.CargoDock)
                    {
                        var dock = go.AddComponent<CargoDockBuilding>();
                        dock.gridX = cell.x; dock.gridY = cell.y; dock.tileId = def.TileId;
                        dock.powered = true;
                        dock.dailyCost = 50f;
                        dock.throughputPerTurn = 6f;
                        tile = dock;
                    }
                    else
                    {
                        var port = go.AddComponent<FishingPortBuilding>();
                        port.gridX = cell.x; port.gridY = cell.y; port.tileId = def.TileId;
                        port.powered = true;
                        port.dailyCost = 12f;
                        tile = port;
                    }
                    break;
                }
                case BuildCategory.Facility:
                {
                    PlacedTile facility;
                    if (def.Name == "小型仓库")
                    {
                        var store = go.AddComponent<WarehouseBuilding>();
                        store.gridX = cell.x; store.gridY = cell.y; store.tileId = def.TileId;
                        facility = store;
                    }
                    else if (def.Name == "分拣区")
                    {
                        var sorter = go.AddComponent<SortingFacility>();
                        sorter.gridX = cell.x; sorter.gridY = cell.y; sorter.tileId = def.TileId;
                        facility = sorter;
                    }
                    else if (def.Name == "接收塔")
                    {
                        var receiver = go.AddComponent<ReceiverTower>();
                        receiver.gridX = cell.x; receiver.gridY = cell.y; receiver.tileId = def.TileId;
                        facility = receiver;
                    }
                    else if (def.Name == "棱镜塔")
                    {
                        var prism = go.AddComponent<PrismTower>();
                        prism.gridX = cell.x; prism.gridY = cell.y; prism.tileId = def.TileId;
                        facility = prism;
                    }
                    else
                    {
                        facility = go.AddComponent<PlacedTile>();
                        facility.gridX = cell.x; facility.gridY = cell.y; facility.tileId = def.TileId;
                    }
                    tile = facility;
                    break;
                }
                default:
                {
                    var factory = go.AddComponent<FactoryBuilding>();
                    factory.gridX = cell.x; factory.gridY = cell.y; factory.tileId = def.TileId;
                    factory.factoryType = def.FactoryType;
                    factory.ratePerTurn = DefaultRate(def.FactoryType);
                    factory.dailyCost = DefaultDailyCost(def.FactoryType);
                    tile = factory;
                    break;
                }
            }

            tile.EnsureVisual();
            if (Mirrored) tile.SetMirrored(true);
            FactoryWorld.Instance.Register(tile);
            tile.buildCost = price;
            tile.StartConstruction(def.Turns);

            // 建筑 / 设施盖在草地上：地面翻成泥土，等拆掉后再慢慢长回草地。
            // 路面不参与：它自己已经记过 originalGround（草地），这里再记会把「石砖路」写进去，
            // 导致拆路后还原成石板路（曾经的实际 bug）。
            if (!def.OnWater && def.Category != BuildCategory.Road)
            {
                if (_map == null) _map = FindObjectOfType<IsoTileMap>();
                if (_map != null)
                {
                    string ground = _map.TileAt(cell.x, cell.y);
                    tile.originalGround = ground;
                    if (ground == "草地" || ground == LandmarkBonus.Gravel)
                        _map.SetTileAt(cell.x, cell.y, "泥土整块");
                }
            }

            if (def.Category == BuildCategory.Road)
                tile.AdvanceConstruction();

            // 能源设施会改变电流束的走向与供电范围，放好就重算一次电网
            if (tile is ReceiverTower || tile is PrismTower)
            {
                PowerGridView.Instance?.NotifyWorldChanged();
                FactoryTutorial.OnPowerFacilityBuilt();   // 第一次摆塔时讲一次电力设施
            }

            GameAudio.Ensure().PlayBuildSfx();   // 建造音效

            // 工程模式下建完要重铺俯视图（地面/新建筑才会出现），
            // 同时把新建筑自己的等距精灵藏掉，免得在角落露一个斜的
            if (EngineeringView.Active && EngineeringView.Instance != null)
                EngineeringView.Instance.Refresh();

            if (!keepMode) Cancel();
            else
            {
                // 连续建造时金币可能花光，预览框会自动变红
                LastMessage = "";
            }
            return true;
        }

        /// <summary>拆掉建筑后踩实的泥土，要过几回合才长回草地。</summary>
        [Header("地面恢复")]
        public int restoreTurns = 3;

        [System.Serializable]
        public class GroundRestore
        {
            public int x;
            public int y;
            public int turnsLeft;
        }

        public List<GroundRestore> restoring = new List<GroundRestore>();

        void AddRestore(Vector2Int cell, int turns)
        {
            for (int k = 0; k < restoring.Count; k++)
            {
                if (restoring[k].x != cell.x || restoring[k].y != cell.y) continue;
                restoring[k].turnsLeft = turns;
                return;
            }
            restoring.Add(new GroundRestore { x = cell.x, y = cell.y, turnsLeft = turns });
        }

        /// <summary>每回合推进：泥土整块倒计时结束就变回草地。</summary>
        public void AdvanceTurn()
        {
            if (restoring.Count == 0) return;
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_map == null) return;

            for (int k = restoring.Count - 1; k >= 0; k--)
            {
                GroundRestore r = restoring[k];
                r.turnsLeft--;
                if (r.turnsLeft > 0) continue;

                var cell = new Vector2Int(r.x, r.y);
                if (FactoryWorld.Instance != null && FactoryWorld.Instance.OccupantAt(cell) != null)
                {
                    restoring.RemoveAt(k);      // 这一格又建了东西，就长不回去了
                    continue;
                }

                if (_map.TileAt(r.x, r.y) == "泥土整块")
                    _map.SetTileAt(r.x, r.y, "草地");

                restoring.RemoveAt(k);
            }
        }

        /// <summary>面板/提示里显示的名字：路面与渔船单独给个名字，其余优先用地块 id，没有就回退物体名。</summary>
        static string DisplayTileName(PlacedTile tile)
        {
            if (tile == null) return "";
            if (tile is RoadTile) return "路面";
            if (tile is PlayerBoatVehicle) return "渔船";
            return string.IsNullOrEmpty(tile.tileId) ? tile.name : tile.tileId;
        }

        /// <summary>拆除：退一半造价。地块（民居/农田/树林/巨石/碎石/聚集点等）不允许拆。</summary>
        public bool TryDemolish(Vector2Int cell)
        {
            if (FactoryWorld.Instance == null) return false;
            PlacedTile target = FactoryWorld.Instance.OccupantAt(cell);
            if (target == null)
            {
                LastMessage = "这一格没有可拆的";
                return false;
            }

            // 附属物（树林 / 民居 / 农田 / 巨石 / 碎石）：地形改造 Lv1 之后才拆得动
            bool prop = !target.CanDemolish;
            if (prop && !AbilityData.CanDemolishProps)
            {
                LastMessage = $"「{DisplayTileName(target)}」是地块，不能拆";
                return false;
            }

            // 地形改造 Lv2：拆建筑从退一半变成全额
            int refund = Mathf.RoundToInt(target.buildCost * AbilityData.DemolishRefundRate);
            if (refund > 0) PlayerProgress.AddGold(refund);

            // 拆路要把地面还原回草地（或原来是啥就还原成啥）
            if (target is RoadTile)
            {
                if (_map == null) _map = FindObjectOfType<IsoTileMap>();
                if (_map != null)
                {
                    string restore = string.IsNullOrEmpty(target.originalGround) ? "草地" : target.originalGround;
                    _map.SetTileAt(cell.x, cell.y, restore);
                }
            }
            else if (prop)
            {
                // 附属物拆掉后露出草地（农田/巨石/碎石是烘焙在地形瓦片上的）
                if (_map == null) _map = FindObjectOfType<IsoTileMap>();
                if (_map != null) _map.SetTileAt(cell.x, cell.y, "草地");
            }
            else
            {
                // 建筑 / 设施拆掉后，被翻过的泥土要过 3 回合才长回草地
                if (_map == null) _map = FindObjectOfType<IsoTileMap>();
                if (_map != null && _map.TileAt(cell.x, cell.y) == "泥土整块")
                    AddRestore(cell, restoreTurns);
            }

            FactoryWorld.Instance.Unregister(target);
            FactoryWorld.Instance.UnregisterRoad(cell);
            if (Application.isPlaying) Destroy(target.gameObject);
            else DestroyImmediate(target.gameObject);

            // 拆掉能源设施，电流束与供电要重新算
            if (target is ReceiverTower || target is PrismTower)
                PowerGridView.Instance?.NotifyWorldChanged();

            LastMessage = refund > 0 ? $"已拆除，退回 {refund} 金" : "已拆除";

            if (EngineeringView.Active && EngineeringView.Instance != null)
                EngineeringView.Instance.Refresh();

            return true;
        }

        // ---------------- 底部操作条（移动端也能用） ----------------

        void BuildActionBar()
        {
            if (_actionGroup != null) return;

            var canvasGo = new GameObject("BuildActionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5025;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _actionGroup = canvasGo.AddComponent<CanvasGroup>();

            // 用「建造菜单栏」那套像素按钮：取消 / 镜像（开、关两张图切换）
            var cancel = MakeImageButton(canvasGo.transform, "Cancel", "btn_cancel", 20f, 86f, Cancel);
            var mirror = MakeImageButton(canvasGo.transform, "Mirror",
                Mirrored ? "btn_mirror_on" : "btn_mirror_off", 114f, 86f, ToggleMirror);
            _mirrorImage = mirror.GetComponent<Image>();

            canvasGo.SetActive(false);
        }

        Button MakeImageButton(Transform parent, string name, string spriteName, float x, float w, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(UiPressScale));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            // 放左下角：右下角是操作按钮区，底部居中会和它们叠在一起
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(w, 76f);
            rect.anchoredPosition = new Vector2(x, 20f);

            var img = go.GetComponent<Image>();
            img.sprite = Resources.Load<Sprite>("Image/UI/工厂按钮/" + spriteName);
            img.preserveAspect = true;
            if (img.sprite == null) Debug.LogWarning("[建造] 缺少按钮素材 " + spriteName);

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        void SetActionBar(bool on)
        {
            if (_actionGroup != null) _actionGroup.gameObject.SetActive(on);
        }

        static float DefaultRate(FactoryType type)
        {
            switch (type)
            {
                case FactoryType.Preprocess: return 5f;
                case FactoryType.Forming: return 2f;
                case FactoryType.Canning: return 2f;
                case FactoryType.Sealing: return 1.2f;
                case FactoryType.Drying: return 1.6f;
                case FactoryType.Fermenting: return 1.5f;
                case FactoryType.Extraction: return 1.2f;
                case FactoryType.Freezing: return 1f;
                case FactoryType.Smoking: return 1f;
                case FactoryType.Frying: return 1f;
                default: return 1f;
            }
        }

        static float DefaultDailyCost(FactoryType type)
        {
            switch (type)
            {
                case FactoryType.Preprocess: return 2.5f;
                case FactoryType.Forming: return 3f;
                case FactoryType.Canning: return 3.6f;
                case FactoryType.Sealing: return 3.2f;
                case FactoryType.Drying: return 2.5f;
                case FactoryType.Fermenting: return 3f;
                case FactoryType.Extraction: return 4.5f;
                case FactoryType.Freezing: return 4f;
                case FactoryType.Smoking: return 2f;
                case FactoryType.Frying: return 2.5f;
                default: return 2.5f;
            }
        }
    }
}
