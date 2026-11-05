using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 异能（原天赋）数据：等级存在静态字段里，异能面板显示、战斗逻辑都读它。
    /// 目前没有存档，等级只在本次运行内有效。
    /// </summary>
    public static class AbilityData
    {
        public const float SurfaceY = 15.5f;

        /// <summary>神眼 0-4（4 = 无限）。默认解锁 0 级。</summary>
        public static int EyeLevel = 0;

        /// <summary>动物交流·海龟 0-3。默认解锁 0 级。</summary>
        public static int TurtleLevel = 0;

        /// <summary>动物交流·鲸鱼 -1 = 未解锁，0-2。</summary>
        public static int WhaleLevel = -1;

        /// <summary>威慑 0-2。默认未解锁。</summary>
        public static int IntimidateLevel = 0;

        /// <summary>神眼：从水面往下能潜多少格（4 级无限）。</summary>
        static readonly float[] EyeDepths = { 10f, 25f, 50f, 100f, float.PositiveInfinity };

        public static int EyeMax => 4;
        public static int TurtleMax => 3;
        public static int WhaleMax => 2;
        public static int IntimidateMax => 2;

        /// <summary>当前潜望深度（格）。</summary>
        public static float DiveDepth => EyeDepths[Mathf.Clamp(EyeLevel, 0, 4)];

        /// <summary>相机中心允许的最低 y（神眼限制，4 级时等于负无穷）。</summary>
        public static float DiveLimitY => SurfaceY - DiveDepth;

        public static bool WhaleUnlocked => WhaleLevel >= 0;

        /// <summary>海龟每次出海可用次数：0 级 1 次、1 级 2 次、3 级 3 次。</summary>
        public static int TurtleSummonsPerTrip => TurtleLevel >= 3 ? 3 : (TurtleLevel >= 1 ? 2 : 1);

        /// <summary>海龟撞晕时长：基础 2 秒，2 级 3 秒，3 级 4 秒。</summary>
        public static float TurtleStunDuration => TurtleLevel >= 3 ? 4f : (TurtleLevel >= 2 ? 3f : 2f);

        /// <summary>鲸鱼每次出海可用次数：解锁 1 次，1 级 2 次。</summary>
        public static int WhaleSummonsPerTrip => !WhaleUnlocked ? 0 : (WhaleLevel >= 1 ? 2 : 1);

        /// <summary>鲸鱼撞击造成鱼耐力上限的多少（2 级直接归零）。</summary>
        public static float WhaleDamageRatio => WhaleLevel >= 2 ? 1f : 0.5f;

        /// <summary>威慑：下沉阶段能略过几条小鱼。</summary>
        public static int IntimidateSkip => Mathf.Clamp(IntimidateLevel, 0, 2);

        /// <summary>面板用的一句话说明。</summary>
        public static string Describe(string key)
        {
            switch (key)
            {
                case "eye":
                    return EyeLevel >= 4 ? "潜望：无限" : $"潜望：{DiveDepth:0.#} 格";
                case "turtle":
                    return $"海龟 ×{TurtleSummonsPerTrip}，眩晕 {TurtleStunDuration:0.#} 秒";
                case "whale":
                    return !WhaleUnlocked ? "未解锁" :
                           (WhaleLevel >= 2 ? $"鲸鱼 ×{WhaleSummonsPerTrip}，撞击直接力竭"
                                            : $"鲸鱼 ×{WhaleSummonsPerTrip}，撞击削一半耐力");
                case "intimidate":
                    return IntimidateLevel <= 0 ? "未解锁" : $"下沉时略过 {IntimidateSkip} 条小鱼";
                default:
                    return "";
            }
        }
    }
}
