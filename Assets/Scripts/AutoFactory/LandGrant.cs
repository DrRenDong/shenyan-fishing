using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 土地局批下来的一组地块（默认 2 列 × 3 行）。
    /// 规则：2~4 格草地、0~2 格缺失（留海面），其余随机是民居 / 棚屋 / 农田 / 树林 / 巨石 / 碎石。
    /// 索引 = row * cols + col；空串表示这一格缺失。可以 90° 旋转（2×3 ↔ 3×2）。
    /// </summary>
    [System.Serializable]
    public class LandGrant
    {
        public const int DefaultCols = 2;
        public const int DefaultRows = 3;
        public const int Count = DefaultCols * DefaultRows;

        public int cols = DefaultCols;
        public int rows = DefaultRows;
        public string[] tiles = new string[Count];

        /// <summary>可以出现在地块组里的地物（草地单独统计，缺失留空）。</summary>
        public static readonly string[] PropPool =
        {
            "单层民居", "两层民居", "木板棚屋", "农田", "树林", "巨石", "碎石",
        };

        public string At(int col, int row) => tiles[row * cols + col];
        public void Set(int col, int row, string tile) => tiles[row * cols + col] = tile;

        /// <summary>这一格是不是抬升物件（民居 / 棚屋 / 树林：地面铺草地、物件单独摆）。</summary>
        public static bool IsProp(string tile)
        {
            if (string.IsNullOrEmpty(tile)) return false;
            TileDef def = TileCatalog.Find(tile);
            return def != null && def.LiftPixels > 0f;
        }

        /// <summary>顺时针旋转 90°（2×3 变成 3×2）。</summary>
        public LandGrant Rotate90()
        {
            var rotated = new LandGrant
            {
                cols = rows,
                rows = cols,
                tiles = new string[Count],
            };

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    rotated.Set(rows - 1 - r, c, At(c, r));

            return rotated;
        }

        /// <summary>随机一组：2~4 草地、0~2 缺失，其余从地物池里抽。</summary>
        public static LandGrant Random()
        {
            var grant = new LandGrant();

            int grass = UnityEngine.Random.Range(2, 5);      // 2~4
            int missing = UnityEngine.Random.Range(0, 3);    // 0~2
            if (grass + missing > Count) missing = Mathf.Max(0, Count - grass);

            var order = new List<int>(Count);
            for (int k = 0; k < Count; k++) order.Add(k);
            Shuffle(order);

            int idx = 0;
            for (int k = 0; k < grass; k++) grant.tiles[order[idx++]] = "草地";
            for (int k = 0; k < missing; k++) grant.tiles[order[idx++]] = "";
            while (idx < Count) grant.tiles[order[idx++]] = PropPool[UnityEngine.Random.Range(0, PropPool.Length)];
            return grant;
        }

        static void Shuffle(List<int> list)
        {
            for (int k = list.Count - 1; k > 0; k--)
            {
                int j = UnityEngine.Random.Range(0, k + 1);
                int tmp = list[k];
                list[k] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>给 UI 用的一行摘要，例如「草地×3、两层民居×1、缺失×2」。</summary>
        public string Describe()
        {
            var counts = new Dictionary<string, int>();
            for (int k = 0; k < tiles.Length; k++)
            {
                string key = string.IsNullOrEmpty(tiles[k]) ? "缺失" : tiles[k];
                counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
            }

            var parts = new List<string>();
            foreach (var kv in counts) parts.Add($"{kv.Key}×{kv.Value}");
            return string.Join("、", parts);
        }
    }
}
