using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 海底的沉船 / 宝藏。
    /// - 新游戏时随机生成：36 个沉船 + 若干个宝藏，坐标范围 ±coordRange，避开初始岛；
    /// - 玩家视野（神眼 5×5）覆盖到才把地块实例化出来，之前只是坐标；
    /// - 生成结果与「是否已捞取」跟着存档走，读档后位置不变；
    /// - 有沉船/宝藏的格子不会再生成海产聚集点。
    /// </summary>
    public class WreckSiteSystem : MonoBehaviour
    {
        public static WreckSiteSystem Instance { get; private set; }

        [System.Serializable]
        public class Site
        {
            public int i;
            public int j;
            public int kind;        // 0 = 沉船，1 = 宝藏
            public bool salvaged;   // 已经捞过（一次性）
            public string loot;     // 沉船里装的东西（贵重品 id，空 = 没东西）
        }

        [Header("生成")]
        public int wreckCount = 36;
        [Tooltip("关掉 treasureIsRandom 时就预生成这么多个宝藏")]
        public int treasureCount = 60;
        public int coordRange = 500;
        [Tooltip("初始岛外扩多少格内不生成")]
        public int islandSafe = 8;
        [Tooltip("两个点之间至少隔几格（切比雪夫）")]
        public int minGap = 3;

        [Header("宝藏（不限个数）")]
        [Tooltip("开视野时每格刷出宝藏的概率（极低）")]
        public float treasureChancePerCell = 0.0012f;
        [Tooltip("宝藏改成「走到哪都有极低概率刷」，不再预生成固定数量、也不限 ±500")]
        public bool treasureIsRandom = true;

        [Header("宝藏奖励（只给钱）")]
        [Tooltip("打捞一个宝箱获得的随机金币下限")]
        public int treasureGoldMin = 1200;
        [Tooltip("打捞一个宝箱获得的随机金币上限")]
        public int treasureGoldMax = 1800;

        /// <summary>所有埋点（静态：跟着存档走，跨场景保留）。</summary>
        public static readonly List<Site> Sites = new List<Site>();

        static readonly Dictionary<Vector2Int, Site> Index = new Dictionary<Vector2Int, Site>();

        readonly HashSet<Vector2Int> _spawned = new HashSet<Vector2Int>();
        readonly HashSet<Vector2Int> _rolled = new HashSet<Vector2Int>();
        IsoTileMap _map;
        PlayerBoatVehicle _boat;
        SeaStreamer _streamer;

        public int SiteCount => Sites.Count;

        /// <summary>这一格是不是沉船/宝藏（聚集点要避开）。</summary>
        public static bool HasSite(Vector2Int cell)
        {
            return Index.ContainsKey(cell);
        }

        public static Site GetSite(Vector2Int cell)
        {
            return Index.TryGetValue(cell, out Site s) ? s : null;
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            EnsureGenerated();
            RevealInView();
        }

        // ---------------- 生成 ----------------

        void EnsureGenerated()
        {
            RebuildIndex();
            if (Sites.Count > 0)
            {
                AssignLootToEmpty();   // 旧存档：给还没分配宝物的沉船补上
                return;
            }

            var map = FindObjectOfType<IsoTileMap>();

            int guard = 0;
            int made = 0;
            while (made < wreckCount && guard++ < 20000)
            {
                if (!TryPickCell(map, out Vector2Int c, 0)) continue;
                Sites.Add(new Site { i = c.x, j = c.y, kind = 0 });
                Index[c] = Sites[Sites.Count - 1];
                made++;
            }

            guard = 0;
            made = 0;
            int treasureTarget = treasureIsRandom ? 0 : treasureCount;
            while (made < treasureTarget && guard++ < 40000)
            {
                if (!TryPickCell(map, out Vector2Int c, 1)) continue;
                Sites.Add(new Site { i = c.x, j = c.y, kind = 1 });
                Index[c] = Sites[Sites.Count - 1];
                made++;
            }

            AssignLoot();

            Debug.Log($"[沉船] 生成完成：沉船 {wreckCount} 个（范围 ±{coordRange}）" +
                      (treasureIsRandom ? "，宝藏改为开视野随机刷出（不限个数）" : $"，宝藏 {treasureTarget} 个"));
        }

        static readonly string[] TreasureKinds =
        {
            ItemDatabase.SouthSeaPearl, ItemDatabase.MeloPearl, ItemDatabase.Ambergris,
        };

        /// <summary>给所有沉船随机分配宝物（36 个沉船时三种各 12 个）。</summary>
        static void AssignLoot()
        {
            var wrecks = new List<Site>();
            foreach (var s in Sites)
                if (s.kind == 0) wrecks.Add(s);
            if (wrecks.Count == 0) return;

            var pool = new List<string>(wrecks.Count);
            for (int k = 0; k < wrecks.Count; k++)
                pool.Add(TreasureKinds[k % TreasureKinds.Length]);

            for (int k = pool.Count - 1; k > 0; k--)
            {
                int r = Random.Range(0, k + 1);
                var tmp = pool[k];
                pool[k] = pool[r];
                pool[r] = tmp;
            }

            for (int k = 0; k < wrecks.Count; k++) wrecks[k].loot = pool[k];
        }

        /// <summary>旧存档兼容：给还没 loot 的沉船按「缺得最多的先补」，尽量保持三种均衡。
        /// 宝藏（宝箱）也一并补——以前宝藏根本没有分配过东西，捞上来永远是空的。</summary>
        static void AssignLootToEmpty()
        {
            // 宝藏（宝箱）：只给金币。36 件贵重品是 36 条沉船的，宝箱里不装。
            foreach (var s in Sites)
                if (s.kind == 1 && !string.IsNullOrEmpty(s.loot)) s.loot = "";

            var missing = new List<Site>();
            foreach (var s in Sites)
                if (s.kind == 0 && string.IsNullOrEmpty(s.loot)) missing.Add(s);
            if (missing.Count == 0) return;

            var counts = new int[TreasureKinds.Length];
            foreach (var s in Sites)
                if (s.kind == 0 && !string.IsNullOrEmpty(s.loot))
                    for (int k = 0; k < TreasureKinds.Length; k++)
                        if (s.loot == TreasureKinds[k]) counts[k]++;

            foreach (var s in missing)
            {
                int best = 0;
                for (int k = 1; k < TreasureKinds.Length; k++)
                    if (counts[k] < counts[best]) best = k;
                s.loot = TreasureKinds[best];
                counts[best]++;
            }
        }

        bool TryPickCell(IsoTileMap map, out Vector2Int cell, int kind)
        {
            cell = new Vector2Int(
                Random.Range(-coordRange, coordRange + 1),
                Random.Range(-coordRange, coordRange + 1));

            // 不要落在初始岛（含安全边距）上
            if (map != null)
            {
                if (cell.x >= map.MinI - islandSafe && cell.x <= map.MaxI + islandSafe &&
                    cell.y >= map.MinJ - islandSafe && cell.y <= map.MaxJ + islandSafe)
                    return false;
            }

            // 不要和已有的点挨太近
            foreach (var s in Sites)
            {
                if (Mathf.Max(Mathf.Abs(s.i - cell.x), Mathf.Abs(s.j - cell.y)) < minGap) return false;
            }

            return true;
        }

        void RebuildIndex()
        {
            Index.Clear();
            foreach (var s in Sites)
                Index[new Vector2Int(s.i, s.j)] = s;
        }

        // ---------------- 视野揭露 ----------------

        /// <summary>把落在神眼视野里的沉船/宝藏显形，顺便掷骰看看要不要刷新宝藏。返回这次显形/刷出的数量。</summary>
        public int RevealInView()
        {
            if (_boat == null) _boat = FindObjectOfType<PlayerBoatVehicle>();
            if (_boat == null) return 0;
            if (_streamer == null) _streamer = FindObjectOfType<SeaStreamer>();
            int radius = _streamer != null ? _streamer.viewRadius : 2;
            int count = 0;

            // 0) 已显形的点：按视野调明暗（视野 = 船周围 + 陆地外扩，和铺海面同一套判断）
            foreach (var t in FindObjectsOfType<WreckSiteTile>(true))
            {
                if (t == null) continue;
                bool inView = _streamer != null
                    ? _streamer.IsInVision(t.Cell)
                    : Mathf.Max(Mathf.Abs(t.gridX - _boat.gridX), Mathf.Abs(t.gridY - _boat.gridY)) <= radius;
                t.SetInView(inView);
            }

            // 1) 已有的沉船 / 宝藏：进视野就显形
            for (int k = 0; k < Sites.Count; k++)
            {
                Site s = Sites[k];
                if (s.salvaged) continue;

                var c = new Vector2Int(s.i, s.j);
                if (_spawned.Contains(c)) continue;

                bool inView = _streamer != null
                    ? _streamer.IsInVision(c)
                    : Mathf.Max(Mathf.Abs(c.x - _boat.gridX), Mathf.Abs(c.y - _boat.gridY)) <= radius;
                if (!inView) continue;

                Spawn(s, c);
                count++;
            }

            // 2) 宝藏：走到没掷过骰子的格子时，按极低概率刷一个新的（不限个数、不限距离）
            if (!treasureIsRandom) return count;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var c = new Vector2Int(_boat.gridX + dx, _boat.gridY + dy);
                    if (!_rolled.Add(c)) continue;              // 这一格已经掷过骰
                    if (HasSite(c)) continue;                   // 有沉船/宝藏了
                    if (Random.value > treasureChancePerCell) continue;

                    var site = new Site { i = c.x, j = c.y, kind = 1 };
                    Sites.Add(site);
                    Index[c] = site;
                    Spawn(site, c);
                    count++;
                    Debug.Log($"[沉船] 开视野刷出宝藏（{c.x},{c.y}）");
                }
            }

            return count;
        }

        void Spawn(Site s, Vector2Int cell)
        {
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();

            var go = new GameObject($"{(s.kind == 1 ? "宝藏" : "沉船")}_{cell.x}_{cell.y}");
            go.transform.SetParent(transform, false);

            var tile = go.AddComponent<WreckSiteTile>();
            tile.gridX = cell.x;
            tile.gridY = cell.y;
            tile.tileId = s.kind == 1 ? "宝藏" : "沉船";
            tile.isTreasure = s.kind == 1;
            tile.salvaged = s.salvaged;

            tile.EnsureVisual();
            if (FactoryWorld.Instance != null) FactoryWorld.Instance.Register(tile);

            _spawned.Add(cell);
            Debug.Log($"[沉船] 神眼发现 {(s.kind == 1 ? "宝藏" : "沉船")}（{cell.x},{cell.y}）");
        }

        // ---------------- 捞取 ----------------

        /// <summary>最近一次打捞宝箱拿到的金币（沉船为 0），给捕鱼界的提示用。</summary>
        public static int LastSalvageGold { get; private set; }

        /// <summary>标记某个点已经捞过（一次性），地块随之隐藏。</summary>
        public bool Salvage(Vector2Int cell)
        {
            LastSalvageGold = 0;

            Site s = GetSite(cell);
            if (s == null || s.salvaged) return false;

            s.salvaged = true;

            // 每捞一条沉船给一个国宝：用来标记已经捞了几条船，集齐解锁授勋漫画
            if (s.kind == 0)
            {
                PlayerProgress.AddTreasure(ItemDatabase.NationalTreasure, 1);
                Debug.Log($"[沉船] 国宝 ×{PlayerProgress.TreasureCount(ItemDatabase.NationalTreasure)}");

                // 沉船里的宝物：直接进玩家的贵重品库存（不占重量，以后作升级材料）
                if (!string.IsNullOrEmpty(s.loot))
                {
                    PlayerProgress.AddTreasure(s.loot, 1);
                    Debug.Log($"[沉船] 打捞到「{s.loot}」（{cell.x},{cell.y}）");
                }
            }
            else
            {
                // 宝藏（宝箱）：只给钱，贵重品是沉船专属的
                int gold = Random.Range(treasureGoldMin, treasureGoldMax + 1);
                LastSalvageGold = gold;
                PlayerProgress.AddGold(gold);
                Debug.Log($"[宝藏] 打捞到金币 {gold}（{cell.x},{cell.y}）");
            }

            PlacedTile occ = FactoryWorld.Instance != null ? FactoryWorld.Instance.OccupantAt(cell) : null;
            if (occ is WreckSiteTile tile)
            {
                tile.salvaged = true;
                if (FactoryWorld.Instance != null) FactoryWorld.Instance.Unregister(tile);
                tile.gameObject.SetActive(false);
                Destroy(tile.gameObject);
            }

            _spawned.Remove(cell);
            return true;
        }

        // ---------------- 存档 ----------------

        public static List<Site> SnapshotSites()
        {
            var list = new List<Site>(Sites.Count);
            foreach (var s in Sites)
                list.Add(new Site { i = s.i, j = s.j, kind = s.kind, salvaged = s.salvaged, loot = s.loot });
            return list;
        }

        public static void ApplySites(List<Site> saved)
        {
            // 先拷一份：传进来的可能就是 Sites 本身，直接遍历会边清空边读
            var copy = saved == null ? null : new List<Site>(saved);

            Sites.Clear();
            Index.Clear();
            if (copy == null) return;

            foreach (var s in copy)
            {
                if (s == null) continue;
                Sites.Add(s);
                Index[new Vector2Int(s.i, s.j)] = s;
            }
            Debug.Log($"[沉船] 读档恢复 {Sites.Count} 个海底点位");
        }
    }
}
