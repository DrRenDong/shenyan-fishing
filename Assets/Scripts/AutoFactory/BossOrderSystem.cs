using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>王老板的一张特殊订单：要什么、要多少、给多少区块、解锁哪项工艺。</summary>
    [System.Serializable]
    public class BossOrder
    {
        public string title = "";
        public string brief = "";
        public string itemId = "";
        public float amount = 1f;
        public int blocks = 1;
        public string unlockName = "";
        [Tooltip("这一单还额外解锁的建造项（可以一次给多项）")]
        public List<string> extraUnlocks = new List<string>();
    }

    /// <summary>
    /// 王老板的特殊订单：交付后土地局自然看得到玩家的经营能力。
    /// 每次只给一张单，需求随工艺难度上升；报酬只有区块（土地）与科技解锁，没有现金。
    /// </summary>
    public class BossOrderSystem : MonoBehaviour
    {
        public static BossOrderSystem Instance { get; private set; }

        [Header("订单链（交付一张解锁下一张）")]
        public List<BossOrder> orders = new List<BossOrder>();

        /// <summary>已完成到第几张。</summary>
        public int Index { get; private set; }

        /// <summary>读档用：直接设置进度，并把已完成的订单对应的科技补解锁
        /// （解锁表是静态的，不补的话读档后所有工厂又变回「未解锁」）。</summary>
        public void SetIndex(int index)
        {
            int max = orders != null ? orders.Count : 0;
            Index = Mathf.Clamp(index, 0, max);

            for (int k = 0; k < Index && k < max; k++) ApplyUnlocks(orders[k]);
        }

        /// <summary>把某张订单的解锁项放进建造目录。</summary>
        public static void ApplyUnlocks(BossOrder order)
        {
            if (order == null) return;
            if (!string.IsNullOrEmpty(order.unlockName)) BuildCatalog.Unlock(order.unlockName);
            if (order.extraUnlocks == null) return;
            for (int k = 0; k < order.extraUnlocks.Count; k++)
                BuildCatalog.Unlock(order.extraUnlocks[k]);
        }

        public BossOrder Current => Index >= 0 && Index < orders.Count ? orders[Index] : null;
        public bool AllDone => Index >= orders.Count;

        void Awake()
        {
            Instance = this;
            if (orders.Count == 0) orders.AddRange(DefaultOrders());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------- 默认订单链 ----------------

        static IEnumerable<BossOrder> DefaultOrders()
        {
            yield return new BossOrder
            {
                title = "第一单：新鲜杂鱼",
                brief = "你海上那手本事，王老板早就见识过了。这回他想看的是工厂那头——先从稳定的杂鱼货源开始。",
                itemId = ItemDatabase.JunkFish,
                amount = 10f, blocks = 2, unlockName = "分拣区",
            };
            yield return new BossOrder
            {
                title = "第二单：沙丁鱼",
                brief = "杂鱼卖得不错。王老板要挑个头匀称的沙丁鱼，看看你分拣得细不细。",
                itemId = ItemDatabase.Sardine,
                amount = 2f, blocks = 1, unlockName = "预处理厂",
            };
            yield return new BossOrder
            {
                title = "第三单：剖好的大黄鱼",
                brief = "样品得干净漂亮。把大黄鱼剖好、去内脏，鱼身完整地送过来。",
                itemId = ItemDatabase.Process("大黄鱼", FishStage.Fillet),
                amount = 3f, blocks = 1, unlockName = "成型厂",
            };
            yield return new BossOrder
            {
                title = "第四单：裹粉杂鱼",
                brief = "王老板想试个新产品——裹上粉的杂鱼，他拿去炸着卖。",
                itemId = ItemDatabase.Process(ItemDatabase.Process("杂鱼", FishStage.Fillet), FishStage.Breading),
                amount = 5f, blocks = 2, unlockName = "烟熏房",
            };
            yield return new BossOrder
            {
                title = "第五单：熏大黄鱼",
                brief = "熏杂鱼打开了销路。王老板想试试整条的熏大黄鱼——顺便听说你厂子老是跳电，他愿意帮你搭两条远程输电的线。",
                itemId = ItemDatabase.Process(ItemDatabase.Process("大黄鱼", FishStage.Fillet), FishStage.Smoked),
                amount = 4f, blocks = 2, unlockName = "接收塔",
                extraUnlocks = new List<string> { "棱镜塔" },
            };
            yield return new BossOrder
            {
                title = "第六单：烟熏杂鱼",
                brief = "熏味压住了杂鱼的腥，客人很买账。再来一批。",
                itemId = ItemDatabase.Process(ItemDatabase.Process("杂鱼", FishStage.Fillet), FishStage.Smoked),
                amount = 10f, blocks = 2, unlockName = "油炸房",
            };
            yield return new BossOrder
            {
                title = "第七单：油炸大黄鱼",
                brief = "码头边的排档要一批炸鱼，外酥里嫩那种。",
                itemId = ItemDatabase.Process(ItemDatabase.Process(ItemDatabase.Process("大黄鱼", FishStage.Fillet), FishStage.Breading), FishStage.Fried),
                amount = 5f, blocks = 2, unlockName = "干燥厂",
            };
            yield return new BossOrder
            {
                title = "第八单：大黄鱼干",
                brief = "干货耐放好运输，王老板要囤一批过冬。",
                itemId = ItemDatabase.Process(ItemDatabase.Process("大黄鱼", FishStage.Fillet), FishStage.Dried),
                amount = 8f, blocks = 3, unlockName = "塑封分装厂",
            };
            yield return new BossOrder
            {
                title = "第九单：袋装大黄鱼",
                brief = "合作社要上超市货架，得真空袋装、贴上标签。",
                itemId = ItemDatabase.Process(ItemDatabase.Process(ItemDatabase.Process("大黄鱼", FishStage.Fillet), FishStage.Dried), FishStage.Bagged),
                amount = 5f, blocks = 3, unlockName = "灌装封口厂",
            };
            yield return new BossOrder
            {
                title = "第十单：大黄鱼罐头",
                brief = "外地客商点名要罐头，能放半年还能运。",
                itemId = ItemDatabase.Process(ItemDatabase.Process(ItemDatabase.Process(ItemDatabase.Process("大黄鱼", FishStage.Fillet), FishStage.Breading), FishStage.Fried), FishStage.Canned),
                amount = 3f, blocks = 3, unlockName = "发酵熟成厂",
            };
            yield return new BossOrder
            {
                title = "第十一单：腌制大黄鱼",
                brief = "老主顾念着小时候的咸鱼味，要腌足时辰的那种。",
                itemId = ItemDatabase.Process(ItemDatabase.Process("大黄鱼", FishStage.Fillet), FishStage.Salted),
                amount = 5f, blocks = 3, unlockName = "萃取精炼厂",
            };
            yield return new BossOrder
            {
                title = "第十二单：大黄鱼露",
                brief = "酒楼师傅要用鱼露吊鲜，越纯越好。",
                itemId = ItemDatabase.Process(ItemDatabase.Process(ItemDatabase.Process("大黄鱼", FishStage.Fillet), FishStage.Salted), FishStage.Sauce),
                amount = 3f, blocks = 4, unlockName = "冷冻冷藏厂",
            };
            yield return new BossOrder
            {
                title = "第十三单：冷冻大黄鱼",
                brief = "最后一单：远洋货轮要一批冷冻鲜鱼，直发北方。",
                itemId = ItemDatabase.Process(ItemDatabase.Process("大黄鱼", FishStage.Fillet), FishStage.Frozen),
                amount = 8f, blocks = 4, unlockName = "",
            };
        }

        // ---------------- 库存与交付 ----------------

        /// <summary>所有仓库 + 工厂暂存里的库存（交付统计用）。</summary>
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

        public bool CanDeliver()
        {
            BossOrder order = Current;
            return order != null && Stock(order.itemId) >= order.amount - 1e-3f;
        }

        /// <summary>交付当前订单：扣货 → 发区块 → 解锁科技。</summary>
        public bool Deliver()
        {
            BossOrder order = Current;
            if (order == null || !CanDeliver()) return false;

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

            Debug.Log($"[王老板] 交付「{order.title}」×{order.amount}，报酬：区块×{order.blocks}" +
                      (string.IsNullOrEmpty(order.unlockName) ? "" : "，解锁" + order.unlockName));

            // 统一走 ApplyUnlocks（解锁名 + 额外解锁）
            ApplyUnlocks(order);

            Index++;
            if (order.blocks > 0 && LandOffice.Instance != null)
                LandOffice.Instance.GrantLand(order.blocks);

            return true;
        }
    }
}
