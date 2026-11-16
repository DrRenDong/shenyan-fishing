using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 分拣区：把杂鱼拆开分拣，每回合产出一条随机鱼种（等级越低越常见）。
    /// 不需要通电、也没有日费；没杂鱼就自然停工。
    /// </summary>
    public class SortingFacility : PlacedTile
    {
        public override bool CanDemolish => true;

        [Header("运营")]
        [Tooltip("每日运营费（员工工资 + 设施维护），没原料也照收")]
        public float dailyCost = 3f;

        [Tooltip("玩家手动开关：停工时不挑拣，也不扣日运营费")]
        public bool running = true;

        public void SetRunning(bool on) => running = on;

        [Header("分拣")]
        [Tooltip("每回合消耗多少杂鱼（KG）")]
        public float junkPerTurn = 1f;
        [Tooltip("每回合产出多少鱼（KG）")]
        public float outputPerTurn = 1f;
        [Range(0f, 1f)] public float quality = 0.45f;
        [Tooltip("等级权重指数：越大越偏向低等级鱼")]
        public float levelWeightPower = 3f;

        public float LastOutput { get; private set; }
        public string LastSpecies { get; private set; } = "";

        /// <summary>推进一回合：吃杂鱼、吐随机鱼。返回产出量（KG）。</summary>
        public float Tick(List<WarehouseBuilding> warehouses)
        {
            LastOutput = 0f;
            if (!running) return 0f;                       // 玩家手动停工
            if (warehouses == null || warehouses.Count == 0) return 0f;

            // 原子扣杂鱼
            float speed = LevelSpeedMultiplier;
            float need = junkPerTurn * speed;
            float taken = 0f;
            string junk = ItemDatabase.JunkFish;
            for (int k = 0; k < warehouses.Count && need > 1e-4f; k++)
            {
                float got = warehouses[k].storage.Take(junk, need);
                taken += got;
                need -= got;
            }

            if (taken <= 1e-3f) return 0f;

            float amount = outputPerTurn * speed * (taken / Mathf.Max(0.01f, junkPerTurn * speed));
            string species = FishDatabase.RandomByLevel(levelWeightPower);

            warehouses[0].storage.Add(species, amount, quality);
            LastOutput = amount;
            LastSpecies = species;
            return amount;
        }
    }
}
