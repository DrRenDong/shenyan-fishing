using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 王老板的限时订单：出现后过 5 回合可以手动刷新；
    /// 完成给「正常卖出 3 倍的现金 + 一组土地」并立刻换一张新单。
    /// 需求从随机任务池里抽——只会抽玩家当前已经能生产的东西。
    /// </summary>
    public class BossRushSystem : MonoBehaviour
    {
        public static BossRushSystem Instance { get; private set; }

        [System.Serializable]
        public class RushOrder
        {
            public string title = "";
            public string brief = "";
            public string itemId = "";
            public float amount = 1f;
            public int createdTurn;
        }

        [Header("刷新与数量")]
        public int refreshCooldown = 5;
        public float rewardMultiplier = 3f;

        public RushOrder current;

        static readonly string[] Briefs =
        {
            "码头临时来了个渔贩子，急着收一批货。",
            "城里的饭馆临时加桌，后厨催得紧。",
            "对岸的船马上要走，王老板想搭一趟顺风船。",
            "有个老主顾问起你家的手艺，想先尝个鲜。",
            "集市明天开场，摊子上总得摆点像样的东西。",
            "车队改道经过镇上，王老板想趁机出一批。",
        };

        static readonly string[] FishPool =
        {
            "杂鱼", "沙丁鱼", "大黄鱼", "鲈鱼", "鲭鱼", "鲷鱼", "鲤鱼",
        };

        /// <summary>合法工艺链（每条都从生鱼开始）。生成任务时只保留「全部工序都已解锁」的链。</summary>
        static readonly FishStage[][] Chains =
        {
            new[] { FishStage.Fillet },
            new[] { FishStage.Fillet, FishStage.Dried },
            new[] { FishStage.Fillet, FishStage.Dried, FishStage.Bagged },
            new[] { FishStage.Fillet, FishStage.Breading },
            new[] { FishStage.Fillet, FishStage.Breading, FishStage.Fried },
            new[] { FishStage.Fillet, FishStage.Breading, FishStage.Fried, FishStage.Canned },
            new[] { FishStage.Fillet, FishStage.Salted },
            new[] { FishStage.Fillet, FishStage.Salted, FishStage.Sauce },
            new[] { FishStage.Fillet, FishStage.Frozen },
            new[] { FishStage.Fillet, FishStage.Smoked },
        };

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            EnsureOrder();
        }

        // ---------------- 生成 ----------------

        static bool StageUnlocked(FishStage stage)
        {
            switch (stage)
            {
                case FishStage.Fillet: return BuildCatalog.IsUnlockedByName("预处理厂");
                case FishStage.Breading: return BuildCatalog.IsUnlockedByName("成型厂");
                case FishStage.Fried: return BuildCatalog.IsUnlockedByName("油炸房");
                case FishStage.Canned: return BuildCatalog.IsUnlockedByName("灌装封口厂");
                case FishStage.Dried: return BuildCatalog.IsUnlockedByName("干燥厂");
                case FishStage.Bagged: return BuildCatalog.IsUnlockedByName("塑封分装厂");
                case FishStage.Salted:
                case FishStage.Sauce:
                case FishStage.Fermented:
                    return BuildCatalog.IsUnlockedByName("发酵熟成厂");
                case FishStage.Frozen: return BuildCatalog.IsUnlockedByName("冷冻冷藏厂");
                case FishStage.Smoked: return BuildCatalog.IsUnlockedByName("烟熏房");
                case FishStage.Oil:
                case FishStage.Meal:
                    return BuildCatalog.IsUnlockedByName("萃取精炼厂");
                default: return false;
            }
        }

        static bool ChainUnlocked(FishStage[] chain)
        {
            for (int k = 0; k < chain.Length; k++)
                if (!StageUnlocked(chain[k])) return false;
            return true;
        }

        public void EnsureOrder()
        {
            // 场景里可能存着一个空壳（序列化出来的默认对象），这种也要重新生成
            if (current != null && !string.IsNullOrEmpty(current.itemId)) return;
            current = GenerateOrder();
        }

        public RushOrder GenerateOrder()
        {
            // 只用玩家已经能生产的工艺链；一条都没有时退回「生鱼」（捕捞港默认可用）
            var available = Chains.Where(ChainUnlocked).ToList();
            FishStage[] chain = available.Count > 0
                ? available[Random.Range(0, available.Count)]
                : new FishStage[0];

            string fish = FishPool[Random.Range(0, FishPool.Length)];
            string itemId = fish;
            for (int k = 0; k < chain.Length; k++) itemId = ItemDatabase.Process(itemId, chain[k]);

            // 工艺越复杂，数量越少
            int baseAmount = Mathf.Max(3, 12 - chain.Length * 2);
            int amount = Random.Range(baseAmount, baseAmount + 5);

            int turn = TurnManager.Instance != null ? TurnManager.Instance.Turn : 1;

            return new RushOrder
            {
                title = "限时订单：" + ItemDatabase.NameOf(itemId),
                brief = Briefs[Random.Range(0, Briefs.Length)],
                itemId = itemId,
                amount = amount,
                createdTurn = turn,
            };
        }

        // ---------------- 状态 ----------------

        public int TurnsUntilRefresh
        {
            get
            {
                if (current == null) return 0;
                int turn = TurnManager.Instance != null ? TurnManager.Instance.Turn : 1;
                return Mathf.Max(0, current.createdTurn + refreshCooldown - turn);
            }
        }

        public bool CanRefresh => current != null && TurnsUntilRefresh <= 0;

        public void RefreshOrder()
        {
            if (!CanRefresh) return;
            current = GenerateOrder();
            Debug.Log("[王老板] 限时订单已刷新：" + current.title);
        }

        /// <summary>所有仓库 + 工厂暂存里的库存。</summary>
        public float Stock(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0f;
            float sum = 0f;
            foreach (var w in FindObjectsOfType<WarehouseBuilding>())
                if (w != null) sum += w.storage.AmountOf(itemId);
            foreach (var f in FindObjectsOfType<FactoryBuilding>())
                if (f != null) sum += f.outputBuffer.AmountOf(itemId);
            return sum;
        }

        public bool CanDeliver => current != null && Stock(current.itemId) >= current.amount - 1e-3f;

        public int RewardGold
        {
            get
            {
                if (current == null) return 0;
                float price = ItemDatabase.SellPrice(current.itemId, 0.7f);
                return Mathf.RoundToInt(price * current.amount * rewardMultiplier);
            }
        }

        /// <summary>完成限时订单：扣货 → 3 倍现金 → 一组土地 → 立刻换新单。</summary>
        public bool Deliver()
        {
            if (!CanDeliver) return false;

            RushOrder order = current;
            float remaining = order.amount;
            foreach (var w in FindObjectsOfType<WarehouseBuilding>())
            {
                if (remaining <= 1e-4f) break;
                remaining -= w.storage.Take(order.itemId, remaining);
            }
            foreach (var f in FindObjectsOfType<FactoryBuilding>())
            {
                if (remaining <= 1e-4f) break;
                remaining -= f.outputBuffer.Take(order.itemId, remaining);
            }

            int gold = RewardGold;
            PlayerProgress.AddGold(gold);

            if (LandOffice.Instance != null) LandOffice.Instance.GrantLand(1);

            Debug.Log($"[王老板] 限时订单完成「{order.title}」，获得 {gold} 金 + 一组土地");

            current = null;
            EnsureOrder();
            return true;
        }
    }
}
