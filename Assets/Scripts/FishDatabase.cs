using UnityEngine;
using System.Collections.Generic;
namespace Shenyan
{
    public enum FishBehavior
    {
        Swim,     // 在水中游动
        Bottom    // 贴在海床上不动（蟹、螺、鲍、蚌类、底栖鱼）
    }

    /// <summary>
    /// 鱼类图鉴：体型等级、估价、出场水深、平均重量、行为。
    /// 近海（水深 &lt; 6）只出 150 以内的便宜货，越深才越有贵鱼和奇珍。
    /// </summary>
    public static class FishDatabase
    {
        public struct Info
        {
            public int level;           // 体型等级 1~5
            public int value;           // 估价（金币）
            public float minDepth;      // 至少多深的水才出现（水面 15.5 - 海床顶面）
            public float weight;        // 平均重量（kg）
            public FishBehavior behavior;
        }

        static Info I(int level, int value, float minDepth, float weight, FishBehavior b)
        {
            return new Info { level = level, value = value, minDepth = minDepth, weight = weight, behavior = b };
        }

        static readonly System.Collections.Generic.Dictionary<string, Info> Table =
            new System.Collections.Generic.Dictionary<string, Info>
        {
            // ---------- 1 级 ----------
            { "沙丁鱼",     I(1,    8, 0f,  0.1f, FishBehavior.Swim) },

            // ---------- 2 级：小型常见 ----------
            { "小丑鱼",     I(2,   30, 0f,  0.02f, FishBehavior.Swim) },
            { "水母",       I(2,   20, 0f,  0.5f,  FishBehavior.Swim) },
            { "螃蟹",       I(2,   45, 0f,  0.3f,  FishBehavior.Bottom) },
            { "海星",       I(2,   15, 0f,  0.2f,  FishBehavior.Bottom) },
            { "海马",       I(2,   60, 1f,  0.01f, FishBehavior.Bottom) },
            { "彩虹鱼",     I(2,   90, 1f,  0.05f, FishBehavior.Swim) },
            { "宝石鱼",     I(2,  120, 2f,  0.1f,  FishBehavior.Swim) },
            { "红鲟",       I(2,  300, 3f,  1f,    FishBehavior.Bottom) },
            { "竹节虾",     I(2,  150, 3f,  0.1f,  FishBehavior.Bottom) },
            { "幽灵鱼",     I(2,  250, 5f,  1f,    FishBehavior.Swim) },
            { "宝箱鱼",     I(2,  400, 6f,  1.5f,  FishBehavior.Bottom) },
            { "金币鱼",     I(2,  500, 7f,  2f,    FishBehavior.Bottom) },

            // ---------- 3 级：常见食用鱼 + 中型珍品 ----------
            { "鲤鱼",       I(3,   80, 0f,  2f,    FishBehavior.Swim) },
            { "珊瑚鱼",     I(3,   60, 1f,  0.3f,  FishBehavior.Bottom) },
            { "鲭鱼",       I(3,   90, 1f,  0.5f,  FishBehavior.Swim) },
            { "鲈鱼",       I(3,  120, 2f,  1.5f,  FishBehavior.Swim) },
            { "鲷鱼",       I(3,  150, 2f,  1.2f,  FishBehavior.Swim) },
            { "鳗鱼",       I(3,  100, 2f,  1f,    FishBehavior.Swim) },
            { "鱿鱼",       I(3,   70, 2f,  0.8f,  FishBehavior.Swim) },
            { "大黄鱼",     I(3,  500, 4f,  3f,    FishBehavior.Swim) },
            { "河豚",       I(3,  200, 4f,  1f,    FishBehavior.Swim) },
            { "龙虾",       I(3,  350, 5f,  1.5f,  FishBehavior.Bottom) },
            { "响螺",       I(3,  300, 6f,  2f,    FishBehavior.Bottom) },
            { "鲍鱼",       I(3,  400, 6f,  0.3f,  FishBehavior.Bottom) },
            { "灯笼鱼",     I(3,  180, 6f,  0.2f,  FishBehavior.Swim) },
            { "鮟鱇鱼",     I(3,  260, 7f,  3f,    FishBehavior.Bottom) },
            { "鹦鹉螺",     I(3,  600, 7f,  1f,    FishBehavior.Bottom) },
            { "锦绣龙虾",   I(3,  800, 7f,  3f,    FishBehavior.Bottom) },
            { "皇帝星斑",   I(3, 1200, 8f,  8f,    FishBehavior.Swim) },
            { "深海龙鱼",   I(3, 1500, 10f, 0.5f,  FishBehavior.Swim) },
            { "吸血鬼鱿鱼", I(3, 1800, 10f, 1f,    FishBehavior.Swim) },

            // ---------- 4 级：大型 ----------
            { "章鱼",       I(4,  400, 6f,  3f,    FishBehavior.Bottom) },
            { "金枪鱼",     I(4,  800, 7f,  60f,   FishBehavior.Swim) },
            { "剑鱼",       I(4,  900, 8f,  100f,  FishBehavior.Swim) },
            { "虎鳗",       I(4, 1500, 9f,  5f,    FishBehavior.Swim) },
            { "海龟",       I(4, 1200, 9f,  80f,   FishBehavior.Swim) },
            { "海豚",       I(4, 1500, 10f, 120f,  FishBehavior.Swim) },
            { "骷髅鱼",     I(4, 2500, 11f, 30f,   FishBehavior.Swim) },
            { "机械鱼",     I(4, 3000, 12f, 200f,  FishBehavior.Bottom) },

            // ---------- 5 级：海怪级 ----------
            { "鲨鱼",       I(5, 2000, 11f, 300f,  FishBehavior.Swim) },
            { "蝠鲼",       I(5, 3000, 12f, 500f,  FishBehavior.Swim) },
            { "蓝鳍金枪鱼", I(5, 5000, 12f, 150f,  FishBehavior.Swim) },
            { "龙趸",       I(5, 4000, 13f, 80f,   FishBehavior.Swim) },
            { "小鲸鱼",     I(5, 5000, 14f, 1500f, FishBehavior.Swim) },
            { "大剑鱼",     I(5, 6000, 14f, 200f,  FishBehavior.Swim) },
            { "皇带鱼",     I(5, 8000, 15f, 200f,  FishBehavior.Swim) },
        };

