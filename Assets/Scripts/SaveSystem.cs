using System.IO;
using UnityEngine;

namespace Shenyan
{
    /// <summary>存档内容：金币/等级/经验/异能点、异能等级、商店装备与道具。</summary>
    [System.Serializable]
    public class SaveData
    {
        public int gold;
        public int level = 1;
        public int exp;
        public int skillPoints;

        public int eyeLevel;
        public int turtleLevel;
        public int whaleLevel = -1;
        public int intimidateLevel;
        public int eyeFarLevel;
        public int qualityLevel;
        public int terrainLevel;

        public int fuelTank;
        public int motor;
        public int load;
        public int hook;
        public int torpedoes;
        public int baits;

        public bool introPlayed;

        /// <summary>授勋漫画（捞齐 36 条沉船）是否已经播过。</summary>
        public bool nationalTreasureComicPlayed;

        /// <summary>是否已经从王老板那里买了海神号（渔村背景与工厂入口都看它）。</summary>
        public bool shipPurchased;

        /// <summary>工厂玩法的新手教程看过没有（开局建造 / 地块组拼接 / 电力）。</summary>
        public bool tutorialFactory;
        public bool tutorialLand;
        public bool tutorialPower;
        public bool tutorialSail;

        /// <summary>第一次进工厂玩法时的启动资金发过没有。</summary>
        public bool factoryStarterGranted;

        /// <summary>已经解锁的建造工艺名（王老板订单解锁的那些，读档要补回来）。</summary>
        public System.Collections.Generic.List<string> unlockedBuildings
            = new System.Collections.Generic.List<string>();

        /// <summary>贵重品库存（沉船打捞所得，不占重量）。</summary>
        public System.Collections.Generic.List<TreasureEntry> treasures
            = new System.Collections.Generic.List<TreasureEntry>();

        /// <summary>海底沉船 / 宝藏的坐标与状态（一次生成，永久跟随存档）。</summary>
        public System.Collections.Generic.List<WreckSiteSystem.Site> wreckSites
            = new System.Collections.Generic.List<WreckSiteSystem.Site>();

        /// <summary>工厂玩法：地形、回合、建筑、库存、订单、船、进度。</summary>
        public FactorySaveData factory = new FactorySaveData();
    }

    /// <summary>
    /// 自动存档：写 Application.persistentDataPath/save.json。
    /// 买卖、加点、完成一局都会调用 Save()；进入游戏时自动读一次。
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "save.json";

        /// <summary>开场漫画是否已经看过（只有第一次开新游戏才播）。</summary>
        public static bool IntroPlayed;

        /// <summary>授勋漫画（捞齐 36 条沉船）是否已经看过。</summary>
        public static bool NationalTreasureComicPlayed;

        /// <summary>是否已经买了海神号。</summary>
        public static bool ShipPurchased;

        /// <summary>第一次进工厂玩法时的启动资金是否已经发过。</summary>
        public static bool FactoryStarterGranted;

        /// <summary>工厂开局建筑教程是否已经看完。</summary>
        public static bool FactoryBasicTutorialDone;

        /// <summary>地块组拼接教程是否已经看过。</summary>
        public static bool FactoryLandTutorialDone;

        /// <summary>电力与电力设施教程是否已经看过。</summary>
        public static bool FactoryPowerTutorialDone;

