using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>码头的一条订单：按码头运转速率慢慢吞吐，不是一点就到。</summary>
    [System.Serializable]
    public class DockOrder
    {
        public string ItemId;
        public bool IsBuy;          // true = 买入，false = 卖出
        public float Remaining;     // 还剩多少单位
        public float UnitPrice;     // 下单时锁定的单价
        public bool AutoCreated;    // 自动补货生成的

        // 每拍金额都会出现不足 1 金的零头，先攒着，攒够 1 金再扣/收，
        // 否则 RoundToInt 会把便宜原料的小额交易直接抹成 0（等于白拿）。
        public float CostCarry;
        public float EarnCarry;

        public DockOrder(string itemId, bool isBuy, float amount, float unitPrice, bool auto)
        {
            ItemId = itemId;
            IsBuy = isBuy;
            Remaining = amount;
            UnitPrice = unitPrice;
            AutoCreated = auto;
        }
    }

    /// <summary>
    /// 货运码头：订单制收发货。
    /// 玩家在这里下单（卖出存货 / 买入原料），码头按「运转速率」每秒只转运一点点，货是慢慢流过去的。
    /// 玩家自己只能生产鱼，其它原料都靠这里的订单买；还能给某些原料设「自动补货」目标，低于目标就自动下单。
    /// </summary>
    public class CargoDockBuilding : PlacedTile
    {
        public override bool CanDemolish => true;

        [Header("运营")]
        public bool powered = true;
        public float dailyCost = 50f;              // 每个游戏日扣的金币
        public float throughputPerTurn = 2f;        // 运转速率：每回合转运多少单位

        [Header("购入目录（玩家不能自产的原料）")]
        /// <summary>码头默认能买到的辅料 / 包材：工厂开工要用的全在这里。</summary>
        public static readonly string[] DefaultBuyCatalog =
        {
            ItemDatabase.EmptyCan, ItemDatabase.Salt, ItemDatabase.Starch,
            ItemDatabase.Film, ItemDatabase.Enzyme, ItemDatabase.Water,
            ItemDatabase.Sawdust, ItemDatabase.CookingOil, ItemDatabase.Rice,
        };

        public string[] buyCatalog = (string[])DefaultBuyCatalog.Clone();

        /// <summary>
        /// 把默认辅料并进清单：旧存档 / 老场景里的码头只写了 3 种，
        /// 结果工厂要的淀粉、复合膜、酶剂、水、木屑、食用油都买不到。
        /// </summary>
        public void EnsureBuyCatalog()
        {
            var list = new System.Collections.Generic.List<string>(buyCatalog ?? new string[0]);
            for (int k = 0; k < DefaultBuyCatalog.Length; k++)
                if (!list.Contains(DefaultBuyCatalog[k])) list.Add(DefaultBuyCatalog[k]);
            buyCatalog = list.ToArray();
        }

        [Header("自动供货")]
        [Tooltip("开启后自动统计同一片路网上所有工厂的需求，缺多少就下单买多少")]
        public bool autoSupply = true;
        [Tooltip("按多少回合的用量来备货（需求 × 回合数 = 目标库存）")]
        public float supplyBufferTurns = 30f;
        [Tooltip("单条自动订单最多下多少单位")]
        public float supplyMaxOrder = 200f;

        [Header("自动出货")]
        [Tooltip("仓库里的成品超过阈值就自动挂卖单（只卖配方里的成品，不卖原料）")]
        public bool autoSell = true;
        public float autoSellThreshold = 5f;
        public float autoSellBatch = 30f;

        public List<DockOrder> orders = new List<DockOrder>();

        readonly Dictionary<string, float> _demand = new Dictionary<string, float>();
        readonly Dictionary<string, float> _incoming = new Dictionary<string, float>();
        readonly List<KeyValuePair<string, float>> _shortfalls = new List<KeyValuePair<string, float>>();

        readonly List<WarehouseBuilding> _warehouses = new List<WarehouseBuilding>();
        readonly List<Inventory> _sources = new List<Inventory>();
        readonly List<Stock> _sellList = new List<Stock>();

        int _lastDay = 0;

        void Update()
        {
        }

        /// <summary>推进一个回合：结算日费 → 水体检定 → 自动供货下单 → 自动出货挂单 → 按速率吞吐订单。</summary>
        public void AdvanceTurn()
        {
            // 还在施工：不扣日费、不接单、不吞吐
            // （以前这里漏了判断，码头没建好就能买卖东西）
            if (UnderConstruction) return;

            EnsureBuyCatalog();          // 旧存档里的购入清单只有 3 种，这里补齐工厂要用的辅料
            TradedThisTurn = false;      // 每回合重置，只有本回合真的成交才派工人

            HandleDailyCost();
            RefreshEnclosed();

            // 所在水体被陆地围死 → 船进不来，转运速率按 0 算
            if (Enclosed) return;

            EnsureAutoSupply();
            EnsureAutoSell();
            ProcessOrders();
        }

        /// <summary>这格水体是不是被陆地围住了（围住 = 码头废掉）。</summary>
        public bool Enclosed { get; private set; }

        /// <summary>本回合有没有真的成交（买入或卖出）。没有就不派工人。</summary>
        public bool TradedThisTurn { get; private set; }

        void RefreshEnclosed()
        {
            var map = FindObjectOfType<IsoTileMap>();
            Enclosed = map != null && map.IsEnclosedWater(gridX, gridY);
        }

        /// <summary>当前有效转运速率（被围住时为 0）。</summary>
        public float EffectiveThroughput => Enclosed ? 0f : throughputPerTurn * LandmarkBonus.ThroughputMultiplier(Cell) * LevelSpeedMultiplier;

        /// <summary>自动出货：仓库里超过阈值的成品自动挂卖单（只认成品，不会把原料/生鱼卖掉）。</summary>
        void EnsureAutoSell()
        {
            if (!autoSell || FactoryWorld.Instance == null) return;

            // 动态配方的成品是「加工过的鱼」，没法枚举固定产物表，直接扫仓库里的成品
            BuildSellList(_sellList);
            for (int k = 0; k < _sellList.Count; k++)
            {
                string item = _sellList[k].ItemId;
                if (!ItemDatabase.IsFinishedGood(item)) continue;
                if (HasPending(item, false)) continue;

                float excess = _sellList[k].Amount - autoSellThreshold;
                if (excess < 1f) continue;

                QueueSell(item, Mathf.Min(excess, autoSellBatch));
            }
        }

        void HandleDailyCost()
        {
            int day = Mathf.FloorToInt(GameClock.Days);
            if (day <= _lastDay) return;
            _lastDay = day;

            // 没有在途订单就不收日费（码头空转不该白扣钱）
            bool hasWork = false;
            for (int k = 0; k < orders.Count; k++)
            {
                if (orders[k] != null && orders[k].Remaining > 1e-3f) { hasWork = true; break; }
            }
            if (!hasWork) return;

            if (dailyCost > 0f) PlayerProgress.AddGold(-Mathf.RoundToInt(dailyCost * LandmarkBonus.OperatingCostMultiplier(Cell) * LevelCostMultiplier));
        }

        // ---------------- 订单 ----------------

        public DockOrder QueueSell(string itemId, float amount)
        {
            if (Enclosed) return null;         // 船进不来，不能交易
            if (ItemDatabase.IsTreasureItem(itemId)) return null;   // 贵重品不卖
            if (amount <= 1e-3f) return null;
            var order = new DockOrder(itemId, false, amount,
                                      ItemDatabase.SellPrice(itemId, CurrentQuality(itemId)), false);
            orders.Add(order);
            return order;
        }

        public DockOrder QueueBuy(string itemId, float amount, bool auto = false)
        {
            if (Enclosed) return null;         // 船进不来，不能交易
            if (amount <= 1e-3f) return null;
            var order = new DockOrder(itemId, true, amount, ItemDatabase.Get(itemId).BasePrice, auto);
            orders.Add(order);
            return order;
        }

        public bool CancelOrder(DockOrder order) => orders.Remove(order);

        public bool HasPending(string itemId, bool isBuy)
        {
            foreach (var o in orders)
                if (o.ItemId == itemId && o.IsBuy == isBuy && o.Remaining > 1e-3f) return true;
            return false;
        }

        void AccumulateDemand(string item, float perTurn)
        {
            if (string.IsNullOrEmpty(item) || perTurn <= 0f) return;
            _demand.TryGetValue(item, out float cur);
            _demand[item] = cur + perTurn;
        }

        /// <summary>
        /// 自动供货：统计同一片路网上所有工厂的需求（配方输入 × 速率），
        /// 再看仓库现有库存 + 别的码头在途的买入单，缺多少补多少。
        /// 需求小的先补（订单按缺口从小到大排），这样几栋建筑互相牵连也不会抢单或重复补货。
        /// </summary>
        void EnsureAutoSupply()
        {
            if (!autoSupply || FactoryWorld.Instance == null) return;
            if (buyCatalog == null || buyCatalog.Length == 0) return;

            var world = FactoryWorld.Instance;

            // 1) 这栋码头的「供货范围」= 和它相连的仓库网络。
            //    以前是看「码头自己邻接的路面组件」，可是码头建在水里、经常只挨着仓库不挨路面，
            //    组件号直接是 -1 → 整段逻辑 return，等于完全不补货（主人反馈的「自动进货没效果」）。
            world.WarehousesConnectedTo(this, _warehouses);
            if (_warehouses.Count == 0) return;

            // 2) 汇总需求：同一仓库网络里所有工厂的主料 / 辅料 / 包材
            _demand.Clear();
            foreach (var kv in world.Occupants)
            {
                if (!(kv.Value is FactoryBuilding factory)) continue;
                if (factory.UnderConstruction) continue;
                if (!SharesWarehouse(factory)) continue;

                Recipe recipe = factory.CurrentRecipe;
                float rate = factory.ratePerTurn * factory.LevelSpeedMultiplier
                             * LandmarkBonus.ProductionMultiplier(factory.Cell);

                for (int k = 0; k < recipe.PrimaryItems.Length; k++)
                    AccumulateDemand(recipe.PrimaryItems[k], recipe.PrimaryPerUnit[k] * rate);
                for (int k = 0; k < recipe.AuxItems.Length; k++)
                    AccumulateDemand(recipe.AuxItems[k], recipe.AuxPerUnit[k] * rate);
            }
            if (_demand.Count == 0) return;

            // 3) 同一仓库网络上、别的码头的在途买入单算作「已有供应」（避免几个码头重复补同一份）
            _incoming.Clear();
            foreach (var kv in world.Occupants)
            {
                if (!(kv.Value is CargoDockBuilding other) || other == this) continue;
                if (!SharesWarehouse(other)) continue;
                foreach (var order in other.orders)
                {
                    if (!order.IsBuy) continue;
                    _incoming.TryGetValue(order.ItemId, out float cur);
                    _incoming[order.ItemId] = cur + order.Remaining;
                }
            }

            // 4) 算每个物资还缺多少
            _shortfalls.Clear();
            foreach (var pair in _demand)
            {
                string item = pair.Key;
                float demandRate = pair.Value;
                if (System.Array.IndexOf(buyCatalog, item) < 0) continue;    // 只买得到目录里的
                if (HasPending(item, true)) continue;                        // 手动/自动订单已经在补了

                float stock = FactoryWorld.Instance.AvailableWarehouse(_warehouses, item);
                _incoming.TryGetValue(item, out float incoming);
                float need = demandRate * supplyBufferTurns - stock - incoming;
                if (need > 0.5f) _shortfalls.Add(new KeyValuePair<string, float>(item, need));
            }

            // 5) 缺口小的先补：单栋码头每回合只能搬 throughput，缺得越多越要靠多建码头 / 升级
            _shortfalls.Sort((a, b) => a.Value.CompareTo(b.Value));
            foreach (var pair in _shortfalls)
            {
                float amount = Mathf.Min(pair.Value, supplyMaxOrder);
                if (QueueBuy(pair.Key, amount, true) != null)
                    Debug.Log($"[码头] 自动补货 {pair.Key} ×{amount:0.#}（需求 {_demand[pair.Key]:0.##}/回合，库存 {FactoryWorld.Instance.AvailableWarehouse(_warehouses, pair.Key):0.#}）");
            }
        }

        readonly List<WarehouseBuilding> _factoryWarehouses = new List<WarehouseBuilding>();

        /// <summary>这个地块和本码头是不是共用同一个仓库网络（工厂、或别的码头都适用）。</summary>
        bool SharesWarehouse(PlacedTile tile)
        {
            if (tile == null || _warehouses.Count == 0) return false;
            if (tile == this) return true;

            _factoryWarehouses.Clear();
            FactoryWorld.Instance.WarehousesConnectedTo(tile, _factoryWarehouses);
            for (int k = 0; k < _factoryWarehouses.Count; k++)
                if (_warehouses.Contains(_factoryWarehouses[k])) return true;
            return false;
        }

        void ProcessOrders()
        {
            int active = 0;
            for (int k = 0; k < orders.Count; k++)
                if (orders[k].Remaining > 1e-3f) active++;
            if (active == 0) return;

            // 运转速率在所有进行中的订单之间平分，这样同时订几种原料都会一起慢慢到货、一起扣钱
            float perOrder = throughputPerTurn / active;
            for (int k = 0; k < orders.Count; k++)
            {
                var order = orders[k];
                if (order.Remaining <= 1e-3f) continue;

                float want = Mathf.Min(perOrder, order.Remaining);
                float moved = order.IsBuy ? MoveBuy(order, want) : MoveSell(order, want);
                if (moved > 0f)
                {
                    order.Remaining -= moved;
                    TradedThisTurn = true;      // 有真正成交才派工人搬货
                }

                // 订单结束时把不足 1 金的零头结清，别让它悬着
                if (order.Remaining <= 1e-3f)
                {
                    if (order.IsBuy && order.CostCarry >= 0.5f) PlayerProgress.AddGold(-1);
                    if (!order.IsBuy && order.EarnCarry >= 0.5f) PlayerProgress.AddGold(1);
                    order.CostCarry = 0f;
                    order.EarnCarry = 0f;
                }
            }
            orders.RemoveAll(o => o.Remaining <= 1e-3f);
        }

        float MoveBuy(DockOrder order, float want)
        {
            if (FactoryWorld.Instance == null) return 0f;
            FactoryWorld.Instance.WarehousesConnectedTo(this, _warehouses);
            if (_warehouses.Count == 0) return 0f;

            float price = Mathf.Max(0.01f, order.UnitPrice);
            // 手里的钱 + 这笔订单攒着的零头，才是真正买得起的量
            float affordable = (PlayerProgress.Gold + order.CostCarry) / price;
            float amount = Mathf.Min(want, affordable);
            if (amount <= 0f) return 0f;

            float costExact = amount * price + order.CostCarry;
            int pay = Mathf.FloorToInt(costExact);
            order.CostCarry = costExact - pay;          // 零头留到下一拍
            if (pay > PlayerProgress.Gold) return 0f;
            if (pay > 0) PlayerProgress.AddGold(-pay);

            _warehouses[0].storage.Add(order.ItemId, amount, 1f);
            return amount;
        }

        float MoveSell(DockOrder order, float want)
        {
            float remaining = Mathf.Min(want, order.Remaining);
            float qualityWeighted = 0f;
            float takenTotal = 0f;

            foreach (var inv in CollectSellableSources())
            {
                if (remaining <= 0f) break;
                float taken = inv.Take(order.ItemId, remaining, out float q);
                if (taken <= 0f) continue;
                takenTotal += taken;
                qualityWeighted += q * taken;
                remaining -= taken;
            }
            if (takenTotal <= 0f) return 0f;

            float quality = qualityWeighted / takenTotal;
            float earnExact = takenTotal * ItemDatabase.SellPrice(order.ItemId, quality) + order.EarnCarry;
            int earn = Mathf.FloorToInt(earnExact);
            order.EarnCarry = earnExact - earn;
            if (earn > 0) PlayerProgress.AddGold(earn);
            return takenTotal;
        }

        // ---------------- 查询 ----------------

        /// <summary>相连的仓库与工厂成品暂存，作为可售出的来源。</summary>
        public List<Inventory> CollectSellableSources()
        {
            _sources.Clear();
            if (FactoryWorld.Instance == null) return _sources;

            FactoryWorld.Instance.WarehousesConnectedTo(this, _warehouses);
            for (int k = 0; k < _warehouses.Count; k++)
                _sources.Add(_warehouses[k].storage);

            int comp = FactoryWorld.Instance.RoadComponentOf(Cell);
            if (comp >= 0)
            {
                foreach (var kv in FactoryWorld.Instance.Occupants)
                {
                    if (kv.Value is FactoryBuilding f && FactoryWorld.Instance.RoadComponentOf(kv.Key) == comp)
                        _sources.Add(f.outputBuffer);
                }
            }
            return _sources;
        }

        public float Available(string itemId)
        {
            float sum = 0f;
            foreach (var inv in CollectSellableSources()) sum += inv.AmountOf(itemId);
            return sum;
        }

        float CurrentQuality(string itemId)
        {
            float amount = 0f, weighted = 0f;
            foreach (var inv in CollectSellableSources())
            {
                float a = inv.AmountOf(itemId);
                if (a <= 0f) continue;
                amount += a;
                weighted += inv.QualityOf(itemId) * a;
            }
            return amount > 0f ? weighted / amount : 1f;
        }

        /// <summary>可售出清单（同种合并数量，品质按质量加权）。</summary>
        public void BuildSellList(List<Stock> result)
        {
            result.Clear();
            foreach (var inv in CollectSellableSources())
            {
                foreach (var stock in inv.All)
                {
                    if (stock.Amount <= 1e-4f) continue;
                    // 贵重品（沉船宝物）不参与买卖：不进售出表，也不会被自动出货扫到
                    if (ItemDatabase.IsTreasureItem(stock.ItemId)) continue;
                    Stock merged = result.Find(s => s.ItemId == stock.ItemId);
                    if (merged == null)
                    {
                        result.Add(new Stock(stock.ItemId, stock.Amount, stock.Quality));
                    }
                    else
                    {
                        float total = merged.Amount + stock.Amount;
                        merged.Quality = (merged.Quality * merged.Amount + stock.Quality * stock.Amount) / total;
                        merged.Amount = total;
                    }
                }
            }
        }
    }
}
