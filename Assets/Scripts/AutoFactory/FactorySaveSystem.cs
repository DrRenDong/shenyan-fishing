using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>一个地块的存档记录（按类型只填需要的字段）。</summary>
    [System.Serializable]
    public class PlacedTileSave
    {
        public string type;          // road / warehouse / factory / port / dock / sorter / receiver / prism / spot / boat / tile
        public int i, j;
        public int level = 1;
        public int buildCost;
        public string tileId;
        public string originalGround;
        public bool colliderOnly;
        public bool mirrored;
        public int buildTurnsLeft;

        // 工厂
        public int factoryType;
        public string recipeId;
        public string selectedPrimary;
        public float ratePerTurn;
        public bool powered;
        public bool running;

        // 仓库 / 工厂暂存
        public List<Stock> stocks = new List<Stock>();

        // 码头
        public float dailyCost;
        public float throughput;
        public bool autoSupply;
        public bool autoSell;
        public List<string> buyCatalog = new List<string>();
        public List<DockOrder> orders = new List<DockOrder>();

        // 捕捞港
        public bool hasTarget;
        public int targetX, targetY;

        // 分拣区
        public float junkPerTurn;
        public float outputPerTurn;
        public float quality;

        // 海产聚集点
        public string species;
        public float spotQuality;
        public float amountKg;
        public float ageHours;
        public float lifeHours;
        public bool revealed;

        // 渔船
        public List<PlayerBoatVehicle.CargoSlot> cargo = new List<PlayerBoatVehicle.CargoSlot>();
        public float bodyScale;
        public Vector2 bodyOffset;
    }

    /// <summary>工厂玩法的整份存档。</summary>
    [System.Serializable]
    public class FactorySaveData
    {
        public bool valid;
        public string[] layout;
        public int layoutMinI;
        public int layoutMinJ;
        public int turn = 1;
        public int landCompleted;
        public int bossIndex;
        public List<PlacedTileSave> tiles = new List<PlacedTileSave>();

        // 电力束（当前落在哪一行 / 哪一列，以及还要几回合重掷）
        public int powerRow = -1;
        public int powerCol = -1;
        public int powerTurns;

        // 王老板限时订单
        public bool hasRush;
        public string rushTitle = "";
        public string rushBrief = "";
        public string rushItem = "";
        public float rushAmount;
        public int rushCreatedTurn;
    }

    /// <summary>
    /// 工厂玩法的存档：地形 + 回合 + 所有地块（建筑/库存/订单/船/聚集点）+ 土地局/订单进度。
    /// 存的时候抓一份，读档时先挂起，等工厂场景加载好再重建。
    /// </summary>
    public static class FactorySaveSystem
    {
        static FactorySaveData _pending;
        static FactorySaveData _cached;

        /// <summary>最近一次抓到的工厂数据（在主菜单保存时用它，避免把工厂数据写空）。</summary>
        public static FactorySaveData Cached => _cached;

        /// <summary>抓一份当前工厂状态；不在工厂场景（没有 FactoryWorld）时返回 null。</summary>
        public static FactorySaveData Capture()
        {
            var world = FactoryWorld.Instance;
            var map = UnityEngine.Object.FindObjectOfType<IsoTileMap>();
            var turn = TurnManager.Instance;
            if (world == null || map == null || turn == null) return null;

            map.SnapshotToLayout();
            var d = new FactorySaveData
            {
                valid = true,
                layout = map.LayoutSnapshot,
                layoutMinI = map.LayoutMinI,
                layoutMinJ = map.LayoutMinJ,
                turn = turn.Turn,
            };

            var land = LandOffice.Instance;
            if (land != null) d.landCompleted = land.CompletedCount;
            var boss = BossOrderSystem.Instance;
            if (boss != null) d.bossIndex = boss.Index;

            foreach (var kv in world.Occupants)
            {
                var tile = kv.Value;
                if (tile == null) continue;
                // 沉船 / 宝藏不写进地块表：它们由 WreckSiteSystem.Sites 管理，
                // 写进来会在读档时多复活一个「捞过还在」的壳。
                if (tile is WreckSiteTile) continue;
                d.tiles.Add(CaptureTile(tile));
            }

            var power = PowerGridView.Instance;
            if (power != null)
            {
                d.powerRow = power.horizontalRow;
                d.powerCol = power.verticalColumn;
                d.powerTurns = power.turnsSinceRefresh;
            }

            var rush = BossRushSystem.Instance;
            if (rush != null && rush.current != null)
            {
                d.hasRush = true;
                d.rushTitle = rush.current.title;
                d.rushBrief = rush.current.brief;
                d.rushItem = rush.current.itemId;
                d.rushAmount = rush.current.amount;
                d.rushCreatedTurn = rush.current.createdTurn;
            }

            _cached = d;
            return d;
        }

        static PlacedTileSave CaptureTile(PlacedTile t)
        {
            var s = new PlacedTileSave
            {
                i = t.gridX,
                j = t.gridY,
                level = t.level,
                buildCost = t.buildCost,
                tileId = t.tileId,
                originalGround = t.originalGround,
                colliderOnly = t.colliderOnly,
                mirrored = t.mirrored,
                buildTurnsLeft = t.BuildTurnsLeft,
            };

            switch (t)
            {
                case PlayerBoatVehicle boat:
                    s.type = "boat";
                    s.cargo = new List<PlayerBoatVehicle.CargoSlot>(boat.cargo);
                    s.bodyScale = boat.bodyScale;
                    s.bodyOffset = boat.bodyOffset;
                    break;

                case RoadTile _:
                    s.type = "road";
                    break;

                case WarehouseBuilding wh:
                    s.type = "warehouse";
                    CopyStocks(wh.storage, s.stocks);
                    break;

                case FactoryBuilding f:
                    s.type = "factory";
                    s.factoryType = (int)f.factoryType;
                    s.recipeId = f.recipeId;
                    s.selectedPrimary = f.selectedPrimary;
                    s.ratePerTurn = f.ratePerTurn;
                    s.dailyCost = f.dailyCost;
                    s.powered = f.powered;
                    s.running = f.running;
                    CopyStocks(f.outputBuffer, s.stocks);
                    break;

                case FishingPortBuilding p:
                    s.type = "port";
                    s.dailyCost = p.dailyCost;
                    s.powered = p.powered;
                    s.running = p.running;
                    s.hasTarget = p.HasTarget;
                    if (p.HasTarget) { s.targetX = p.targetCell.x; s.targetY = p.targetCell.y; }
                    break;

                case CargoDockBuilding c:
                    s.type = "dock";
                    s.dailyCost = c.dailyCost;
                    s.throughput = c.throughputPerTurn;
                    s.powered = c.powered;
                    s.autoSupply = c.autoSupply;
                    s.autoSell = c.autoSell;
                    s.buyCatalog = new List<string>(c.buyCatalog);
                    s.orders = new List<DockOrder>(c.orders);
                    break;

                case SortingFacility so:
                    s.type = "sorter";
                    s.dailyCost = so.dailyCost;
                    s.junkPerTurn = so.junkPerTurn;
                    s.outputPerTurn = so.outputPerTurn;
                    s.quality = so.quality;
                    s.running = so.running;
                    break;

                case ReceiverTower _:
                    s.type = "receiver";
                    break;

                case PrismTower _:
                    s.type = "prism";
                    break;

                case SeaSpotBuilding sp:
                    s.type = "spot";
                    s.species = sp.species;
                    s.spotQuality = sp.quality;
                    s.amountKg = sp.amountKg;
                    s.ageHours = sp.ageHours;
                    s.lifeHours = sp.lifeHours;
                    s.revealed = sp.revealed;
                    break;

                case WreckSiteTile _:
                    s.type = "wreck";
                    break;

                default:
                    s.type = "tile";
                    break;
            }
            return s;
        }

        static void CopyStocks(Inventory inv, List<Stock> target)
        {
            if (inv == null || target == null) return;
            foreach (var st in inv.All)
            {
                if (st == null || st.Amount <= 0f) continue;
                target.Add(new Stock(st.ItemId, st.Amount, st.Quality));
            }
        }

        /// <summary>读档时先把数据挂起来，等工厂场景起来再 Apply。</summary>
        public static void StorePending(FactorySaveData data)
        {
            if (data == null || !data.valid) return;
            _pending = data;
            _cached = data;
        }

        public static bool HasPending => _pending != null && _pending.valid;

        /// <summary>
        /// 没有挂起的存档时，直接从磁盘把存档里的工厂部分读出来挂上。
        /// 用途：从工厂退回主菜单、再点海神号进工厂时，进程没有重启，
        /// SaveSystem.Load() 不会重跑，_pending 是空的 → 地图会退回场景默认（看起来"存档没读上、地图被重置"）。
        /// </summary>
        public static void LoadFromSaveFileIfNeeded()
        {
            if (_pending != null) return;
            try
            {
                if (!System.IO.File.Exists(SaveSystem.SavePath)) return;
                var d = JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(SaveSystem.SavePath));
                if (d != null) StorePending(d.factory);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[工厂存档] 重新读取工厂存档失败: " + e.Message);
            }
        }

        /// <summary>工厂场景是否已经把「待应用的存档」处理完（没存档也算处理完）。
        /// 新手教程要等它，否则会把读档重建出来的建筑当成玩家刚盖的。</summary>
        public static bool Applied { get; private set; }

        /// <summary>工厂场景启动后调用：重建地形、地块、回合与进度。</summary>
        public static bool TryApplyPending()
        {
            Applied = true;
            if (_pending == null || !_pending.valid) return false;
            var data = _pending;
            _pending = null;
            Apply(data);
            return true;
        }

        static void Apply(FactorySaveData d)
        {
            var world = FactoryWorld.Instance;
            var map = UnityEngine.Object.FindObjectOfType<IsoTileMap>();
            if (world == null || map == null) return;

            // 1) 地形
            if (d.layout != null && d.layout.Length > 0)
                map.ReplaceLayout(d.layout, d.layoutMinI, d.layoutMinJ);

            // 2) 清掉场景里现有的地块（船 / 聚集点 / 沉船等），并把占用表清干净
            //    例外：存档里没有渔船时保留场景自带的那条船（否则读档后玩家没船可开）
            bool hasBoatInSave = false;
            for (int k = 0; k < d.tiles.Count; k++)
                if (d.tiles[k] != null && d.tiles[k].type == "boat") { hasBoatInSave = true; break; }

            PlayerBoatVehicle keepBoat = hasBoatInSave
                ? null
                : UnityEngine.Object.FindObjectOfType<PlayerBoatVehicle>();

            var olds = UnityEngine.Object.FindObjectsOfType<PlacedTile>(true);
            foreach (var t in olds)
            {
                if (t == null) continue;
                if (t == keepBoat) continue;
                world.Unregister(t);
                world.UnregisterRoad(t.Cell);
                UnityEngine.Object.Destroy(t.gameObject);
            }
            if (keepBoat != null) world.Register(keepBoat);

            // 3) 重建
            foreach (var s in d.tiles)
            {
                if (s == null) continue;
                // 沉船 / 宝藏由 WreckSiteSystem 按 Sites 重新显形，存档里不要再复活一份
                if (s.type == "wreck") continue;
                if (s.tileId == "沉船" || s.tileId == "宝藏") continue;
                Spawn(s, world);
            }

            // 兜底：清掉「已经捞过、但地块还在」的沉船/宝藏壳
            var wrecks = UnityEngine.Object.FindObjectsOfType<WreckSiteTile>(true);
            for (int k = 0; k < wrecks.Length; k++)
            {
                var wr = wrecks[k];
                if (wr == null) continue;
                var site = WreckSiteSystem.GetSite(new Vector2Int(wr.gridX, wr.gridY));
                if (site != null && !site.salvaged) continue;
                world.Unregister(wr);
                UnityEngine.Object.Destroy(wr.gameObject);
            }

            // 修复旧档：附属物（民居 / 树林 / 农田 / 巨石 / 碎石）下面没有地形 →
            // 补回对应地面，避免读档后「房子树浮在海面上」。
            int repaired = RepairPropGrounds(map);
            if (repaired > 0) Debug.Log($"[工厂存档] 修补 {repaired} 格缺失的地面（附属物不再浮空）");

            // 陆地外缘补一圈浅海：地图边缘的空格不算「水体」，港口就建不上去、也看不到浅海
            int coast = BuildCoastRing(map);
            if (coast > 0) Debug.Log($"[工厂存档] 陆地外缘补浅海 {coast} 格");

            // 4) 回合 / 土地局 / 王老板订单
            if (TurnManager.Instance != null) TurnManager.Instance.SetTurn(Mathf.Max(1, d.turn));
            var land = LandOffice.Instance;
            if (land != null) land.SetCompletedCount(d.landCompleted);
            var boss = BossOrderSystem.Instance;
            if (boss != null) boss.SetIndex(d.bossIndex);

            var power = PowerGridView.Instance;
            if (power != null && d.powerRow >= 0 && d.powerCol >= 0)
            {
                power.horizontalRow = d.powerRow;
                power.verticalColumn = d.powerCol;
                power.turnsSinceRefresh = d.powerTurns;
                power.NotifyWorldChanged();     // 按存下来的行列重算，不重掷
            }

            var rush = BossRushSystem.Instance;
            if (rush != null)
            {
                rush.current = d.hasRush
                    ? new BossRushSystem.RushOrder
                    {
                        title = d.rushTitle,
                        brief = d.rushBrief,
                        itemId = d.rushItem,
                        amount = d.rushAmount,
                        createdTurn = d.rushCreatedTurn,
                    }
                    : null;
            }

            var spawner = UnityEngine.Object.FindObjectOfType<SeaSpotSpawner>();
            if (spawner != null) spawner.RebindExisting();

            // 船是重新建出来的：把海面流式视野重新绑到新船，并立刻按新位置铺一次视野。
            // 不重绑的话，船读档后在远海就完全没有视野（只有陆地外扩那圈海面）。
            var streamer = UnityEngine.Object.FindObjectOfType<SeaStreamer>();
            if (streamer != null)
            {
                streamer.boat = UnityEngine.Object.FindObjectOfType<PlayerBoatVehicle>();
                streamer.Refresh();
            }

            Debug.Log($"[工厂存档] 已恢复：回合 {d.turn}，地块 {d.tiles.Count} 个");
        }

        /// <summary>
        /// 附属物（民居 / 树林 / 农田 / 巨石 / 碎石）所在格如果地形为空或是水，补一块对应地面。
        /// 老存档里 layout 快照丢过地形（负坐标/快照失败），读档后就只剩房子漂在海面上。
        /// </summary>
        static int RepairPropGrounds(IsoTileMap map)
        {
            if (map == null || FactoryWorld.Instance == null) return 0;
            int n = 0;
            foreach (var kv in FactoryWorld.Instance.Occupants)
            {
                var t = kv.Value;
                if (t == null) continue;

                string cur = map.TileAt(kv.Key.x, kv.Key.y);
                bool hasLand = !string.IsNullOrEmpty(cur) && !map.IsWaterAt(kv.Key.x, kv.Key.y);

                // 路面：地面必须铺成石板路，否则「数据里有路、画面看不见、又建不了」
                if (t is RoadTile)
                {
                    if (cur == "石砖路") continue;
                    map.SetTileAt(kv.Key.x, kv.Key.y, "石砖路");
                    n++;
                    continue;
                }

                // 陆地上的设施（工厂/仓库/分拣区/两座塔）地面丢了 → 补泥土整块
                string ground = GroundForTile(t);
                if (ground == null || hasLand) continue;
                map.SetTileAt(kv.Key.x, kv.Key.y, ground);
                n++;
            }
            return n;
        }

        /// <summary>
        /// 陆地外缘补一圈浅海：地图边界的空格没有地形数据，`IsWaterAt` 返回 false → 港口建不上去，
        /// 视觉上也缺一圈浅海。这里把「紧挨着陆地的空格」改成浅水（已有的地形不动）。
        /// </summary>
        static int BuildCoastRing(IsoTileMap map)
        {
            if (map == null) return 0;

            var ring = new List<Vector2Int>();
            for (int j = map.MinJ; j <= map.MaxJ; j++)
            {
                for (int i = map.MinI; i <= map.MaxI; i++)
                {
                    string g = map.TileAt(i, j);
                    if (string.IsNullOrEmpty(g) || map.IsWaterAt(i, j)) continue;   // 只从陆地往外算

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int x = i + dx, y = j + dy;
                            if (!string.IsNullOrEmpty(map.TileAt(x, y))) continue;    // 已经有地形的跳过
                            var c = new Vector2Int(x, y);
                            if (!ring.Contains(c)) ring.Add(c);
                        }
                    }
                }
            }

            for (int k = 0; k < ring.Count; k++) map.SetTileAt(ring[k].x, ring[k].y, "浅水");
            return ring.Count;
        }

        static string GroundForTile(PlacedTile t)        {
            if (t is FactoryBuilding || t is WarehouseBuilding || t is SortingFacility
                || t is ReceiverTower || t is PrismTower)
                return "泥土整块";
            return GroundForProp(t.tileId);
        }

        static string GroundForProp(string tileId)
        {
            switch (tileId)
            {
                case "单层民居":
                case "两层民居":
                case "木板棚屋":
                case "树林":
                    return "草地";
                case "农田": return "农田";
                case "巨石": return "巨石";
                case "碎石": return "碎石";
                default: return null;
            }
        }

        static void Spawn(PlacedTileSave s, FactoryWorld world)
        {
            var go = new GameObject($"{s.type}_{s.i}_{s.j}");
            go.transform.SetParent(world.transform, false);
            go.transform.localPosition = PlacedTile.CellToWorld(s.i, s.j);

            PlacedTile t;
            switch (s.type)
            {
                case "road":
                    var road = go.AddComponent<RoadTile>();
                    road.originalGround = s.originalGround;
                    t = road;
                    break;

                case "warehouse":
                    var wh = go.AddComponent<WarehouseBuilding>();
                    FillInventory(wh.storage, s.stocks);
                    t = wh;
                    break;

                case "factory":
                    var f = go.AddComponent<FactoryBuilding>();
                    f.factoryType = (FactoryType)s.factoryType;
                    f.recipeId = s.recipeId;
                    f.selectedPrimary = s.selectedPrimary ?? "";
                    f.ratePerTurn = s.ratePerTurn;
                    f.dailyCost = s.dailyCost;
                    f.powered = s.powered;
                    f.running = s.running;
                    FillInventory(f.outputBuffer, s.stocks);
                    t = f;
                    break;

                case "port":
                    var p = go.AddComponent<FishingPortBuilding>();
                    p.dailyCost = s.dailyCost;
                    p.powered = s.powered;
                    p.running = s.running;
                    if (s.hasTarget) p.SetTarget(new Vector2Int(s.targetX, s.targetY));
                    t = p;
                    break;

                case "dock":
                    var c = go.AddComponent<CargoDockBuilding>();
                    c.dailyCost = s.dailyCost;
                    c.throughputPerTurn = s.throughput;
                    c.powered = s.powered;
                    c.autoSupply = s.autoSupply;
                    c.autoSell = s.autoSell;
                    if (s.buyCatalog != null && s.buyCatalog.Count > 0) c.buyCatalog = s.buyCatalog.ToArray();
                    c.orders = new List<DockOrder>(s.orders);
                    t = c;
                    break;

                case "sorter":
                    var so = go.AddComponent<SortingFacility>();
                    so.dailyCost = s.dailyCost;
                    so.junkPerTurn = s.junkPerTurn;
                    so.outputPerTurn = s.outputPerTurn;
                    so.quality = s.quality;
                    so.running = s.running;
                    t = so;
                    break;

                case "receiver":
                    t = go.AddComponent<ReceiverTower>();
                    break;

                case "prism":
                    t = go.AddComponent<PrismTower>();
                    break;

                case "spot":
                    var sp = go.AddComponent<SeaSpotBuilding>();
                    sp.species = s.species;
                    sp.quality = s.spotQuality;
                    sp.amountKg = s.amountKg;
                    sp.ageHours = s.ageHours;
                    sp.lifeHours = s.lifeHours;
                    sp.revealed = s.revealed;
                    t = sp;
                    break;

                case "boat":
                    var boat = go.AddComponent<PlayerBoatVehicle>();
                    if (s.cargo != null) boat.cargo = new List<PlayerBoatVehicle.CargoSlot>(s.cargo);
                    if (s.bodyScale > 0.01f) boat.bodyScale = s.bodyScale;
                    if (s.bodyOffset.sqrMagnitude > 0.0001f) boat.bodyOffset = s.bodyOffset;
                    t = boat;
                    break;

                default:
                    t = go.AddComponent<PlacedTile>();
                    break;
            }

            t.gridX = s.i;
            t.gridY = s.j;
            t.tileId = s.tileId;
            t.level = Mathf.Clamp(s.level, 1, PlacedTile.MaxLevel);
            t.buildCost = s.buildCost;
            t.colliderOnly = s.colliderOnly;

            t.EnsureVisual();
            if (s.mirrored) t.SetMirrored(true);            // 建的时候按过 R：精灵和点击轮廓一起翻回来
            if (s.buildTurnsLeft > 0) t.StartConstruction(s.buildTurnsLeft);
            world.Register(t);
            if (t is RoadTile) world.RegisterRoad(t.Cell);
        }

        static void FillInventory(Inventory inv, List<Stock> stocks)
        {
            if (inv == null || stocks == null) return;
            foreach (var st in stocks)
                if (st != null && !string.IsNullOrEmpty(st.ItemId) && st.Amount > 0f)
                    inv.Add(st.ItemId, st.Amount, st.Quality);
        }
    }
}
