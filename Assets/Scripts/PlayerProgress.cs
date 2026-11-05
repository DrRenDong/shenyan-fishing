using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 玩家成长数据：金币、等级、经验、异能点。跨场景保留（静态字段）。
    /// 经验只看渔获条数，和重量、大小无关；升级需求按指数增长。
    /// </summary>
    public static class PlayerProgress
    {
        public static int Gold;
        public static int Level = 1;
        public static int Exp;
        public static int SkillPoints;

        /// <summary>连击第 1 条鱼的基础经验（后续按连击指数增长）。</summary>
        public const int BaseExpPerFish = 3;

        /// <summary>连击每多一条，经验乘以这个系数（指数增长）。</summary>
        public const float ComboExpGrowth = 1.35f;

        /// <summary>第 combo 条连击（从 1 开始）能拿到多少经验。</summary>
        public static int ExpForCombo(int combo)
        {
            combo = Mathf.Max(1, combo);
            return Mathf.RoundToInt(BaseExpPerFish * Mathf.Pow(ComboExpGrowth, combo - 1));
        }

        /// <summary>升到下一级需要的经验（指数增长：18 × 1.45^(等级-1)）。</summary>
        public static int ExpToNext => Mathf.RoundToInt(18f * Mathf.Pow(1.45f, Level - 1));

        public static float ExpRatio => ExpToNext > 0 ? Mathf.Clamp01((float)Exp / ExpToNext) : 1f;

        public static void AddGold(int amount)
        {
            Gold = Mathf.Max(0, Gold + amount);
        }

        /// <summary>给某一条鱼加经验（combo = 它在本竿里的第几条）。返回升了几级。</summary>
        public static int GainExpForCombo(int combo)
        {
            if (combo <= 0) return 0;

            Exp += ExpForCombo(combo);
            int levels = 0;
            while (Exp >= ExpToNext)
            {
                Exp -= ExpToNext;
                Level++;
                SkillPoints++;
                levels++;
            }
            return levels;
        }

        /// <summary>兼容旧调用：按条数直接加经验（不再走连击曲线）。</summary>
        public static int GainExp(int fishCount)
        {
            if (fishCount <= 0) return 0;
            int levels = 0;
            for (int i = 1; i <= fishCount; i++) levels += GainExpForCombo(i);
            return levels;
        }
    }
}
