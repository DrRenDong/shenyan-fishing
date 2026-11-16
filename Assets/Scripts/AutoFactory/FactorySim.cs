using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>游戏时间：1 回合 = 1 游戏日（24 小时）。</summary>
    public static class GameClock
    {
        public const float SecondsPerDay = 24f * 60f;
        public const float SecondsPerHour = SecondsPerDay / 24f;
        public const float HoursPerTurn = 24f;

        public static float TotalSeconds { get; private set; }
        public static float Hours => TotalSeconds / SecondsPerHour;
        public static float Days => TotalSeconds / SecondsPerDay;

        public static void Advance(float dt) => TotalSeconds += Mathf.Max(0f, dt);
        public static void AdvanceHours(float hours) => Advance(hours * SecondsPerHour);
        public static void Reset() => TotalSeconds = 0f;
    }

    /// <summary>
    /// 每一拍处理所有工厂：按固定顺序（等距斜列 + 坐标）逐个结算，
    /// 每个工厂都从仓库「先查量、再原子扣减」，返回多少就用多少。
    /// 这样多个灌装厂在同帧抢最后一个空罐头时，后一个只会拿到 0，不会把库存扣成负数。
    /// </summary>
    public class FactorySim : MonoBehaviour
    {
        public static FactorySim Instance { get; private set; }

        [Tooltip("低于这个产量（个）就不开工，避免浮点尾数一直扣库存")]
        public float minBatch = 0.01f;

        public float FishPerCan => ItemDatabase.FishPerCan;

        public int TickCount { get; private set; }
        public float TotalCansProduced { get; private set; }
        public float TotalFishConsumed { get; private set; }
        public float TotalContainersConsumed { get; private set; }
        public float TotalFishCaught { get; private set; }
        public int NegativeInventoryHits { get; private set; }

        readonly List<FactoryBuilding> _factories = new List<FactoryBuilding>();
        readonly List<FishingPortBuilding> _ports = new List<FishingPortBuilding>();
        readonly List<SortingFacility> _sorters = new List<SortingFacility>();
        readonly List<WarehouseBuilding> _warehouses = new List<WarehouseBuilding>();
        readonly List<Inventory> _sources = new List<Inventory>();

        float[] _avail = new float[4];
        float[] _takes = new float[4];
        float[] _quals = new float[4];
        int[] _order = new int[4];

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>推进一个回合：所有工厂、码头、捕捞港各走一步，时间前进 1 游戏小时。</summary>
        public void AdvanceTurn()
        {
            if (FactoryWorld.Instance == null) return;
            TickCount++;
            GameClock.AdvanceHours(GameClock.HoursPerTurn);

            // 施工进度：本回合刚完工的，下一回合才开始生产
            foreach (var kv in FactoryWorld.Instance.Occupants)
                if (kv.Value != null && kv.Value.UnderConstruction) kv.Value.AdvanceConstruction();

            CollectBuildings();
            for (int k = 0; k < _factories.Count; k++)
                TickFactory(_factories[k]);

            for (int k = 0; k < _ports.Count; k++)
            {
                var warehouses = FactoryWorld.Instance.WarehousesConnectedTo(_ports[k], _warehouses);
                _ports[k].BeginTurn();                       // 先清「本回合有没有捞到」
                float caught = _ports[k].Tick(warehouses);
                if (caught > 0f) TotalFishCaught += caught;
            }

            // 分拣区：吃杂鱼、吐随机鱼（不需要通电）
            for (int k = 0; k < _sorters.Count; k++)
            {
                var warehouses = FactoryWorld.Instance.WarehousesConnectedTo(_sorters[k], _warehouses);
                _sorters[k].Tick(warehouses);
            }

            ChargeDailyCosts();
            AdvanceSeaSpots(GameClock.HoursPerTurn);
        }

        [Tooltip("两次派小人之间的最短/最长间隔（秒）")]
        public float workerMinInterval = 0.8f;
        public float workerMaxInterval = 1.8f;

        float _workerTimer = 1f;
        readonly List<WarehouseBuilding> _workerBuffer = new List<WarehouseBuilding>();
        readonly List<FactoryBuilding> _suppliedBuffer = new List<FactoryBuilding>();
        readonly List<FactoryBuilding> _producedBuffer = new List<FactoryBuilding>();

        void Update()
        {
            if (FactoryWorld.Instance == null) return;

            // 静置时也一直有搬运工，只要「上一回合真的有供货」的厂才会派人
            _workerTimer -= Time.unscaledDeltaTime;
            if (_workerTimer > 0f) return;
            _workerTimer = Random.Range(workerMinInterval, Mathf.Max(workerMinInterval, workerMaxInterval));
            SpawnOneWorker();
        }

        readonly List<PlacedTile> _workplaces = new List<PlacedTile>();
        readonly List<bool> _wpCanIn = new List<bool>();
        readonly List<bool> _wpCanOut = new List<bool>();

        /// <summary>登记一个工作场所：canIn = 本回合真的从仓库领了料，canOut = 本回合真的有货产出。</summary>
        void AddWorkplace(PlacedTile tile, bool canIn, bool canOut)
        {
            _workplaces.Add(tile);
            _wpCanIn.Add(canIn);
            _wpCanOut.Add(canOut);
        }

        void SpawnOneWorker()
        {
            // 工程模式是看图纸，不派小人（已经在路上的会在进入时被隐藏）
            if (EngineeringView.Active) return;

            if (_factories.Count == 0) CollectBuildings();

            // 工人在「仓库 ↔ 各个工作场所」之间跑，但只在**本回合真的有货物流转**时才派，
            // 而且方向要跟着物流走：工厂领料才从仓库出发，产出/捕捞/成交只往仓库送。
            _workplaces.Clear();
            _wpCanIn.Clear();
            _wpCanOut.Clear();
            for (int k = 0; k < _factories.Count; k++)
            {
                var f = _factories[k];
                if (f.SuppliedLastTurn || f.ProducedLastTurn)
                    AddWorkplace(f, f.SuppliedLastTurn, f.ProducedLastTurn);
            }
            for (int k = 0; k < _sorters.Count; k++)
                if (_sorters[k] != null && _sorters[k].LastOutput > 0f) AddWorkplace(_sorters[k], false, true);
            for (int k = 0; k < _ports.Count; k++)
                if (_ports[k] != null && _ports[k].CaughtThisTurn) AddWorkplace(_ports[k], false, true);
            foreach (var d in FindObjectsOfType<CargoDockBuilding>())
                if (d != null && d.TradedThisTurn) AddWorkplace(d, false, true);

            if (_workplaces.Count == 0) return;

            int pick = Random.Range(0, _workplaces.Count);
            PlacedTile target = _workplaces[pick];
            bool canIn = _wpCanIn[pick];
            bool canOut = _wpCanOut[pick];
            if (!canIn && !canOut) return;
            if (!FactoryWorld.Instance.TryGetRoadNeighbour(target.Cell, out Vector2Int targetRoad)) return;

            FactoryWorld.Instance.WarehousesConnectedTo(target, _workerBuffer);
            if (_workerBuffer.Count == 0) return;

            WarehouseBuilding wh = _workerBuffer[Random.Range(0, _workerBuffer.Count)];

            // 仓库可能同时挨着两条互不相通的路（比如左边一条、右边一条），
            // 要挑一条真的能走到工作场所的路面，否则 FindRoadPath 会返回 null、小人就出不来了。
            Vector2Int whRoad = default;
            bool found = false;
            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };
            foreach (var d in dirs)
            {
                Vector2Int cand = wh.Cell + d;
                if (!FactoryWorld.Instance.IsRoad(cand)) continue;
                if (FactoryWorld.Instance.FindRoadPath(targetRoad, cand) == null) continue;
                whRoad = cand;
                found = true;
                break;
            }
            if (!found) return;

            // 工厂供货/产出都有就随机挑一程；捕捞港 / 分拣区 / 货运码头本回合只有货流出，
            // 只派「工作场所 → 仓库」这一程，不会再出现空仓库给港口送货的怪画面。
            bool goOut;
            if (canIn && canOut) goOut = Random.value < 0.5f;
            else goOut = canOut;
            if (goOut) SpawnWorkerPath(target.Cell, targetRoad, whRoad, wh.Cell);
            else SpawnWorkerPath(wh.Cell, whRoad, targetRoad, target.Cell);
        }

        /// <summary>沿路面把小人从 fromCell 搬到 toCell（首尾各补一格建筑格，看起来像进出楼）。</summary>
        void SpawnWorkerPath(Vector2Int fromCell, Vector2Int fromRoad, Vector2Int toRoad, Vector2Int toCell)
        {
            List<Vector2Int> roadPath = FactoryWorld.Instance.FindRoadPath(fromRoad, toRoad);
            if (roadPath == null) return;

            var path = new List<Vector2Int>(roadPath.Count + 2);
            path.Add(fromCell);
            path.AddRange(roadPath);
            if (path[path.Count - 1] != toCell) path.Add(toCell);

            RoadWorker.Spawn(FactoryWorld.Instance.transform, path, Random.Range(1, 5));
        }

        /// <summary>工厂日运营费（含电费/人工，公用工程都折在这里）：一回合 = 一日，所以每回合扣一次。</summary>
        void ChargeDailyCosts()
        {
            for (int k = 0; k < _factories.Count; k++)
            {
                FactoryBuilding f = _factories[k];
                if (!f.powered || !f.running) continue;
                if (f.LastTickStalled) continue;   // 没原料自然停机，不扣运营费
                int cost = Mathf.RoundToInt(f.dailyCost * LandmarkBonus.OperatingCostMultiplier(f.Cell) * f.LevelCostMultiplier);
                if (cost <= 0) continue;
                PlayerProgress.AddGold(-cost);
                TotalOperatingCost += cost;
            }

            // 捕捞港：员工与船只维护
            for (int k = 0; k < _ports.Count; k++)
            {
                FishingPortBuilding p = _ports[k];
                if (!p.powered || !p.running || p.dailyCost <= 0f) continue;   // 停工不收费
                int cost = Mathf.RoundToInt(p.dailyCost * p.LevelCostMultiplier);
                if (cost <= 0) continue;
                PlayerProgress.AddGold(-cost);
                TotalOperatingCost += cost;
            }

            // 分拣区：员工工资 + 设施维护（没杂鱼也照收；停工不扣）
            for (int k = 0; k < _sorters.Count; k++)
            {
                SortingFacility s = _sorters[k];
                if (s == null || s.dailyCost <= 0f) continue;
                if (!s.running) continue;
                int cost = Mathf.RoundToInt(s.dailyCost * s.LevelCostMultiplier);
                if (cost <= 0) continue;
                PlayerProgress.AddGold(-cost);
                TotalOperatingCost += cost;
            }
        }

        public int TotalOperatingCost { get; private set; }

        void CollectBuildings()
        {
            _factories.Clear();
            _ports.Clear();
            _sorters.Clear();
            foreach (var kv in FactoryWorld.Instance.Occupants)
            {
                if (kv.Value == null || kv.Value.UnderConstruction) continue;   // 施工中不产不捞
                if (kv.Value is FactoryBuilding f) _factories.Add(f);
                else if (kv.Value is FishingPortBuilding p) _ports.Add(p);
                else if (kv.Value is SortingFacility s) _sorters.Add(s);
            }

            // 固定顺序：先按等距斜列 (i+j)，再按 i —— 保证同帧内谁先拿存货是确定的
            _factories.Sort((a, b) =>
            {
                int da = a.gridX + a.gridY, db = b.gridX + b.gridY;
                if (da != db) return da.CompareTo(db);
                return a.gridX.CompareTo(b.gridX);
            });
        }

        void TickFactory(FactoryBuilding factory)
        {
            factory.MarkProduced(false);      // 本回合有没有产出，决定要不要派「取货工」

            if (!factory.running)             // 玩家手动停工：不生产、也不扣日费
            {
                factory.MarkStalled(false);
                return;
            }

            if (!factory.powered)
            {
                factory.MarkStalled(true);
                return;
            }

            // 要电的厂没供上电：直接停工（也当作"自然停机"，不扣日运营费）
            if (!factory.HasPower)
            {
                factory.MarkStalled(true);
                return;
            }

            List<WarehouseBuilding> warehouses =
                FactoryWorld.Instance.WarehousesConnectedTo(factory, _warehouses);

            Recipe recipe = factory.CurrentRecipe;

            // 批次型（灌装 / 发酵 / 萃取）：攒够一整批才开工，做满几回合再一次性出货
            if (recipe.IsBatch)
            {
                TickBatch(factory, recipe, warehouses);
                return;
            }

            // 先收集来源：动态配方要从存货里挑一种鱼来加工
            FactoryWorld.Instance.CollectSources(factory, _sources);
            if (_sources.Count == 0)
            {
                factory.MarkStalled(true);
                return;
            }

            // 主料口 + 辅料/包材口 摊平成两张表
            _items.Clear();
            _per.Clear();

            string dynamicMain = null;
            if (recipe.DynamicPrimary)
            {
                dynamicMain = PickDynamicPrimary(factory, recipe, _sources);
                if (dynamicMain == null)
                {
                    factory.MarkStalled(true);
                    return;
                }
                _items.Add(dynamicMain);
                _per.Add(recipe.DynamicPrimaryPerUnit);
            }

            for (int k = 0; k < recipe.PrimaryItems.Length; k++)
            {
                _items.Add(recipe.PrimaryItems[k]);
                _per.Add(recipe.PrimaryPerUnit[k]);
            }
            for (int k = 0; k < recipe.AuxItems.Length; k++)
            {
                _items.Add(recipe.AuxItems[k]);
                _per.Add(recipe.AuxPerUnit[k] * LandmarkBonus.AuxiliaryMultiplier(factory.Cell));
            }

            int n = _items.Count;
            if (_avail.Length < n)
            {
                _avail = new float[n];
                _takes = new float[n];
                _quals = new float[n];
                _order = new int[n];
            }

            // 1) 每个输入能支持几份，取最小
            float runs = factory.ratePerTurn * LandmarkBonus.ProductionMultiplier(factory.Cell) * factory.LevelSpeedMultiplier;
            for (int k = 0; k < n; k++)
            {
                float per = _per[k];
                float avail = FactoryWorld.Instance.AvailableSources(_sources, _items[k]);
                _avail[k] = per > 0f ? avail / per : float.MaxValue;
                runs = Mathf.Min(runs, _avail[k]);
                _order[k] = k;
            }

            if (runs < minBatch)
            {
                factory.MarkStalled(true);
                return;
            }

            // 2) 紧缺的输入先扣
            for (int a = 1; a < n; a++)
            {
                int key = _order[a];
                int b = a - 1;
                while (b >= 0 && _avail[_order[b]] > _avail[key])
                {
                    _order[b + 1] = _order[b];
                    b--;
                }
                _order[b + 1] = key;
            }

            float limited = runs;
            for (int a = 0; a < n; a++)
            {
                int k = _order[a];
                float per = _per[k];
                if (per <= 0f)
                {
                    _takes[k] = 0f;
                    _quals[k] = 1f;
                    continue;
                }

                float want = limited * per;
                _takes[k] = FactoryWorld.Instance.TakeFromSources(
                    _sources, _items[k], want, out float q);
                _quals[k] = q;
                limited = Mathf.Min(limited, _takes[k] / per);
            }

            if (limited < minBatch)
            {
                RefundAll(limited);
                factory.MarkStalled(true);
                return;
            }

            // 3) 多扣的按品质加权退回
            RefundAll(limited);

            float produced = limited * recipe.OutputPerUnit;
            float quality = Mathf.Clamp01((n > 0 ? _quals[0] : 1f) + LandmarkBonus.QualityBonus(factory.Cell));
            string productId = recipe.DynamicPrimary
                ? RecipeDatabase.ProductFor(recipe, dynamicMain)
                : recipe.ProductId;
            float pushed = FactoryWorld.Instance.AddToWarehouses(
                warehouses, productId, produced, quality);
            if (pushed < produced - 1e-5f)
                factory.outputBuffer.Add(productId, produced - pushed, quality);

            // 副产物口（橙）：有几个就吐几个，一样进仓库
            for (int b = 0; b < recipe.ByproductIds.Length; b++)
            {
                float byAmount = limited * recipe.ByproductPerUnit[b];
                if (byAmount <= 0f) continue;
                float byPushed = FactoryWorld.Instance.AddToWarehouses(
                    warehouses, recipe.ByproductIds[b], byAmount, quality);
                if (byPushed < byAmount - 1e-5f)
                    factory.outputBuffer.Add(recipe.ByproductIds[b], byAmount - byPushed, quality);
            }

            factory.ReportConsumed(n > 0 ? _takes[0] : 0f, n > 1 ? _takes[1] : 0f);
            factory.MarkSupplied(limited > 0f);
            factory.ReportProduced(produced, quality);
            factory.MarkStalled(false);

            TotalCansProduced += produced;
            TotalFishConsumed += n > 0 ? _takes[0] : 0f;
            TotalContainersConsumed += n > 1 ? _takes[1] : 0f;

            GuardAgainstNegative(warehouses, factory);
        }

        readonly List<string> _items = new List<string>();
        readonly List<float> _per = new List<float>();

        /// <summary>
        /// 批次型节奏：空闲时攒一整批（速率 × 回合数的量）→ 期间断料 → 做满 BatchTurns 回合 → 一次性出货。
        /// 这样同样的配方，灌装/发酵/萃取是「大批次慢节奏」，和连续厂混在一起才有物流问题。
        /// </summary>
        void TickBatch(FactoryBuilding factory, Recipe recipe, List<WarehouseBuilding> warehouses)
        {
            // 正在生产：只走回合数，不碰原料
            if (factory.IsBatchRunning)
            {
                factory.AdvanceBatchTurn();
                if (factory.BatchTurnsLeft > 0)
                {
                    factory.MarkStalled(false);
                    return;
                }

                float copies = factory.BatchCopies;
                float quality = factory.BatchQuality;
                string primary = factory.BatchPrimaryId;
                factory.EndBatch();
                string productId = recipe.DynamicPrimary && !string.IsNullOrEmpty(primary)
                    ? RecipeDatabase.ProductFor(recipe, primary)
                    : recipe.ProductId;
                PushOutputs(factory, recipe, productId, copies, quality, warehouses);
                factory.ReportProduced(recipe.OutputPerUnit * copies, quality);
                factory.MarkStalled(false);
                return;
            }

            // 空闲：先攒够一整批
            FactoryWorld.Instance.CollectSources(factory, _sources);
            if (_sources.Count == 0)
            {
                factory.MarkStalled(true);
                return;
            }

            _items.Clear();
            _per.Clear();

            string dynamicMain = null;
            if (recipe.DynamicPrimary)
            {
                dynamicMain = PickDynamicPrimary(factory, recipe, _sources);
                if (dynamicMain == null)
                {
                    factory.MarkStalled(true);
                    return;
                }
                _items.Add(dynamicMain);
                _per.Add(recipe.DynamicPrimaryPerUnit);
            }

            for (int k = 0; k < recipe.PrimaryItems.Length; k++)
            {
                _items.Add(recipe.PrimaryItems[k]);
                _per.Add(recipe.PrimaryPerUnit[k]);
            }
            for (int k = 0; k < recipe.AuxItems.Length; k++)
            {
                _items.Add(recipe.AuxItems[k]);
                _per.Add(recipe.AuxPerUnit[k] * LandmarkBonus.AuxiliaryMultiplier(factory.Cell));
            }

            int n = _items.Count;
            if (_avail.Length < n)
            {
                _avail = new float[n];
                _takes = new float[n];
                _quals = new float[n];
                _order = new int[n];
            }

            float wantCopies = Mathf.Max(minBatch, factory.ratePerTurn * LandmarkBonus.ProductionMultiplier(factory.Cell) * factory.LevelSpeedMultiplier * recipe.BatchTurns);

            // 先看现有原料够做多少份
            float best = wantCopies;
            for (int k = 0; k < n; k++)
            {
                float per = _per[k];
                if (per <= 0f) continue;
                float avail = FactoryWorld.Instance.AvailableSources(_sources, _items[k]);
                best = Mathf.Min(best, avail / per);
            }

            // 不够一整批就开「小批」：只要够 minBatch 就做，不然工厂永远等在那里
            // （等级/加成把一整批推到 20 多份时，玩家手里几份料就完全开不了工）
            float batchCopies = Mathf.Min(wantCopies, best);
            if (batchCopies < minBatch - 1e-4f)
            {
                factory.MarkStalled(true);
                return;
            }

            // 一次性把整批原料扣下来
            float limited = batchCopies;
            for (int k = 0; k < n; k++)
            {
                float per = _per[k];
                if (per <= 0f) { _takes[k] = 0f; _quals[k] = 1f; continue; }
                _takes[k] = FactoryWorld.Instance.TakeFromSources(
                    _sources, _items[k], limited * per, out float q);
                _quals[k] = q;
                limited = Mathf.Min(limited, _takes[k] / per);
            }

            if (limited < batchCopies - 1e-4f)
            {
                RefundAll(limited);
                factory.MarkStalled(true);
                return;
            }

            RefundAll(limited);   // 理论上没有多余，留着保险
            factory.BeginBatch(limited, Mathf.Clamp01((n > 0 ? _quals[0] : 1f) + LandmarkBonus.QualityBonus(factory.Cell)), recipe.BatchTurns, dynamicMain);
            factory.MarkSupplied(true);
            factory.ReportConsumed(n > 0 ? _takes[0] : 0f, n > 1 ? _takes[1] : 0f);
            factory.MarkSupplied(limited > 0f);
            factory.MarkStalled(false);
        }

        /// <summary>
        /// 挑这一回合要加工的「动态主料」：
        /// 玩家在工厂子菜单里指定了原料就只认它（没货就等料，不会偷偷换成别的鱼）；
        /// 没指定则自动挑库存最多的那种。
        /// </summary>
        string PickDynamicPrimary(FactoryBuilding factory, Recipe recipe, List<Inventory> sources)
        {
            if (factory != null && !string.IsNullOrEmpty(factory.selectedPrimary))
            {
                string want = factory.selectedPrimary;
                if (RecipeDatabase.MatchesPrimary(recipe, want) &&
                    FactoryWorld.Instance.AvailableSources(sources, want) > 1e-5f)
                    return want;
                return null;
            }

            return FactoryWorld.Instance.FindBestSource(
                sources, id => RecipeDatabase.MatchesPrimary(recipe, id), out _);
        }

        /// <summary>把成品和每一路副产物都塞进仓库（仓库装不下就进工厂自己的暂存）。</summary>
        void PushOutputs(FactoryBuilding factory, Recipe recipe, string productId, float copies, float quality,
                         List<WarehouseBuilding> warehouses)
        {
            float produced = recipe.OutputPerUnit * copies;
            float pushed = FactoryWorld.Instance.AddToWarehouses(
                warehouses, productId, produced, quality);
            if (pushed < produced - 1e-5f)
                factory.outputBuffer.Add(productId, produced - pushed, quality);

            for (int b = 0; b < recipe.ByproductIds.Length; b++)
            {
                float byAmount = recipe.ByproductPerUnit[b] * copies;
                if (byAmount <= 0f) continue;
                float byPushed = FactoryWorld.Instance.AddToWarehouses(
                    warehouses, recipe.ByproductIds[b], byAmount, quality);
                if (byPushed < byAmount - 1e-5f)
                    factory.outputBuffer.Add(recipe.ByproductIds[b], byAmount - byPushed, quality);
            }
        }

        void RefundAll(float usedRuns)
        {
            if (_sources.Count == 0) return;
            for (int k = 0; k < _items.Count; k++)
            {
                float used = usedRuns * _per[k];
                float excess = _takes[k] - used;
                if (excess > 1e-5f)
                    _sources[0].Add(_items[k], excess, _quals[k]);
            }
        }

        /// <summary>兜底检查：任何仓库出现负库存就记账（用来盯「同帧抢最后一个」这类 bug）。</summary>
        void GuardAgainstNegative(List<WarehouseBuilding> warehouses, FactoryBuilding factory)
        {
            for (int k = 0; k < warehouses.Count; k++)
            {
                foreach (var stock in warehouses[k].storage.All)
                {
                    if (stock.Amount < -1e-4f)
                    {
                        NegativeInventoryHits++;
                        Debug.LogError($"[FactorySim] 负库存！{warehouses[k].name} 的 {stock.ItemId} = {stock.Amount}");
                    }
                }
            }
        }

        void AdvanceSeaSpots(float hours)
        {
            // 先收集过期的，再统一注销/销毁（不要在遍历 Occupants 时改字典）
            List<SeaSpotBuilding> expired = null;
            foreach (var kv in FactoryWorld.Instance.Occupants)
            {
                if (!(kv.Value is SeaSpotBuilding spot)) continue;
                spot.ageHours += hours;
                if (spot.RemainingHours > 0f) continue;
                if (expired == null) expired = new List<SeaSpotBuilding>();
                expired.Add(spot);
            }
            if (expired == null) return;

            for (int k = 0; k < expired.Count; k++)
            {
                SeaSpotBuilding spot = expired[k];
                if (spot == null) continue;
                FactoryWorld.Instance.Unregister(spot);
                if (spot.gameObject.activeSelf) spot.gameObject.SetActive(false);
                Destroy(spot.gameObject);
            }
        }
    }
}
