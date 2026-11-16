using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 海产聚集点（区块制）：
    /// 世界按 chunkSize × chunkSize 切成无限个区块，**每个区块最多一个聚集点**；
    /// 只有玩家视野覆盖到的区块才启动刷新（在区块内随机挑一个非内陆湖的水体格）；
    /// 聚集点过期后先消失，等这块区域再次进入视野时才重新刷 —— 期间不会补。
    /// 区块里没有水体、或者水体全是内陆湖的，永远不刷。
    /// </summary>
    public class SeaSpotSpawner : MonoBehaviour
    {
        [Header("区块")]
        public int chunkSize = 16;
        [Tooltip("每回合在每个区块里最多试几格")]
        public int triesPerChunk = 12;
        [Tooltip("为了性能，每回合每个区块最多做几次「内陆湖」判定")]
        public int inlandChecksPerChunk = 4;

        [Header("聚集点属性")]
        public string spotTileId = "聚集点";
        public int minAmountKg = 200;
        public int maxAmountKg = 6000;
        public int minLifeTurns = 24;
        public int maxLifeTurns = 96;

        readonly Dictionary<Vector2Int, SeaSpotBuilding> _live = new Dictionary<Vector2Int, SeaSpotBuilding>();
        readonly HashSet<Vector2Int> _chunks = new HashSet<Vector2Int>();

        IsoTileMap _map;
        PlayerBoatVehicle _boat;
        SeaStreamer _streamer;

        public int LiveCount
        {
            get
            {
                int n = 0;
                foreach (var s in _live.Values)
                    if (s != null && s.RemainingHours > 0f) n++;      // 视野外的点是隐藏的，但仍算活着
                return n;
            }
        }

        void Awake()
        {
            _map = FindObjectOfType<IsoTileMap>();
        }

        void Start()
        {
            // 读档恢复出来的聚集点要保留：登记进 _live（同一区块不会重复刷），
            // 以前这里会把它们当「旧版本的点」全删掉，结果读档后海里的聚集点全没了、
            // 港口自然也指定不了。
            RebindExisting();
            AdvanceTurn();
        }

        /// <summary>每回合：把视野覆盖到的区块检查一遍，缺点的补一个。</summary>
        public void AdvanceTurn()
        {
            if (_boat == null) _boat = FindObjectOfType<PlayerBoatVehicle>();
            if (_streamer == null) _streamer = FindObjectOfType<SeaStreamer>();
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_boat == null || _map == null) return;

            int radius = _streamer != null ? _streamer.viewRadius : 2;
            int halo = _streamer != null ? _streamer.landHalo : 5;

            _chunks.Clear();

            // 1) 船周围
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    _chunks.Add(ChunkOf(new Vector2Int(_boat.gridX + dx, _boat.gridY + dy)));
                }
            }

            // 2) 每一块陆地外扩 landHalo 格：这片也算视野，对应区块要一直保持活跃，
            //    否则站在岛边看，海里的聚集点永远不刷新。
            for (int j = _map.MinJ; j <= _map.MaxJ; j++)
            {
                for (int i = _map.MinI; i <= _map.MaxI; i++)
                {
                    string ground = _map.TileAt(i, j);
                    if (string.IsNullOrEmpty(ground)) continue;
                    if (_map.IsWaterAt(i, j)) continue;      // 只从陆地往外算

                    for (int dx = -halo; dx <= halo; dx++)
                    {
                        for (int dy = -halo; dy <= halo; dy++)
                        {
                            _chunks.Add(ChunkOf(new Vector2Int(i + dx, j + dy)));
                        }
                    }
                }
            }

            foreach (var chunk in _chunks) EnsureChunk(chunk);

            UpdateVisibility();
        }

        void UpdateVisibility()
        {
            foreach (var kv in _live)
            {
                SeaSpotBuilding s = kv.Value;
                if (s == null) continue;

                // 视野 = 船周围 + 陆地外扩（和 SeaStreamer 铺海面用同一套判断）
                bool inView = _streamer != null
                    ? _streamer.IsInVision(s.Cell)
                    : Mathf.Max(Mathf.Abs(s.gridX - _boat.gridX), Mathf.Abs(s.gridY - _boat.gridY)) <= 2;

                // 兜底：如果登记被别人挤掉了（比如船路过同一格），补回来。
                // 没登记的话 FactorySim 就收不到这个点，离散时间会永远卡住。
                if (FactoryWorld.Instance != null && FactoryWorld.Instance.OccupantAt(s.Cell) != s)
                    FactoryWorld.Instance.Register(s);

                // 看见过就一直显示：船开走了也留在图上，别让玩家刚发现的东西又凭空消失
                if (inView) s.revealed = true;

                bool visible = inView || s.revealed;
                if (s.gameObject.activeSelf != visible) s.gameObject.SetActive(visible);

                // 视野外的点压暗（半透明黑滤镜的感觉），一眼能分出「我现在看得到」和「之前看过」
                SpriteRenderer sr = s.Renderer;
                if (sr != null)
                    sr.color = inView ? Color.white : new Color(0.42f, 0.42f, 0.42f, 1f);
            }
        }

        /// <summary>保留给旧调用方（刷新时就直接显形了，这里不用做事）。</summary>
        public int RevealInView() => 0;

        // ---------------- 区块 ----------------

        public Vector2Int ChunkOf(Vector2Int cell)
        {
            return new Vector2Int(
                Mathf.FloorToInt(cell.x / (float)chunkSize),
                Mathf.FloorToInt(cell.y / (float)chunkSize));
        }

        void EnsureChunk(Vector2Int chunk)
        {
            // 已经有活着的点就不动。
            // 注意不能看 activeSelf：视野外的点是隐藏状态，但它还在计时。
            if (_live.TryGetValue(chunk, out SeaSpotBuilding cur))
            {
                if (cur != null && cur.RemainingHours > 0f) return;
                _live.Remove(chunk);
            }

            int x0 = chunk.x * chunkSize;
            int y0 = chunk.y * chunkSize;
            int inlandChecks = 0;

            for (int t = 0; t < triesPerChunk; t++)
            {
                var cell = new Vector2Int(x0 + Random.Range(0, chunkSize), y0 + Random.Range(0, chunkSize));

                // 有地形的地方：必须是水、而且不能是内陆湖；
                // 没有地形的地方（地图外的开放海域）直接算可刷。
                string ground = _map.TileAt(cell.x, cell.y);
                if (!string.IsNullOrEmpty(ground))
                {
                    if (!_map.IsWaterAt(cell.x, cell.y)) continue;
                    if (inlandChecks < inlandChecksPerChunk)
                    {
                        inlandChecks++;
                        if (_map.IsEnclosedWater(cell.x, cell.y)) continue;
                    }
                }

                if (FactoryWorld.Instance != null && FactoryWorld.Instance.OccupantAt(cell) != null) continue;
                if (WreckSiteSystem.HasSite(cell)) continue;

                SpawnSpot(cell, chunk);
                return;
            }
        }

        void SpawnSpot(Vector2Int cell, Vector2Int chunk)
        {
            var go = new GameObject($"SeaSpot_{cell.x}_{cell.y}");
            go.transform.SetParent(transform, false);
            go.SetActive(false);      // 先别触发 Awake/OnEnable，等字段填好

            var spot = go.AddComponent<SeaSpotBuilding>();
            spot.gridX = cell.x;
            spot.gridY = cell.y;
            spot.tileId = spotTileId;
            spot.species = FishDatabase.RandomByLevel(2);
            spot.quality = Random.Range(0.45f, 0.95f);
            spot.amountKg = Random.Range(minAmountKg, maxAmountKg + 1);
            spot.lifeHours = Random.Range(minLifeTurns, maxLifeTurns + 1) * GameClock.HoursPerTurn;
            spot.ageHours = 0f;

            go.SetActive(true);
            spot.EnsureVisual();
            if (FactoryWorld.Instance != null) FactoryWorld.Instance.Register(spot);

            _live[chunk] = spot;

            // 落在视野外就先藏起来（区块比视野大，随机点很可能在视野之外）；
            // 视野内生成的就直接算「已看见」，之后一直显示。
            bool inView = _streamer != null
                ? _streamer.IsInVision(cell)
                : (_boat != null && Mathf.Max(Mathf.Abs(cell.x - _boat.gridX), Mathf.Abs(cell.y - _boat.gridY)) <= 2);
            spot.revealed = inView;
            if (!inView) go.SetActive(false);

            Debug.Log($"[聚集点] 区块({chunk.x},{chunk.y}) 刷新：{spot.species}（{cell.x},{cell.y}）" +
                      $"品质{spot.quality:P0} {spot.amountKg:F0}KG");
        }

        // ---------------- 调试 ----------------

        /// <summary>调试用：立刻给某个区块刷一个点（忽略视野）。</summary>
        public bool ForceSpawnChunk(Vector2Int chunk)
        {
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            _live.Remove(chunk);
            EnsureChunk(chunk);
            return _live.ContainsKey(chunk);
        }

        /// <summary>调试用：清掉所有活着的聚集点（含视野外隐藏的）。</summary>
        public void ClearAll()
        {
            foreach (var s in FindObjectsOfType<SeaSpotBuilding>(true))
                if (s != null) Destroy(s.gameObject);
            _live.Clear();
        }

        /// <summary>读档后把场景里已有的聚集点重新登记到区块表，避免同一区块再刷一个。</summary>
        public void RebindExisting()
        {
            _live.Clear();
            foreach (var s in FindObjectsOfType<SeaSpotBuilding>(true))
            {
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                _live[ChunkOf(s.Cell)] = s;
            }
        }
    }
}
