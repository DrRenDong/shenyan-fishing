using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 捕捞港口：给玩家指定一个捕捞点持续作业。
    /// - 目标可以是任意水体格：只能捞到「杂鱼」，速率很低；
    /// - 目标若是海产聚集点：直接捞这个聚集点的鱼，速率正常、品质继承聚集点；
    /// - 距离每远 1 格，品质 -1%（越远越不新鲜）。
    /// 产出走和工厂一样的路子：进相连仓库。
    /// </summary>
    public class FishingPortBuilding : PlacedTile
    {
        public override bool CanDemolish => true;

        [Header("运营")]
        public bool powered = true;
        [Tooltip("玩家开关：停工时不捕捞、也不扣日费")]
        public bool running = true;
        public float dailyCost = 0f;

        [Header("捕捞点")]
        // 用 int.MinValue 做「没指定」的哨兵：地图可以延伸到负坐标，
        // 以前用 (-1,-1) 判断 ≥0，负坐标的聚集点会被当成「没有目标」。
        public Vector2Int targetCell = new Vector2Int(int.MinValue, int.MinValue);
        public float spotCatchPerTurn = 1.2f;       // 目标为海产聚集点时（KG/回合）
        public float junkCatchPerTurn = 0.4f;       // 目标为普通水体时（KG/回合）
        public string junkFish = ItemDatabase.JunkFish;
        [Range(0f, 1f)] public float junkQuality = 0.45f;
        public float distanceQualityPenalty = 0.01f; // 每格 -1%

        [Header("水体检定")]
        [Tooltip("捕捞点是内陆湖（被陆地围死）时，速率乘这个系数")]
        [Range(0f, 1f)] public float inlandRateFactor = 0.2f;

        /// <summary>港口和捕捞点是否在同一片连通水域里。</summary>
        public bool ConnectedToTarget { get; private set; }
        /// <summary>捕捞点是不是内陆湖（被陆地围住）。</summary>
        public bool TargetIsInland { get; private set; }

        /// <summary>重新算一遍水体检定（每回合 / 面板刷新时调用）。</summary>
        public void RefreshWaterState()
        {
            ConnectedToTarget = false;
            TargetIsInland = false;
            WaterPath = null;
            if (!HasTarget) return;

            var map = FindObjectOfType<IsoTileMap>();
            if (map == null) return;

            var self = new Vector2Int(gridX, gridY);
            // allowOpenSea：捕捞点可能落在「地图外没有地形」的开放海域里（比如漂在远海的聚集点）
            WaterPath = map.FindWaterPath(self, targetCell, true);
            ConnectedToTarget = WaterPath != null;
            TargetIsInland = map.IsEnclosedWater(targetCell.x, targetCell.y);
        }

        /// <summary>港口 → 捕捞点的水面最短路径（只在 RefreshWaterState 后有效）。</summary>
        public List<Vector2Int> WaterPath { get; private set; }

        public bool HasTarget => targetCell.x != int.MinValue && targetCell.y != int.MinValue;

        /// <summary>与捕捞点的距离：优先用「绕开水面的最短路径」格数，没路径则退回直线格距。</summary>
        public int Distance
        {
            get
            {
                if (!HasTarget) return 0;
                if (WaterPath != null && WaterPath.Count > 1) return WaterPath.Count - 1;
                return Mathf.Max(Mathf.Abs(gridX - targetCell.x), Mathf.Abs(gridY - targetCell.y));
            }
        }

        public void SetTarget(Vector2Int cell)
        {
            targetCell = cell;
        }

        public void ClearTarget()
        {
            targetCell = new Vector2Int(int.MinValue, int.MinValue);
        }

        /// <summary>当前目标是不是水体（可以作业）。</summary>
        public bool TargetIsWater
        {
            get
            {
                if (!HasTarget) return false;
                var map = FindObjectOfType<IsoTileMap>();
                if (map == null) return true;
                if (map.IsWaterAt(targetCell.x, targetCell.y)) return true;

                // 地图外的开放海域没有地形数据（TileAt 为空），那也算水，
                // 否则漂在远海的聚集点会一直「目标不是水体」而不作业。
                return string.IsNullOrEmpty(map.TileAt(targetCell.x, targetCell.y));
            }
        }

        /// <summary>当前目标上的海产聚集点（没有就是 null → 只能捞杂鱼）。</summary>
        public SeaSpotBuilding TargetSpot
        {
            get
            {
                if (!HasTarget || FactoryWorld.Instance == null) return null;
                return FactoryWorld.Instance.OccupantAt(targetCell) as SeaSpotBuilding;
            }
        }

        public string CatchSpecies
        {
            get
            {
                SeaSpotBuilding spot = TargetSpot;
                return spot != null ? spot.species : junkFish;
            }
        }

        /// <summary>未考虑连通/内陆湖的基准速率。</summary>
        public float BaseCatchRate
        {
            get { return TargetSpot != null ? spotCatchPerTurn : junkCatchPerTurn; }
        }

        /// <summary>
        /// 实际速率：和捕捞点不连通 → 0（船开不过去）；
        /// 捕捞点是内陆湖 → 乘 inlandRateFactor（大幅降速）。
        /// </summary>
        public float CatchRate
        {
            get
            {
                if (!ConnectedToTarget) return 0f;
                return BaseCatchRate * (TargetIsInland ? inlandRateFactor : 1f) * LevelSpeedMultiplier;
            }
        }

        /// <summary>港口被供电时开冷链：鱼不再因为离得远而掉品质。</summary>
        public bool ColdChainActive => PowerGridView.GridPowered(Cell);

        /// <summary>港口是否真的接在电网上（面板显示用：以前直接显示序列化字段，永远写着「已通电」）。</summary>
        public bool HasPower => PowerGridView.GridPowered(Cell);

        /// <summary>实际品质：基础品质 - 距离惩罚（有冷链就不惩罚）+ 动物交流·优质加成。</summary>
        public float CatchQuality
        {
            get
            {
                SeaSpotBuilding spot = TargetSpot;
                float quality = spot != null ? spot.quality : junkQuality;
                if (!ColdChainActive) quality -= distanceQualityPenalty * Distance;

                // 动物交流·优质：捕鱼港口也享受这份加成（每级 +5%）
                quality += AbilityData.CatchQualityBonus;
                return Mathf.Clamp01(quality);
            }
        }

        /// <summary>推进一个回合：往相连仓库里放鱼。返回实际捞到的量（KG）。</summary>
        public float Tick(List<WarehouseBuilding> warehouses)
        {
            // 捕捞本身不要求通电（电只影响冷链品质），所以这里不再判断 powered 字段
            if (!running) return 0f;                // 玩家手动停工
            if (!TargetIsWater) return 0f;
            if (warehouses == null || warehouses.Count == 0) return 0f;

            RefreshWaterState();
            if (!ConnectedToTarget) return 0f;      // 港口和捕捞点不在一片水域，捞不到

            SeaSpotBuilding spot = TargetSpot;
            float amount = CatchRate;
            if (amount <= 0f) return 0f;

            if (spot != null)
            {
                if (spot.amountKg <= 0f) return 0f;
                amount = Mathf.Min(amount, spot.amountKg);
                spot.amountKg -= amount;
            }

            if (amount <= 0f) return 0f;

            warehouses[0].storage.Add(CatchSpecies, amount, CatchQuality);
            LastCaught += amount;
            CaughtThisTurn = true;
            return amount;
        }

        public float LastCaught { get; private set; }

        /// <summary>本回合有没有真的捞到鱼（只有在捞到鱼时才会派工人往仓库搬）。</summary>
        public bool CaughtThisTurn { get; private set; }

        /// <summary>每回合开始时清一次。</summary>
        public void BeginTurn() => CaughtThisTurn = false;
    }
}
