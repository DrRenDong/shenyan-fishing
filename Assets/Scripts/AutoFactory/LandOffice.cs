using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 土地局：主角有钱也不能随便买地，必须先证明运营能力。
    /// 每完成一个任务（累计产出 / 捕捞 / 建厂）就批一组地块：给 3 组选 1 组，
    /// 选好后进入「安放地块」模式，贴到现有陆地边上填海造陆。
    /// </summary>
    public class LandOffice : MonoBehaviour
    {
        public static LandOffice Instance { get; private set; }

        [System.Serializable]
        public class Quest
        {
            public string title = "";
            [Tooltip("produce=累计产出加工品 / catch=累计捕捞 / factories=工厂数量 / gold=金币")]
            public string kind = "produce";
            public float target = 50f;
        }

        [Header("任务链（完成一个批一组地）")]
        public List<Quest> quests = new List<Quest>();

        [Tooltip("地块现在主要由王老板的订单发放；这里默认关闭旧任务链，避免重复发地")]
        public bool autoQuestChain = false;

        [Header("每次批复给几组备选")]
        public int offerCount = 3;

        /// <summary>已完成的任务数（也是已获批的土地组数）。</summary>
        public int CompletedCount { get; private set; }

        /// <summary>读档用：直接设置已完成任务数。</summary>
        public void SetCompletedCount(int count)
        {
            int max = quests != null ? quests.Count : 0;
            CompletedCount = Mathf.Clamp(count, 0, max);
            _grantedForQuest = CompletedCount;
        }

        /// <summary>正在等玩家选地 / 放地。</summary>
        public bool Waiting { get; private set; }

        float _timer;
        int _grantedForQuest = -1;   // 已经为第几个任务批过地（玩家取消后不再自动重弹）
        int _pendingGrants;          // 还欠玩家几组地块（王老板订单奖励一次给多组）

        void Awake()
        {
            Instance = this;
            if (autoQuestChain && quests.Count == 0) quests.AddRange(DefaultQuests());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static IEnumerable<Quest> DefaultQuests()
        {
            yield return new Quest { title = "累计产出加工品 30KG", kind = "produce", target = 30f };
            yield return new Quest { title = "累计捕捞原料 100KG", kind = "catch", target = 100f };
            yield return new Quest { title = "累计产出加工品 150KG", kind = "produce", target = 150f };
            yield return new Quest { title = "累计捕捞原料 400KG", kind = "catch", target = 400f };
            yield return new Quest { title = "建成 6 座工厂", kind = "factories", target = 6f };
            yield return new Quest { title = "累计产出加工品 600KG", kind = "produce", target = 600f };
        }

        void Update()
        {
            // 半秒查一次，够用也不费
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.5f;

            if (Waiting || CompletedCount >= quests.Count) return;
            if (_grantedForQuest == CompletedCount) return;   // 这个任务的批复已经给过（玩家取消了），等下一个任务
            Quest quest = quests[CompletedCount];
            if (ProgressOf(quest) >= quest.target) BeginGrant();
        }

        public float ProgressOf(Quest quest)
        {
            if (quest == null) return 0f;
            switch (quest.kind)
            {
                case "produce":
                    return FactorySim.Instance != null ? FactorySim.Instance.TotalCansProduced : 0f;
                case "catch":
                    return FactorySim.Instance != null ? FactorySim.Instance.TotalFishCaught : 0f;
                case "factories":
                    return FindObjectsOfType<FactoryBuilding>().Length;
                case "gold":
                    return PlayerProgress.Gold;
                default:
                    return 0f;
            }
        }

        /// <summary>给 HUD 显示的一行状态。</summary>
        public string CurrentStatus()
        {
            if (CompletedCount >= quests.Count) return "土地局：所有审批已完成";
            if (Waiting) return "土地局：正在等你安排新地块";

            Quest quest = quests[CompletedCount];
            float p = ProgressOf(quest);
            return $"土地局：{quest.title}（{Mathf.FloorToInt(p)}/{quest.target:0}）";
        }

        /// <summary>直接发若干组地块（王老板订单的报酬）：一组一组排队让玩家选。</summary>
        public void GrantLand(int count)
        {
            if (count <= 0) return;
            _pendingGrants = count;
            if (!Waiting) BeginGrant();
        }

        /// <summary>跳过等待（调试/测试用）：立刻当成完成一个任务并批复。</summary>
        public void ForceComplete()
        {
            if (Waiting) return;
            _grantedForQuest = -1;   // 手动补发：允许再批一次
            BeginGrant();
        }

        void BeginGrant()
        {
            var panel = LandGrantPanel.Instance;
            var placement = LandPlacementController.Instance;
            if (panel == null || placement == null)
            {
                Debug.LogWarning("[土地局] 缺少 LandGrantPanel / LandPlacementController，无法批复地块");
                return;
            }

            Waiting = true;
            _grantedForQuest = CompletedCount;

            int n = Mathf.Max(1, offerCount);
            var offers = new LandGrant[n];
            for (int k = 0; k < n; k++) offers[k] = LandGrant.Random();

            panel.Open(offers, grant =>
            {
                placement.Begin(grant, () =>
                {
                    CompletedCount++;
                    Waiting = false;
                    Debug.Log($"[土地局] 第 {CompletedCount} 组地块已落地，下一个任务：{CurrentStatus()}");

                    if (_pendingGrants > 0)
                    {
                        _pendingGrants--;
                        if (_pendingGrants > 0) BeginGrant();
                    }
                },
                () =>
                {
                    // 玩家取消了放置：解冻，等把这一轮任务重新做完/下次再批复
                    Waiting = false;
                    _pendingGrants = 0;
                    Debug.Log("[土地局] 玩家取消了地块放置");
                });
            },
            () =>
            {
                // 玩家直接关掉选地面板：同样解冻
                Waiting = false;
                _pendingGrants = 0;
                Debug.Log("[土地局] 玩家关掉了选地面板");
            });
        }
    }
}