        /// <summary>远洋教程（航线 / 沉船宝藏 / 贵重品升异能 / 通关目标）是否已经看过。</summary>
        public static bool FactorySailTutorialDone;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save()
        {
            var d = new SaveData
            {
                gold = PlayerProgress.Gold,
                level = PlayerProgress.Level,
                exp = PlayerProgress.Exp,
                skillPoints = PlayerProgress.SkillPoints,

                eyeLevel = AbilityData.EyeLevel,
                turtleLevel = AbilityData.TurtleLevel,
                whaleLevel = AbilityData.WhaleLevel,
                intimidateLevel = AbilityData.IntimidateLevel,
                eyeFarLevel = AbilityData.EyeFarLevel,
                qualityLevel = AbilityData.QualityLevel,
                terrainLevel = AbilityData.TerrainLevel,

                fuelTank = ShopData.FuelTankLevel,
                motor = ShopData.MotorLevel,
                load = ShopData.LoadLevel,
                hook = ShopData.HookLevel,
                torpedoes = ShopData.Torpedoes,
                baits = ShopData.Baits,

                introPlayed = IntroPlayed,
                nationalTreasureComicPlayed = NationalTreasureComicPlayed,
                shipPurchased = ShipPurchased,
                tutorialFactory = FactoryBasicTutorialDone,
                tutorialLand = FactoryLandTutorialDone,
                tutorialPower = FactoryPowerTutorialDone,
                tutorialSail = FactorySailTutorialDone,
                factoryStarterGranted = FactoryStarterGranted,
                unlockedBuildings = BuildCatalog.SnapshotUnlocked(),
                wreckSites = WreckSiteSystem.SnapshotSites(),
                treasures = PlayerProgress.SnapshotTreasures(),
                factory = FactorySaveSystem.Capture() ?? FactorySaveSystem.Cached ?? new FactorySaveData(),
            };

            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(d, true));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[存档] 保存失败: " + e.Message);
            }
        }

        public static void Load()
        {
            if (!File.Exists(SavePath)) return;

            try
            {
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (d == null) return;

                PlayerProgress.Gold = d.gold;
                PlayerProgress.Level = Mathf.Max(1, d.level);
                PlayerProgress.Exp = Mathf.Max(0, d.exp);
                PlayerProgress.SkillPoints = Mathf.Max(0, d.skillPoints);

                AbilityData.EyeLevel = Mathf.Clamp(d.eyeLevel, 0, AbilityData.EyeMax);
                AbilityData.TurtleLevel = Mathf.Clamp(d.turtleLevel, 0, AbilityData.TurtleMax);
                AbilityData.WhaleLevel = Mathf.Clamp(d.whaleLevel, -1, AbilityData.WhaleMax);
                AbilityData.IntimidateLevel = Mathf.Clamp(d.intimidateLevel, 0, AbilityData.IntimidateMax);
                AbilityData.EyeFarLevel = Mathf.Clamp(d.eyeFarLevel, 0, AbilityData.EyeFarMax);
                AbilityData.QualityLevel = Mathf.Clamp(d.qualityLevel, 0, AbilityData.QualityMax);
                AbilityData.TerrainLevel = Mathf.Clamp(d.terrainLevel, 0, AbilityData.TerrainMax);

                ShopData.FuelTankLevel = Mathf.Clamp(d.fuelTank, 0, ShopData.MaxLevel);
                ShopData.MotorLevel = Mathf.Clamp(d.motor, 0, ShopData.MaxLevel);
                ShopData.LoadLevel = Mathf.Clamp(d.load, 0, ShopData.MaxLevel);
                ShopData.HookLevel = Mathf.Clamp(d.hook, 0, ShopData.MaxLevel);
                ShopData.Torpedoes = Mathf.Max(0, d.torpedoes);
                ShopData.Baits = Mathf.Max(0, d.baits);

                IntroPlayed = d.introPlayed;
                NationalTreasureComicPlayed = d.nationalTreasureComicPlayed;
                ShipPurchased = d.shipPurchased;
                FactoryBasicTutorialDone = d.tutorialFactory;
                FactoryLandTutorialDone = d.tutorialLand;
                FactoryPowerTutorialDone = d.tutorialPower;
                FactorySailTutorialDone = d.tutorialSail;
                FactoryStarterGranted = d.factoryStarterGranted;
                BuildCatalog.ApplyUnlocked(d.unlockedBuildings);

                WreckSiteSystem.ApplySites(d.wreckSites);
                PlayerProgress.ApplyTreasures(d.treasures);
                FactorySaveSystem.StorePending(d.factory);   // 等工厂场景加载时再重建

                Debug.Log("[存档] 已读取 " + SavePath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[存档] 读取失败: " + e.Message);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadOnBoot()
        {
            Load();
        }
    }
}
