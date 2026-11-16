using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>存档里的一条贵重品记录（JsonUtility 不支持字典，用列表存）。</summary>
    [System.Serializable]
    public class TreasureEntry
    {
        public string id;
        public int count;
    }

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

        /// <summary>第一次进工厂玩法时发的启动资金（不再在游戏启动时发）。</summary>
        public const int StartingGold = 3000;

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

        // ---------------- 贵重品（沉船打捞） ----------------

        /// <summary>贵重品库存：不占重量，后续作为升级材料（随存档保存）。</summary>
        public static readonly Dictionary<string, int> Treasures = new Dictionary<string, int>();

        public static void AddTreasure(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return;
            Treasures.TryGetValue(id, out int have);
            Treasures[id] = have + count;
        }

        /// <summary>扣除贵重品（纠错 / 回退用），不足就扣到 0。</summary>
        public static void RemoveTreasure(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return;
            Treasures.TryGetValue(id, out int have);
            int left = Mathf.Max(0, have - count);
            if (left == 0) Treasures.Remove(id);
            else Treasures[id] = left;
        }

        public static int TreasureCount(string id)
        {
            return !string.IsNullOrEmpty(id) && Treasures.TryGetValue(id, out int have) ? have : 0;
        }

        /// <summary>升级消耗贵重品：不够就不扣、返回 false。</summary>
        public static bool TrySpendTreasure(string id, int count)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return false;
            if (!Treasures.TryGetValue(id, out int have) || have < count) return false;
            if (have == count) Treasures.Remove(id);
            else Treasures[id] = have - count;
            return true;
        }

        public static int TotalTreasures
        {
            get
            {
                int sum = 0;
                foreach (var kv in Treasures) sum += kv.Value;
                return sum;
            }
        }

        /// <summary>存档用：把贵重品字典拍平成列表（JsonUtility 不支持字典）。</summary>
        public static List<TreasureEntry> SnapshotTreasures()
        {
            var list = new List<TreasureEntry>(Treasures.Count);
            foreach (var kv in Treasures)
                list.Add(new TreasureEntry { id = kv.Key, count = kv.Value });
            return list;
        }

        public static void ApplyTreasures(List<TreasureEntry> saved)
        {
            Treasures.Clear();
            if (saved == null) return;
            foreach (var e in saved)
            {
                if (e == null || string.IsNullOrEmpty(e.id) || e.count <= 0) continue;
                Treasures[e.id] = e.count;
            }
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
