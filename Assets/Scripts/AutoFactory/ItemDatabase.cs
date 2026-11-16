using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Shenyan
{
    /// <summary>物资计量单位。</summary>
    public enum ItemUnit
    {
        Kilogram,   // 按 KG 计（鱼、成品）
        Piece,      // 按 个 计（空罐头）
    }

    /// <summary>一种物资的定义。</summary>
    public class ItemDef
    {
        public string Id;
        public string DisplayName;
        public ItemUnit Unit;
        public bool HasQuality;     // 是否携带品质标签
        public float MassPerUnit;   // 每个（罐）对应的鱼重量，KG
        public float BasePrice;     // 原价（售价 = 原价/2 + 原价/2 × 品质）
        public bool IsTreasure;     // 贵重品：不占重量，沉船打捞获得，后续作为升级材料
        public string UnitText => Unit == ItemUnit.Kilogram ? "KG" : "个";

        // ---- 工艺链（只有「鱼」这一路会用到，其它物资留空）----
        public string BaseId;           // 基础鱼名；空 = 不是加工鱼
        public FishStage[] Stages;      // 已经过的工序
    }

    /// <summary>鱼的加工工序：物品名直接拼接工艺词，价格按倍率累积，不用给每种鱼×每道工序建物品。</summary>
    public enum FishStage
    {
        Fillet,     // 剖好的
        Breading,   // 裹粉
        Fried,      // 油炸
        Canned,     // 罐头（替换掉前面的工艺词）
        Dried,      // 干
        Frozen,     // 冷冻
        Smoked,     // 烟熏
        Fermented,  // 发酵
        Oil,        // 鱼油
        Bagged,     // 袋装

        // ---- 成型类成品（鱼糜 / 鱼丸 / 鱼糕 / 鱼肉肠）----
        Minced,     // 糜
        Ball,       // 丸
        Cake,       // 糕
        Sausage,    // 肉肠

        // ---- 发酵类 ----
        Salted,     // 腌制
        Sauce,      // 露（鱼露）

        // ---- 副产物加工 ----
        Meal,       // 粉（鱼粉）

        // ---- 调味罐头 ----
        TomatoCan,  // 茄汁罐头
    }

    /// <summary>一档库存：同种物资按质量加权只保留一个品质。可序列化，随场景存档。</summary>
    [System.Serializable]
    public class Stock
    {
        public string ItemId;
        public float Amount;
        public float Quality;   // 0~1

        public Stock(string itemId, float amount, float quality)
        {
            ItemId = itemId;
            Amount = amount;
            Quality = quality;
        }
    }

    /// <summary>物资登记表。品质规则：同种不同品质混合时按质量加权平均。</summary>
    public static class ItemDatabase
    {
        public const string EmptyCan = "空罐头";         // 灌装厂的容器
        public const string Salt = "盐";                 // 需要购买的原料
        public const string Rice = "大米";               // 需要购买的原料
        public const string JunkFish = "杂鱼";
        public const string Sardine = "沙丁鱼";

        // ---- 加工链上的物料（鱼这一路由工艺链自动生成，这里只剩辅料与副产物）----
        public const string Offcut = "边角料";
        public const string WasteWater = "废水";
        public const string Starch = "淀粉";
        public const string WasteHeat = "废热";
        public const string Slurry = "发酵液";
        public const string Brine = "卤水";
        public const string Enzyme = "酶剂";
        public const string FishOil = "鱼油";
        public const string Residue = "残渣";
        public const string Water = "水";
        public const string Film = "复合膜";
        public const string Sawdust = "木屑";
        public const string SmokeGas = "烟气";
        public const string CookingOil = "食用油";
        public const string WasteOil = "废油";
        public const string ResidualLiquid = "残液";
        public const string FishMeal = "鱼粉";           // 边角料 → 萃取精炼厂的副产品饲料

        // ---- 贵重品（只在沉船里出，不占重量，以后作升级材料）----
        public const string SouthSeaPearl = "南海黑珍珠";
        public const string MeloPearl = "美乐珠";
        public const string Ambergris = "龙涎香";

        /// <summary>每捞起一条沉船给一个，用来标记捞了几条船；集齐会解锁授勋漫画。</summary>
        public const string NationalTreasure = "国宝";

        /// <summary>一罐罐头里装的鱼重量（KG/个）。</summary>
        public const float FishPerCan = 0.2f;

        static readonly Dictionary<string, ItemDef> Map = new Dictionary<string, ItemDef>
        {
            { EmptyCan, new ItemDef { Id = EmptyCan, DisplayName = EmptyCan, Unit = ItemUnit.Piece, HasQuality = false, BasePrice = 3f } },
            { Salt, new ItemDef { Id = Salt, DisplayName = Salt, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 2f } },
            { Rice, new ItemDef { Id = Rice, DisplayName = Rice, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 1.5f } },
            { JunkFish, new ItemDef { Id = JunkFish, DisplayName = JunkFish, Unit = ItemUnit.Kilogram, HasQuality = true, BasePrice = 40f } },
            { Sardine, new ItemDef { Id = Sardine, DisplayName = Sardine, Unit = ItemUnit.Kilogram, HasQuality = true, BasePrice = 60f } },

            // ---- 辅料 / 副产物（鱼本体由工艺链自动生成，不在这里登记） ----
            { Offcut, new ItemDef { Id = Offcut, DisplayName = Offcut, Unit = ItemUnit.Kilogram, HasQuality = true, BasePrice = 20f } },
            { WasteWater, new ItemDef { Id = WasteWater, DisplayName = WasteWater, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 1f } },
            { Starch, new ItemDef { Id = Starch, DisplayName = Starch, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 4f } },
            { WasteHeat, new ItemDef { Id = WasteHeat, DisplayName = WasteHeat, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 2f } },
            { Slurry, new ItemDef { Id = Slurry, DisplayName = Slurry, Unit = ItemUnit.Kilogram, HasQuality = true, BasePrice = 160f } },
            { Brine, new ItemDef { Id = Brine, DisplayName = Brine, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 3f } },
            { Enzyme, new ItemDef { Id = Enzyme, DisplayName = Enzyme, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 12f } },
            { FishOil, new ItemDef { Id = FishOil, DisplayName = FishOil, Unit = ItemUnit.Kilogram, HasQuality = true, BasePrice = 380f } },
            { Residue, new ItemDef { Id = Residue, DisplayName = Residue, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 5f } },
            { Water, new ItemDef { Id = Water, DisplayName = Water, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 0.5f } },
            { Film, new ItemDef { Id = Film, DisplayName = Film, Unit = ItemUnit.Piece, HasQuality = false, BasePrice = 2f } },
            { Sawdust, new ItemDef { Id = Sawdust, DisplayName = Sawdust, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 1.5f } },
            { SmokeGas, new ItemDef { Id = SmokeGas, DisplayName = SmokeGas, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 0.5f } },
            { CookingOil, new ItemDef { Id = CookingOil, DisplayName = CookingOil, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 8f } },
            { WasteOil, new ItemDef { Id = WasteOil, DisplayName = WasteOil, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 6f } },
            { ResidualLiquid, new ItemDef { Id = ResidualLiquid, DisplayName = ResidualLiquid, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 1f } },
            { FishMeal, new ItemDef { Id = FishMeal, DisplayName = FishMeal, Unit = ItemUnit.Kilogram, HasQuality = false, BasePrice = 30f } },

            // ---- 贵重品：沉船里打捞出来，不占重量，后续作升级材料 ----
            { SouthSeaPearl, new ItemDef { Id = SouthSeaPearl, DisplayName = SouthSeaPearl, Unit = ItemUnit.Piece, HasQuality = false, BasePrice = 12000f, IsTreasure = true } },
            { MeloPearl, new ItemDef { Id = MeloPearl, DisplayName = MeloPearl, Unit = ItemUnit.Piece, HasQuality = false, BasePrice = 9000f, IsTreasure = true } },
            { Ambergris, new ItemDef { Id = Ambergris, DisplayName = Ambergris, Unit = ItemUnit.Piece, HasQuality = false, BasePrice = 7500f, IsTreasure = true } },
            { NationalTreasure, new ItemDef { Id = NationalTreasure, DisplayName = NationalTreasure, Unit = ItemUnit.Piece, HasQuality = false, BasePrice = 0f, IsTreasure = true } },
        };

        static ItemDatabase()
        {
            // 海产聚集点会捞上图鉴里的各种鱼：按图鉴估价补登记，保证它们都能进库存、能在码头卖钱。
            foreach (string name in FishDatabase.AllNames)
            {
                if (Map.ContainsKey(name)) continue;
                Map[name] = new ItemDef
                {
                    Id = name,
                    DisplayName = name,
                    Unit = ItemUnit.Kilogram,
                    HasQuality = true,
                    BasePrice = Mathf.Max(1f, FishDatabase.Get(name).value),
                };
            }
        }

        /// <summary>售价：原价/2 + 原价/2 × 品质（不带品质的按满品质算）。</summary>
        public static float SellPrice(string id, float quality)        {
            ItemDef def = Get(id);
            float q = def.HasQuality ? Mathf.Clamp01(quality) : 1f;
            return def.BasePrice * 0.5f + def.BasePrice * 0.5f * q;
        }

        /// <summary>是不是贵重品（沉船宝物，不占重量）。</summary>
        public static bool IsTreasureItem(string id) => !string.IsNullOrEmpty(id) && Get(id).IsTreasure;

        // ---------------- 工艺链（鱼的加工：名字拼接 + 价格倍率） ----------------

        struct StageInfo
        {
            public string Word;
            public bool Suffix;       // true = 贴在名字后面，false = 贴在前面
            public bool ReplaceAll;   // true = 清掉之前的工艺词（罐头）
            public float Multiplier;
        }

        static readonly Dictionary<FishStage, StageInfo> StageTable = new Dictionary<FishStage, StageInfo>
        {
            { FishStage.Fillet,    new StageInfo { Word = "剖好的", Multiplier = 1.10f } },
            { FishStage.Breading,  new StageInfo { Word = "裹粉",   Multiplier = 1.00f } },
            { FishStage.Fried,     new StageInfo { Word = "油炸",   Multiplier = 1.30f } },
            { FishStage.Canned,    new StageInfo { Word = "罐头",   Suffix = true, ReplaceAll = true, Multiplier = 1.50f } },
            { FishStage.Dried,     new StageInfo { Word = "干",     Suffix = true, Multiplier = 1.20f } },
            { FishStage.Frozen,    new StageInfo { Word = "冷冻",   Multiplier = 1.20f } },
            { FishStage.Smoked,    new StageInfo { Word = "烟熏",   Multiplier = 1.30f } },
            { FishStage.Fermented, new StageInfo { Word = "发酵",   Multiplier = 1.25f } },
            { FishStage.Oil,       new StageInfo { Word = "油",     Suffix = true, Multiplier = 1.40f } },
            { FishStage.Bagged,    new StageInfo { Word = "袋装",   Multiplier = 1.15f } },

            // ---- 成型 / 发酵 / 副产物 / 调味罐头 ----
            { FishStage.Minced,    new StageInfo { Word = "糜",       Suffix = true, Multiplier = 1.15f } },
            { FishStage.Ball,      new StageInfo { Word = "丸",       Suffix = true, Multiplier = 1.25f } },
            { FishStage.Cake,      new StageInfo { Word = "糕",       Suffix = true, Multiplier = 1.25f } },
            { FishStage.Sausage,   new StageInfo { Word = "肉肠",     Suffix = true, Multiplier = 1.30f } },
            { FishStage.Salted,    new StageInfo { Word = "腌制",     Multiplier = 1.20f } },
            { FishStage.Sauce,     new StageInfo { Word = "露",       Suffix = true, Multiplier = 1.50f } },
            { FishStage.Meal,      new StageInfo { Word = "粉",       Suffix = true, Multiplier = 1.10f } },
            { FishStage.TomatoCan, new StageInfo { Word = "茄汁罐头", Suffix = true, ReplaceAll = true, Multiplier = 1.60f } },
        };

        static readonly Dictionary<string, ItemDef> Dynamic = new Dictionary<string, ItemDef>();

        /// <summary>
        /// 有些基础物品本身就是某道工序的完成态（预留：以后如果加「腌制」工艺，腌好的鱼可以登记成已剖）。
        /// 目前原点只有生鱼，所以这张表是空的。
        /// </summary>
        static readonly Dictionary<string, FishStage[]> PreStaged = new Dictionary<string, FishStage[]>();

        static StageInfo InfoOf(FishStage stage)
        {
            return StageTable.TryGetValue(stage, out StageInfo info)
                ? info
                : new StageInfo { Multiplier = 1f };
        }

        /// <summary>把物品 id 拆成「基础鱼 + 工序序列」；不是加工鱼返回 false。</summary>
        public static bool TryGetStages(string id, out string baseId, out FishStage[] stages)
        {
            baseId = null;
            stages = null;
            if (string.IsNullOrEmpty(id)) return false;

            int at = id.IndexOf('@');
            if (at <= 0) return false;

            string[] parts = id.Substring(at + 1).Split(',');
            var list = new FishStage[parts.Length];
            for (int k = 0; k < parts.Length; k++)
            {
                if (!System.Enum.TryParse(parts[k], out list[k])) return false;
                if (!StageTable.ContainsKey(list[k])) return false;
            }

            baseId = id.Substring(0, at);
            stages = list;
            return true;
        }

        /// <summary>取最后一道工序；生鱼（或非加工鱼）返回 false。</summary>
        public static bool TryGetLastStage(string id, out FishStage stage)
        {
            stage = default;
            if (TryGetStages(id, out _, out FishStage[] stages) && stages.Length > 0)
            {
                stage = stages[stages.Length - 1];
                return true;
            }
            if (PreStaged.TryGetValue(id, out FishStage[] pre) && pre.Length > 0)
            {
                stage = pre[pre.Length - 1];
                return true;
            }
            return false;
        }

        /// <summary>给鱼施加一道工序，返回新物品 id（例如 蓝鳍金枪鱼 → 蓝鳍金枪鱼@Fillet）。</summary>
        public static string Process(string id, FishStage stage)
        {
            var stages = new List<FishStage>();
            string baseId = id;

            if (TryGetStages(id, out string baseOfId, out FishStage[] existing))
            {
                baseId = baseOfId;
                stages.AddRange(existing);
            }

            // 已经是这道工序完成态的物品，不再重复加工同一道工序
            if (PreStaged.TryGetValue(baseId, out FishStage[] pre) && System.Array.IndexOf(pre, stage) >= 0)
                return baseId;

            stages.Add(stage);   // 罐头只改名字，价格倍率照样累积

            var parts = new string[stages.Count];
            for (int k = 0; k < stages.Count; k++) parts[k] = stages[k].ToString();
            return baseId + "@" + string.Join(",", parts);
        }

        /// <summary>
        /// 按工艺词拼中文名：只显示「最后一道工序」，避免「油炸裹粉剖好的…」越叠越长。
        /// 罐头这类替换型直接是「鱼名 + 罐头」；腌制过的鱼本身就是处理过的，不再加「剖好的」。
        /// 价格仍然是全程累乘（不受显示名影响）。
        /// </summary>
        static string BuildDisplayName(string baseName, FishStage[] stages)
        {
            if (stages == null || stages.Length == 0) return baseName;

            FishStage last = stages[stages.Length - 1];
            StageInfo info = InfoOf(last);

            if (info.ReplaceAll) return baseName + info.Word;
            if (last == FishStage.Fillet && baseName.Contains("腌制")) return baseName;

            return info.Suffix ? baseName + info.Word : info.Word + baseName;
        }

        static bool TryResolveProcessed(string id, out ItemDef def)
        {
            def = null;
            if (Dynamic.TryGetValue(id, out def)) return true;
            if (!TryGetStages(id, out string baseId, out FishStage[] stages) || stages.Length == 0) return false;
            if (!Map.TryGetValue(baseId, out ItemDef baseDef)) return false;

            float price = baseDef.BasePrice;
            for (int k = 0; k < stages.Length; k++) price *= InfoOf(stages[k]).Multiplier;

            def = new ItemDef
            {
                Id = id,
                DisplayName = BuildDisplayName(baseDef.DisplayName, stages),
                Unit = ItemUnit.Kilogram,
                HasQuality = true,
                BasePrice = price,
                BaseId = baseId,
                Stages = stages,
            };
            Dynamic[id] = def;
            return true;
        }

        /// <summary>面板 / 日志里显示的名字（不要直接显示 id）。</summary>
        public static string NameOf(string id) => Get(id).DisplayName;

        /// <summary>工序的中文词（面板显示用）。</summary>
        public static string StageWord(FishStage stage) => InfoOf(stage).Word;

        /// <summary>能不能被码头当成成品卖掉：只认「终结工序」的鱼 + 固定成品，半成品（剖/裹粉/油炸）不自动卖。</summary>
        public static bool IsFinishedGood(string id)
        {
            if (TryGetLastStage(id, out FishStage stage))
            {
                switch (stage)
                {
                    case FishStage.Canned:
                    case FishStage.TomatoCan:
                    case FishStage.Dried:
                    case FishStage.Frozen:
                    case FishStage.Smoked:
                    case FishStage.Oil:
                    case FishStage.Bagged:
                    case FishStage.Ball:
                    case FishStage.Cake:
                    case FishStage.Sausage:
                    case FishStage.Sauce:
                    case FishStage.Meal:
                        return true;
                    default:
                        return false;
                }
            }

            // 固定配方产物（鱼油 / 发酵液 / 鱼粉）
            return id == FishOil || id == Slurry || id == FishMeal;
        }

        /// <summary>这个物品是不是「鱼」（可以直接进加工链）。</summary>
        public static bool IsFish(string id)
        {
            if (TryGetStages(id, out _, out _)) return true;
            return Map.TryGetValue(id, out ItemDef def) && def.Unit == ItemUnit.Kilogram &&
                   (def.HasQuality && (id == JunkFish || FishDatabase.AllNames.Contains(id)));
        }

        public static ItemDef Get(string id)
        {
            if (Map.TryGetValue(id, out ItemDef def)) return def;
            if (TryResolveProcessed(id, out def)) return def;
            Debug.LogWarning($"[ItemDatabase] 未登记的物资：{id}");
            return new ItemDef { Id = id, DisplayName = id, Unit = ItemUnit.Kilogram, HasQuality = true };
        }

        /// <summary>把数值格式化成「5,524KG」「41个」这种形式。</summary>
        public static string Describe(string id, float amount)
        {
            ItemDef def = Get(id);
            if (def.Unit == ItemUnit.Piece) return $"{Mathf.RoundToInt(amount):N0}{def.UnitText}";
            return $"{amount:N1}{def.UnitText}";
        }
    }

    /// <summary>
    /// 库存：每种物资一条记录，加入时按质量加权合并品质。
    /// 用 List 存（字典不可序列化），随场景/存档保留；条目很少，线性查找足够。
    /// Take 是唯一的扣减入口，并且是「原子」的——返回真正扣到的量，
    /// 调用方必须用返回值而不是请求值，这样多个工厂同帧抢同一份存货也不会扣成负数。
    /// </summary>
    [System.Serializable]
    public class Inventory
    {
        [SerializeField] List<Stock> stocks = new List<Stock>();

        public IReadOnlyList<Stock> All => stocks;
        public int Count => stocks.Count;

        Stock Find(string itemId)
        {
            for (int k = 0; k < stocks.Count; k++)
                if (stocks[k].ItemId == itemId) return stocks[k];
            return null;
        }

        public float AmountOf(string itemId)
        {
            Stock s = Find(itemId);
            return s != null ? s.Amount : 0f;
        }

        public float QualityOf(string itemId)
        {
            Stock s = Find(itemId);
            return s != null ? s.Quality : 0f;
        }

        public void Add(string itemId, float amount, float quality)
        {
            if (amount <= 0f) return;
            Stock s = Find(itemId);
            if (s != null)
            {
                float total = s.Amount + amount;
                s.Quality = total <= Mathf.Epsilon ? quality : (s.Quality * s.Amount + quality * amount) / total;
                s.Amount = total;
            }
            else
            {
                stocks.Add(new Stock(itemId, amount, Mathf.Clamp01(quality)));
            }
        }

        /// <summary>原子扣减：返回真正扣掉的量（可能少于 wanted）。</summary>
        public float Take(string itemId, float wanted)
        {
            return Take(itemId, wanted, out _);
        }

        public float Take(string itemId, float wanted, out float quality)
        {
            quality = 0f;
            if (wanted <= 0f) return 0f;
            Stock s = Find(itemId);
            if (s == null || s.Amount <= 0f) return 0f;

            float taken = Mathf.Min(wanted, s.Amount);
            quality = s.Quality;
            s.Amount -= taken;
            if (s.Amount <= 1e-5f) stocks.Remove(s);
            return taken;
        }

        public void Clear() => stocks.Clear();

        /// <summary>整条移除某种物资（清理已经不用的旧物料）。</summary>
        public bool Remove(string itemId)
        {
            for (int k = 0; k < stocks.Count; k++)
            {
                if (stocks[k].ItemId != itemId) continue;
                stocks.RemoveAt(k);
                return true;
            }
            return false;
        }
    }
}
