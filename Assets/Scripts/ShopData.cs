using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 渔具商店：四项装备升级（各 10 级上限）+ 鱼雷 / 饵料两种道具。
    /// 等级和道具数都是静态字段，和金币一样跨场景保留。
    /// </summary>
    public static class ShopData
    {
        public const int MaxLevel = 10;

        public static int FuelTankLevel;   // 油箱
        public static int MotorLevel;      // 马达
        public static int LoadLevel;       // 载重
        public static int HookLevel;      // 鱼钩（串鱼数上限）

        public static int Torpedoes;       // 鱼雷数量
        public static int Baits;           // 饵料数量

        // ---- 效果 ----
        public static float FuelCapacity => 100f + FuelTankLevel * 12f;
        public static float MotorSpeed => 6f + MotorLevel * 0.3f;
        public static float MaxLoad => 150f + LoadLevel * 18f;
        public static int MaxSmallFish => 8 + HookLevel;

        // ---- 价格 ----
        static readonly int[] BasePrice = { 200, 300, 260, 180 };

        public static int UpgradeCost(int index)
        {
            int level = LevelOf(index);
            return Mathf.RoundToInt(BasePrice[index] * Mathf.Pow(1.55f, level));
        }

        public static int TorpedoPrice => 400;
        public static int BaitPrice => 120;

        public static int LevelOf(int index)
        {
            switch (index)
            {
                case 0: return FuelTankLevel;
                case 1: return MotorLevel;
                case 2: return LoadLevel;
                default: return HookLevel;
            }
        }

        public static bool TryBuyUpgrade(int index)
        {
            if (LevelOf(index) >= MaxLevel) return false;
            int cost = UpgradeCost(index);
            if (PlayerProgress.Gold < cost) return false;

            PlayerProgress.AddGold(-cost);
            switch (index)
            {
                case 0: FuelTankLevel++; break;
                case 1: MotorLevel++; break;
                case 2: LoadLevel++; break;
                default: HookLevel++; break;
            }
            SaveSystem.Save();
            return true;
        }

        public static bool TryBuyTorpedo()
        {
            if (PlayerProgress.Gold < TorpedoPrice) return false;
            PlayerProgress.AddGold(-TorpedoPrice);
            Torpedoes++;
            SaveSystem.Save();
            return true;
        }

        public static bool TryBuyBait()
        {
            if (PlayerProgress.Gold < BaitPrice) return false;
            PlayerProgress.AddGold(-BaitPrice);
            Baits++;
            SaveSystem.Save();
            return true;
        }

        /// <summary>商店列表用的四行文案。</summary>
        public static string Describe(int index)
        {
            switch (index)
            {
                case 0: return $"油量 {FuelCapacity:0}";
                case 1: return $"航速 {MotorSpeed:0.00}";
                case 2: return $"载重 {MaxLoad:0}";
                default: return $"串鱼 {MaxSmallFish} 条";
            }
        }

        public static string TitleOf(int index)
        {
            switch (index)
            {
                case 0: return "油箱";
                case 1: return "马达";
                case 2: return "载重";
                default: return "鱼钩";
            }
        }
    }
}
