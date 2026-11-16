using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 船况：载重 + 油量 + 收入。
    /// 钓上来的鱼先"装船"；开回码头靠岸时，船上那堆鱼会飞进冷链车（暂存，不立刻换成钱）；
    /// 入夜之后回港，才把冷链车里的货一次性结算，本局结束。
    /// </summary>
    [DisallowMultipleComponent]
    public class BoatStatus : MonoBehaviour
    {
        [Header("容量")]
        [SerializeField] float maxLoad = 150f;          // 载重上限 kg
        [SerializeField] float maxFuel = 100f;          // 油箱容量
        [SerializeField] float fuelPerDistance = 0.45f; // 每移动 1 格耗油

        [Header("码头")]
        [SerializeField] float dockX = 30f;             // 停靠点 x
        [SerializeField] float dockRange = 4f;          // 进港判定范围

        [Header("码头卸货（冷链车）")]
        [SerializeField] Transform coldTruck;           // 留空会自动找场景里的「复古像素冷藏货车侧视图」
        [SerializeField] Vector3 coldTruckAim = Vector3.zero;   // 飞到车上的落点微调
        [SerializeField, Min(0.05f)] float unloadDuration = 0.8f;

        [Header("本局")]
        [SerializeField] bool endRunAtDock = true;      // 入夜后回港 = 本局结束
        [SerializeField] bool settleOnlyAtNight = true; // 白天靠岸只卸货到冷链车，不结算
        [SerializeField] TimeOfDaySystem timeSystem;

        [Header("开阔海域模式（工厂地图进入捕鱼）")]
        [SerializeField] bool unlimitedFuel = false;    // 不耗油
        public bool Dockless { get; private set; }      // 没有码头：不回港、不结算

        /// <summary>本次出海按鱼种统计的渔获重量（回工厂时写进船背包）。</summary>
        public readonly Dictionary<string, float> CatchByName = new Dictionary<string, float>();

        public void RecordCatch(string fishName, float weight)
        {
            if (string.IsNullOrEmpty(fishName) || weight <= 0f) return;
            CatchByName.TryGetValue(fishName, out float have);
            CatchByName[fishName] = have + weight;
        }

        /// <summary>切到「没有码头」的开阔海域：不靠港、不补油、不结算。</summary>
        public void SetDockless()
        {
            Dockless = true;
            endRunAtDock = false;
            unlimitedFuel = true;
            dockX = 1e9f;
            Fuel = maxFuel;
            maxLoad = ShopData.MaxLoad * 10f;   // 海神号船舱 ×10
        }

        [Header("召唤（大鱼战斗里用）")]
        [SerializeField, Min(0)] int turtleSummonsPerTrip = 1;   // 每次出海能用几次海龟
        [SerializeField, Min(0)] int whaleSummonsPerTrip = 1;    // 鲸鱼（天赋解锁后）
        [SerializeField] bool whaleUnlocked = false;             // 天赋点解锁开关

        public float MaxLoad => maxLoad;
        public float MaxFuel => maxFuel;
        public float Load { get; private set; }

        /// <summary>把工厂船上的货带进捕鱼玩法：只同步重量与条数，不结算、不算本局渔获。</summary>
        public void SeedCargo(float load, int count)
        {
            Load = Mathf.Clamp(load, 0f, maxLoad);
            CargoCount = Mathf.Max(0, count);
        }
        public float Fuel { get; private set; }
        public int Money { get; private set; }
        public int PendingValue { get; private set; }
        public float PendingWeight { get; private set; }

        /// <summary>本次出海还剩几次海龟召唤；回码头补满。</summary>
        public int TurtleSummons { get; private set; }
        /// <summary>本次出海还剩几次鲸鱼召唤（未解锁恒为 0）。</summary>
        public int WhaleSummons { get; private set; }
        public bool WhaleUnlocked => AbilityData.WhaleUnlocked;

        /// <summary>按异能等级刷新本局可用的召唤次数。</summary>
        void RefreshSummons()
        {
            TurtleSummons = AbilityData.TurtleSummonsPerTrip;
            WhaleSummons = AbilityData.WhaleUnlocked ? AbilityData.WhaleSummonsPerTrip : 0;
        }

        public bool TryUseTurtleSummon()
        {
            if (TurtleSummons <= 0) return false;
            TurtleSummons--;
            return true;
        }

        public bool TryUseWhaleSummon()
        {
            if (!AbilityData.WhaleUnlocked || WhaleSummons <= 0) return false;
            WhaleSummons--;
            return true;
        }

        public bool LoadFull => Load >= maxLoad - 0.01f;
        public bool OutOfFuel => Fuel <= 0.01f;
        public bool AtDock => Mathf.Abs(transform.position.x - dockX) <= dockRange;
        public bool IsNight => timeSystem != null && timeSystem.IsNight;

        /// <summary>菜单「返回码头」：整仓渔获（待结算 + 已卸冷链车）全部抛弃，然后瞬移回码头。</summary>
        public void ReturnToDock()
        {
            Load = 0f;
            PendingValue = 0;
            PendingWeight = 0f;
            CargoCount = 0;
            StoredValue = 0;
            StoredWeight = 0f;
            StoredCount = 0;
            _catchCount = 0;
            SingleCatchName = null;

            var p = transform.position;
            transform.position = new Vector3(dockX, p.y, p.z);
            _lastX = dockX;
        }

        /// <summary>船舱里现在有几条鱼（卸完清零），船上的鱼堆按这个数换贴图。</summary>
        public int CargoCount { get; private set; }

        /// <summary>空船一竿就装满时记下这条鱼的名字，船上的鱼堆改成直接显示这条鱼。</summary>
        public string SingleCatchName { get; private set; }
        public bool ShowSingleCatch =>
            CargoCount == 1 && LoadFull && !string.IsNullOrEmpty(SingleCatchName);

        /// <summary>冷链车里暂存的货（还没换成钱），入夜回港一次性结算。</summary>
        public int StoredValue { get; private set; }
        public int StoredCount { get; private set; }
        public float StoredWeight { get; private set; }
        public bool Unloading { get; private set; }

        /// <summary>本局是否已经结束（入夜回港结算后为 true，船和操作都停下等结算页）。</summary>
        public bool RunOver { get; private set; }
        public int RunMoney { get; private set; }       // 本局结算后的总收入
        public int RunCatch { get; private set; }       // 本局一共钓上多少条
        public float RunSeconds { get; private set; }   // 本局用时
        public System.Action OnRunEnded;

        float _lastX;
        float _runStart;
        bool _leftDock;
        int _catchCount;
        JellyBounce _truckJelly;

        void Awake()
        {
            // 商店升级的油箱 / 载重直接覆盖场景配置
            maxFuel = ShopData.FuelCapacity;
            maxLoad = ShopData.MaxLoad;

            Fuel = maxFuel;
            _lastX = transform.position.x;
            _runStart = Time.time;
            if (timeSystem == null) timeSystem = FindObjectOfType<TimeOfDaySystem>();
            if (coldTruck == null)
            {
                var truck = GameObject.Find("复古像素冷藏货车侧视图");
                if (truck != null) coldTruck = truck.transform;
            }
            if (coldTruck != null) _truckJelly = coldTruck.GetComponent<JellyBounce>();

            RefreshSummons();
        }

        void LateUpdate()
        {
            // 移动耗油
            float dx = Mathf.Abs(transform.position.x - _lastX);
            _lastX = transform.position.x;
            if (!unlimitedFuel && dx > 0.0001f && Fuel > 0f)
                Fuel = Mathf.Max(0f, Fuel - dx * fuelPerDistance);

            if (AtDock)
            {
                if (Fuel < maxFuel) Fuel = maxFuel;      // 进港补满
                // 回码头按异能等级恢复召唤次数
                RefreshSummons();

                if (!Unloading && (PendingValue > 0 || PendingWeight > 0f || CargoCount > 0))
                    StartUnload();
            }

            if (endRunAtDock && !RunOver)
            {
                // 开局就停在港口，必须先离港一次；之后开回来才算
                if (!_leftDock && !AtDock) _leftDock = true;
                bool canSettle = !settleOnlyAtNight || IsNight;
                if (_leftDock && AtDock && canSettle && !Unloading) EndRun();
            }
        }

        /// <summary>装船：这一杆的鱼进舱（还没变成钱）。</summary>
        public void AddCatch(int value, float weight, int count = 1, string firstFishName = null)
        {
            if (value <= 0 && weight <= 0f) return;

            int before = CargoCount;
            PendingValue += value;
            PendingWeight += weight;
            Load = Mathf.Min(maxLoad, Load + weight);
            _catchCount += Mathf.Max(0, count);
            CargoCount += Mathf.Max(0, count);

            // 空船 + 这一竿只有一条鱼 + 上船就满仓 → 船上的鱼堆直接显示这条鱼
            SingleCatchName = (before == 0 && count == 1 && LoadFull && !string.IsNullOrEmpty(firstFishName))
                ? firstFishName
                : null;
        }

        /// <summary>补油（宝箱鱼开出的汽油桶）。</summary>
        public void AddFuel(float amount)
        {
            Fuel = Mathf.Min(maxFuel, Fuel + amount);
        }

        // ---------------- 码头卸货 ----------------

        void StartUnload()
        {
            Unloading = true;

            Vector3 target = coldTruck != null
                ? coldTruck.position + coldTruckAim
                : transform.position + Vector3.left * 8f;

            var pile = GetComponent<BoatFishPile>();
            if (pile == null) pile = GetComponentInChildren<BoatFishPile>();

            if (pile != null) pile.PlayUnload(target, unloadDuration, FinishUnload);
            else FinishUnload();
        }

        /// <summary>鱼堆飞到车上之后：货从船上转到冷链车（暂时不换钱）。</summary>
        void FinishUnload()
        {
            Unloading = false;

            if (PendingValue <= 0 && PendingWeight <= 0f && CargoCount <= 0) return;

            StoredValue += PendingValue;
            StoredWeight += PendingWeight;
            StoredCount += CargoCount;

            Debug.Log("[冷链车] 卸货 " + CargoCount + " 条 / " + PendingWeight.ToString("0.0") +
                      "kg（车里暂存 " + StoredCount + " 条，" + StoredValue + " 金，入夜回港结算）");

            PendingValue = 0;
            PendingWeight = 0f;
            Load = 0f;
            CargoCount = 0;

            // 鱼砸进车厢的那一下：车来个果冻弹性
            if (_truckJelly != null) _truckJelly.Play();
        }

        /// <summary>
        /// 入夜回港 = 本局结束：把冷链车里的货一次性卖掉。
        /// 结算页读 RunMoney / RunCatch / RunSeconds，或接 <see cref="OnRunEnded"/>。
        /// </summary>
        void EndRun()
        {
            if (RunOver) return;
            RunOver = true;

            if (StoredValue > 0 || StoredWeight > 0f || StoredCount > 0)
            {
                Money += StoredValue;
                Debug.Log("[冷链结算] 卖出 " + StoredCount + " 条 / " + StoredWeight.ToString("0.0") +
                          "kg，+" + StoredValue + " 金（累计 " + Money + "）");
                StoredValue = 0;
                StoredWeight = 0f;
                StoredCount = 0;
            }

            RunSeconds = Time.time - _runStart;
            RunMoney = Money;
            RunCatch = _catchCount;

            GameAudio.Ensure().PlayEndRun(0.5f);   // 结算音降一半，别太响

            Debug.Log("[本局结束] 用时 " + RunSeconds.ToString("0") + "s，渔获 " + RunCatch +
                      " 条，收入 " + RunMoney + " 金");
            OnRunEnded?.Invoke();
        }

        /// <summary>开下一局（结算页调）。</summary>
        public void RestartRun()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
        }
    }
}
