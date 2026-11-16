using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 回合控制：按空格或点「下一回合」推进；每回合时间前进 1 游戏小时，24 回合 = 1 日。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        public int Turn { get; private set; } = 1;
        public KeyCode endTurnKey = KeyCode.Space;

        /// <summary>读档用：直接设置回合数。</summary>
        public void SetTurn(int turn) => Turn = Mathf.Max(1, turn);

        public bool Busy { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Input.GetKeyDown(endTurnKey)) EndTurn();
        }

        /// <summary>同一帧里只允许推进一次（键盘空格 + 按钮被选中后按空格会双触发）</summary>
        int _lastEndTurnFrame = -1;

        /// <summary>推进一回合：时间 +1 小时，所有工厂/码头/捕捞港走一步。</summary>
        public void EndTurn()
        {
            if (Busy) return;

            // 点过一次「下一回合」按钮之后，uGUI 按钮会保持选中，
            // 之后按空格除了键盘输入，还会再触发一次按钮的 onClick → 一次跳两回合。
            // 这里清掉选中，并做同帧去重。
            if (Time.frameCount == _lastEndTurnFrame) return;
            _lastEndTurnFrame = Time.frameCount;

            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            Busy = true;
            try
            {
                Turn++;
                if (FactorySim.Instance != null) FactorySim.Instance.AdvanceTurn();
                else GameClock.AdvanceHours(GameClock.HoursPerTurn);

                foreach (var dock in FindObjectsOfType<CargoDockBuilding>())
                    dock.AdvanceTurn();

                var boat = FindObjectOfType<PlayerBoatVehicle>();
                if (boat != null)
                {
                    boat.AdvanceTurn();
                    boat.UnloadToWarehouses();   // 贴着路面/仓库就把船舱里的渔获送进仓
                }

                var streamer = FindObjectOfType<SeaStreamer>();
                if (streamer != null) streamer.Refresh();

                // 海产聚集点：先按概率埋新点，再让刚移动过的神眼视野去探查一次
                var spawner = FindObjectOfType<SeaSpotSpawner>();
                if (spawner != null)
                {
                    spawner.AdvanceTurn();
                    spawner.RevealInView();
                }

                // 沉船 / 宝藏：同样是神眼扫到才显形
                var wreck = FindObjectOfType<WreckSiteSystem>();
                if (wreck != null) wreck.RevealInView();

                // 拆掉的建筑留下的泥土，过几回合长回草地
                if (BuildController.Instance != null) BuildController.Instance.AdvanceTurn();

                // 电力图：电流束每 N 回合重新随机
                var power = FindObjectOfType<PowerGridView>();
                if (power != null) power.AdvanceTurn();
            }
            finally
            {
                Busy = false;
            }

            // 每回合自动存档（地形 / 建筑 / 库存 / 订单 / 船 / 回合都写进去）
            SaveSystem.Save();
        }

        public string DescribeTime()
        {
            return $"第 {Turn} 日";
        }
    }
}