        /// <summary>图鉴里的全部鱼名（随机生成海产聚集点的品种时用）。</summary>
        public static readonly System.Collections.Generic.IReadOnlyList<string> AllNames =
            new System.Collections.Generic.List<string>(Table.Keys).AsReadOnly();

        static string[] _pool;
        static float[] _weights;
        static float _weightSum;

        /// <summary>
        /// 按体型等级加权抽一条鱼：等级越低越常见（权重 = 1 / 等级^power）。
        /// 分拣区、随机订单都用它，保证「高级鱼概率低」。
        /// </summary>
        public static string RandomByLevel(float power = 3f)
        {
            if (_pool == null)
            {
                var names = new List<string>(Table.Keys);
                _pool = new string[names.Count];
                _weights = new float[names.Count];
                _weightSum = 0f;

                float p = Mathf.Max(0.01f, power);
                for (int k = 0; k < names.Count; k++)
                {
                    float lvl = Mathf.Max(1f, Table[names[k]].level);
                    float w = 1f / Mathf.Pow(lvl, p);
                    _pool[k] = names[k];
                    _weights[k] = w;
                    _weightSum += w;
                }
            }

            float roll = UnityEngine.Random.value * _weightSum;
            for (int k = 0; k < _pool.Length; k++)
            {
                roll -= _weights[k];
                if (roll <= 0f) return _pool[k];
            }
            return _pool[_pool.Length - 1];
        }
        public static bool TryGet(string name, out Info info) => Table.TryGetValue(name, out info);

        public static Info Get(string name)
        {
            return Table.TryGetValue(name, out var info)
                ? info
                : new Info { level = 2, value = 50, minDepth = 0f, weight = 1f, behavior = FishBehavior.Swim };
        }
    }
}
