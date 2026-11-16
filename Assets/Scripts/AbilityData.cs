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

        /// <summary>神眼·远眺 0-4：海神号（渔船）的视野半径每级 +1 格。消耗美乐珠。</summary>
        public static int EyeFarLevel = 0;

        /// <summary>动物交流·优质 0-4：捕鱼带回的鱼每级 +5% 品质。消耗龙涎香。</summary>
        public static int QualityLevel = 0;

        /// <summary>地形改造 0-3：1 拆附属物、2 拆建筑全额退款、3 移动草地。消耗南海黑珍珠。</summary>
        public static int TerrainLevel = 0;

        public static int EyeFarMax => 4;
        public static int QualityMax => 4;
        public static int TerrainMax => 3;

        /// <summary>升级消耗的贵重品数量（下标 = 当前等级）。</summary>
        public static readonly int[] EyeFarCosts = { 1, 2, 4, 5 };
        public static readonly int[] QualityCosts = { 1, 2, 4, 5 };
        public static readonly int[] TerrainCosts = { 2, 4, 6 };

        /// <summary>船只视野额外半径（神眼·远眺）。</summary>
        public static int BoatViewBonus => Mathf.Clamp(EyeFarLevel, 0, EyeFarMax);

        /// <summary>捕鱼渔获的品质加成（动物交流·优质）。</summary>
        public static float CatchQualityBonus => 0.05f * Mathf.Clamp(QualityLevel, 0, QualityMax);

        /// <summary>捕鱼带回的鱼的基础品质（再叠加「优质」加成）。</summary>
        public const float BaseCatchQuality = 0.7f;
        public static float CatchQuality => Mathf.Clamp01(BaseCatchQuality + CatchQualityBonus);

        /// <summary>地形改造 Lv1：能拆树林、民居这类附属物。</summary>
        public static bool CanDemolishProps => TerrainLevel >= 1;

        /// <summary>地形改造 Lv2：拆建筑从退一半变成全额。</summary>
        public static float DemolishRefundRate => TerrainLevel >= 2 ? 1f : 0.5f;

        /// <summary>地形改造 Lv3：能把草地搬到别的位置。</summary>
        public static bool CanMoveGrass => TerrainLevel >= 3;

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
                case "eyefar":
                    return EyeFarLevel <= 0 ? "视野：正常" : $"视野半径 +{BoatViewBonus} 格";
                case "quality":
                    return QualityLevel <= 0 ? "品质：正常" : $"捕鱼品质 {CatchQuality * 100f:0}%";
                case "terrain":
                    if (TerrainLevel <= 0) return "未解锁";
                    if (TerrainLevel == 1) return "可拆除树林 / 民居等附属物";
                    if (TerrainLevel == 2) return "拆建筑全额退款（可拆附属物）";
                    return "可移动草地（拆建筑全额退款）";
                default:
                    return "";
            }
        }
    }
}
