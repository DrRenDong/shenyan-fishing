using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 自动化玩法的「世界」：登记所有地块/建筑，并维护路面的连通性。
    /// 规则：工厂只和「相邻路面」相连，路面再连路面；同一片路面上的建筑之间物资瞬间转移（速度无限）。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class FactoryWorld : MonoBehaviour
    {
        public static FactoryWorld Instance { get; private set; }

        readonly Dictionary<Vector2Int, PlacedTile> _occupants = new Dictionary<Vector2Int, PlacedTile>();
        // 每个地块当前登记在哪个格子（船会移动，用它清旧位置）
        readonly Dictionary<PlacedTile, Vector2Int> _tileCells = new Dictionary<PlacedTile, Vector2Int>();
        readonly HashSet<Vector2Int> _roads = new HashSet<Vector2Int>();
        readonly Dictionary<Vector2Int, int> _roadComponent = new Dictionary<Vector2Int, int>();

        int _componentVersion = -1;
        bool _dirty = true;

        /// <summary>所有已登记的地块（含建筑与路面标记），key = 网格坐标。</summary>
        public IEnumerable<KeyValuePair<Vector2Int, PlacedTile>> Occupants => _occupants;

        void Awake()
        {
            Bind();
        }

        /// <summary>把自己设为当前世界（Awake 自动调用；编辑器离线验证时可以手动调用）。</summary>
        public void Bind()
        {
            Instance = this;
        }

        void Start()
        {
            // 兜底：把场景里所有地块登记一遍。
            // 有些对象（比如预置的海产聚集点）可能比 FactoryWorld 先 Start，
            // 那时 Instance 还是 null，注册就丢了 —— 结果它的计时也不走。
            var all = FindObjectsOfType<PlacedTile>(true);
            int n = 0;
            foreach (var t in all)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                Register(t);
                // 路面还要登记进路网：RoadTile.Start 如果比 FactoryWorld 早，注册会漏掉
                if (t is RoadTile) RegisterRoad(t.Cell);
                n++;
            }
            Debug.Log($"[工厂世界] 兜底登记 {n} 个地块");

            // 有工厂存档就先恢复（地形 / 建筑 / 库存 / 回合），会替换掉刚登记的这些。
            // 从主菜单再进工厂时进程没重启、SaveSystem.Load 不会再跑，这里自己从磁盘补一次。
            FactorySaveSystem.LoadFromSaveFileIfNeeded();
            FactorySaveSystem.TryApplyPending();

            // 第一次进工厂玩法时发启动资金（读档之后再发，才不会把恢复出来的金币盖掉）
            if (!SaveSystem.FactoryStarterGranted)
            {
                SaveSystem.FactoryStarterGranted = true;
                PlayerProgress.AddGold(PlayerProgress.StartingGold);
                SaveSystem.Save();
                Debug.Log($"[工厂] 发放初始资金 {PlayerProgress.StartingGold} 金（现有 {PlayerProgress.Gold}）");
            }

            // 注意：不要在这里删除「在水上的地块点击壳」——那些是玩家的民居/农田/树木，
            // 只是地形丢了才显得泡在水里。现在由 FactorySaveSystem.RepairPropGrounds() 给它们补地面。
        }

        readonly List<PlacedTile> _staleProps = new List<PlacedTile>();

        /// <summary>
        /// 清掉「地形已经变成水、但点击壳还留在原地」的地块（碎石 / 农田 / 巨石）。
        /// 典型情况：填海把那一格改成浅水，壳还在 → 看着是浅水、点上去却是碎石。
        /// 返回清理数量。
        /// </summary>
        public int RemovePropsOnWater()
        {
            var map = FindObjectOfType<IsoTileMap>();
            if (map == null) return 0;

            _staleProps.Clear();
            foreach (var kv in _occupants)
            {
                var t = kv.Value;
                if (t == null || !t.colliderOnly) continue;
                if (map.IsWaterAt(kv.Key.x, kv.Key.y)) _staleProps.Add(t);
            }

            for (int k = 0; k < _staleProps.Count; k++)
            {
                Unregister(_staleProps[k]);
                if (Application.isPlaying) Destroy(_staleProps[k].gameObject);
                else DestroyImmediate(_staleProps[k].gameObject);
            }
            int n = _staleProps.Count;
            _staleProps.Clear();
            return n;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(PlacedTile tile)
        {
            if (tile == null) return;

            Vector2Int cell = tile.Cell;

            // 会移动的地块（比如渔船）换格时，先把旧格子的登记清掉，
            // 否则每走过一格都会留下一个「这里还有东西」的幽灵占位，
            // 海面就不会往那些格子铺（表现为船走过以后底下空了）。
            if (_tileCells.TryGetValue(tile, out Vector2Int old) && old != cell)
            {
                if (_occupants.TryGetValue(old, out PlacedTile cur) && cur == tile) _occupants.Remove(old);
            }

            _tileCells[tile] = cell;

            // 这一格已经被别的东西占着就不要抢。
            // 典型情况：渔船从海产聚集点上经过，以前会把聚集点的登记挤掉，
            // 聚集点就再也收不到「时间流逝」，离散时间永远卡住。
            if (_occupants.TryGetValue(cell, out PlacedTile occupied) && occupied != null && occupied != tile)
                return;

            _occupants[cell] = tile;
        }

        /// <summary>注销某个地块（销毁/移走时调用）。</summary>
        public void Unregister(PlacedTile tile)
        {
            if (tile == null) return;
            if (_occupants.TryGetValue(tile.Cell, out PlacedTile cur) && cur == tile)
                _occupants.Remove(tile.Cell);

            // 顺带清掉可能残留的其它格子（移动过的地块）
            if (_tileCells.TryGetValue(tile, out Vector2Int old))
            {
                if (_occupants.TryGetValue(old, out PlacedTile cur2) && cur2 == tile) _occupants.Remove(old);
                _tileCells.Remove(tile);
            }
        }

        public void RegisterRoad(Vector2Int cell)
        {
            if (_roads.Add(cell)) _dirty = true;
        }

        /// <summary>注销路面（拆路时调用）。</summary>
        public void UnregisterRoad(Vector2Int cell)
        {
            if (_roads.Remove(cell)) _dirty = true;
        }

        /// <summary>该建筑四邻接里的路面格（小人从这儿出发/到达）。</summary>
        public bool TryGetRoadNeighbour(Vector2Int cell, out Vector2Int road)
        {
            foreach (var d in Neighbors)
            {
                if (_roads.Contains(cell + d)) { road = cell + d; return true; }
            }
            road = cell;
            return false;
        }

        /// <summary>只走路面的最短路径（小人搬运路线）。</summary>
        public List<Vector2Int> FindRoadPath(Vector2Int from, Vector2Int to)
        {
            if (!_roads.Contains(from) || !_roads.Contains(to)) return null;
            if (from == to) return new List<Vector2Int> { from };

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            cameFrom[from] = from;

            bool found = false;
            int guard = 0;
            while (queue.Count > 0 && !found && guard++ < 20000)
            {
                Vector2Int cur = queue.Dequeue();
                foreach (var d in Neighbors)
                {
                    Vector2Int n = cur + d;
                    if (cameFrom.ContainsKey(n)) continue;
                    if (!_roads.Contains(n)) continue;
                    cameFrom[n] = cur;
                    if (n == to) { found = true; break; }
                    queue.Enqueue(n);
                }
            }
            if (!found) return null;

            var path = new List<Vector2Int>();
            Vector2Int walk = to;
            while (walk != cameFrom[walk])
            {
                path.Add(walk);
                walk = cameFrom[walk];
            }
            path.Add(walk);
            path.Reverse();
            return path;
        }

        public PlacedTile OccupantAt(Vector2Int cell)
        {
            return _occupants.TryGetValue(cell, out PlacedTile t) ? t : null;
        }

        /// <summary>该地块周围 4 格里有没有路面；有则返回那片路面所在的连通块编号。</summary>
        public int RoadComponentOf(Vector2Int cell)
        {
            RebuildIfNeeded();
            int best = -1;
            foreach (var dir in Neighbors)
            {
                if (_roadComponent.TryGetValue(cell + dir, out int comp))
                {
                    if (best < 0) best = comp;
                }
            }
            return best;
        }

        static readonly Vector2Int[] Neighbors =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        void RebuildIfNeeded()
        {
            if (!_dirty) return;
            _dirty = false;
            _roadComponent.Clear();
            _componentVersion++;

            int comp = 0;
            var queue = new Queue<Vector2Int>();
            foreach (var start in _roads)
            {
                if (_roadComponent.ContainsKey(start)) continue;
                queue.Clear();
                queue.Enqueue(start);
                _roadComponent[start] = comp;
                while (queue.Count > 0)
                {
                    Vector2Int cur = queue.Dequeue();
                    foreach (var dir in Neighbors)
                    {
                        Vector2Int next = cur + dir;
                        if (_roads.Contains(next) && !_roadComponent.ContainsKey(next))
                        {
                            _roadComponent[next] = comp;
                            queue.Enqueue(next);
                        }
                    }
                }
                comp++;
            }
        }

        /// <summary>该建筑能访问到的所有仓库（同一片路面 + 紧贴着但不走路的仓库）。只此一层，不做中转。</summary>
        /// <summary>这一格是不是路面。</summary>
        public bool IsRoad(Vector2Int cell)
        {
            RebuildIfNeeded();
            return _roads.Contains(cell);
        }

        /// <summary>这一格相邻的所有道路组件（一栋建筑可能同时贴着两条互不相通的路）。</summary>
        public HashSet<int> RoadComponentsOf(Vector2Int cell)
        {
            RebuildIfNeeded();
            var set = new HashSet<int>();
            foreach (var dir in Neighbors)
                if (_roadComponent.TryGetValue(cell + dir, out int comp)) set.Add(comp);
            return set;
        }

        public List<WarehouseBuilding> WarehousesConnectedTo(PlacedTile tile, List<WarehouseBuilding> buffer)
        {
            buffer.Clear();

            // 用「所有相邻组件」而不是第一个：
            // 比如仓库左邻 (4,10) 一块路、右邻 (6,10) 另一块路，
            // 以前只取先遍历到的那块，导致另一侧的工作场所永远连不上它。
            HashSet<int> myComps = RoadComponentsOf(tile.Cell);

            foreach (var kv in _occupants)
            {
                if (!(kv.Value is WarehouseBuilding wh)) continue;
                if (IsAdjacent(tile.Cell, kv.Key)) { buffer.Add(wh); continue; }   // 直接贴着也算连上
                if (myComps.Count == 0) continue;

                foreach (int comp in RoadComponentsOf(kv.Key))
                {
                    if (!myComps.Contains(comp)) continue;
                    buffer.Add(wh);
                    break;
                }
            }
            return buffer;
        }

        static bool IsAdjacent(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return dx + dy == 1;
        }

        /// <summary>
        /// 拉货来源：路网上的仓库 + 紧贴着（不走路面）的仓库 + 紧贴着的另一个工厂的成品暂存。
        /// 注意只认「直接相邻」这一层，所以 A—B—C 里 A 拿不到 C 的东西，不会变成中转站。
        /// </summary>
        public void CollectSources(PlacedTile tile, List<Inventory> buffer)
        {
            buffer.Clear();
            WarehousesConnectedTo(tile, _warehouseBuffer);
            for (int k = 0; k < _warehouseBuffer.Count; k++)
                buffer.Add(_warehouseBuffer[k].storage);

            foreach (var kv in _occupants)
            {
                if (kv.Value == tile) continue;
                if (!IsAdjacent(tile.Cell, kv.Key)) continue;
                if (kv.Value is FactoryBuilding f && !buffer.Contains(f.outputBuffer))
                    buffer.Add(f.outputBuffer);
            }
        }

        readonly List<WarehouseBuilding> _warehouseBuffer = new List<WarehouseBuilding>();

        /// <summary>统计这批来源里某物资的总量（只读，用来估算这一回合能做多少，不能当作授权）。</summary>
        public float AvailableSources(List<Inventory> sources, string itemId)
        {
            float sum = 0f;
            for (int k = 0; k < sources.Count; k++) sum += sources[k].AmountOf(itemId);
            return sum;
        }

        /// <summary>统计这批仓库里某物资的总量（只读）。</summary>
        public float AvailableWarehouse(List<WarehouseBuilding> warehouses, string itemId)
        {
            float sum = 0f;
            for (int k = 0; k < warehouses.Count; k++) sum += warehouses[k].storage.AmountOf(itemId);
            return sum;
        }

        /// <summary>
        /// 在这些来源里找「满足条件、总量最多」的那种物品（动态主料用：一回合只加工一个品种）。
        /// 找不到返回 null。
        /// </summary>
        public string FindBestSource(List<Inventory> sources, System.Func<string, bool> match, out float amount)
        {
            amount = 0f;
            string best = null;
            if (sources == null || match == null) return null;

            var seen = new HashSet<string>();
            for (int k = 0; k < sources.Count; k++)
            {
                var all = sources[k].All;
                for (int a = 0; a < all.Count; a++)
                {
                    string id = all[a].ItemId;
                    if (all[a].Amount <= 1e-5f) continue;
                    if (!seen.Add(id)) continue;
                    if (!match(id)) continue;

                    float total = AvailableSources(sources, id);
                    if (total > amount + 1e-5f)
                    {
                        amount = total;
                        best = id;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// 从来源里扣 itemId（原子操作，返回真正扣到的量——多个工厂同回合抢同一份存货也不会扣成负数）。
        /// </summary>
        public float TakeFromSources(List<Inventory> sources, string itemId, float amount, out float quality)
        {
            quality = 0f;
            if (amount <= 0f) return 0f;

            float remaining = amount;
            float weighted = 0f;
            float takenTotal = 0f;
            for (int k = 0; k < sources.Count && remaining > 0f; k++)
            {
                float taken = sources[k].Take(itemId, remaining, out float q);
                if (taken <= 0f) continue;
                weighted += q * taken;
                takenTotal += taken;
                remaining -= taken;
            }
            if (takenTotal > 0f) quality = weighted / takenTotal;
            return takenTotal;
        }

        /// <summary>从仓库里扣 itemId（兼容旧接口）。</summary>
        public float TakeFromWarehouses(List<WarehouseBuilding> warehouses, string itemId, float amount, out float quality)
        {
            quality = 0f;
            if (amount <= 0f) return 0f;

            float remaining = amount;
            float weighted = 0f;
            float takenTotal = 0f;
            for (int k = 0; k < warehouses.Count && remaining > 0f; k++)
            {
                float taken = warehouses[k].storage.Take(itemId, remaining, out float q);
                if (taken <= 0f) continue;
                weighted += q * taken;
                takenTotal += taken;
                remaining -= taken;
            }
            if (takenTotal > 0f) quality = weighted / takenTotal;
            return takenTotal;
        }

        /// <summary>把产物塞进相连的仓库；没仓库就返回 0，由工厂自己暂存。</summary>
        public float AddToWarehouses(List<WarehouseBuilding> warehouses, string itemId, float amount, float quality)
        {
            if (amount <= 0f || warehouses.Count == 0) return 0f;
            // 先进第一个仓库，仓库不限容量，所以整批都能放下
            warehouses[0].storage.Add(itemId, amount, quality);
            return amount;
        }
    }
}
