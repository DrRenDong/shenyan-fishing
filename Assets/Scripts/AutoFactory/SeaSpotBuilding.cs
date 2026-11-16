using UnityEngine;

namespace Shenyan
{
    /// <summary>海产聚集点：随机生成的捕捞点，一段时间后消失，鱼带品质。</summary>
    public class SeaSpotBuilding : PlacedTile
    {
        public string species = "沙丁鱼";
        [Range(0f, 1f)] public float quality = 0.69f;
        public float amountKg = 5524f;
        public float lifeHours = 486f;      // 总存在时间（游戏小时）
        public float ageHours = 0f;

        /// <summary>
        /// 被神眼看见过没有。看见之后就一直显示（哪怕船开走了），
        /// 免得玩家明明看到过、回头只剩一片黑。
        /// </summary>
        public bool revealed;

        public float RemainingHours => Mathf.Max(0f, lifeHours - ageHours);
    }
}
