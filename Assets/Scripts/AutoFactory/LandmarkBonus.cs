using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 地块增益：民居 / 棚屋 / 农田 / 树林 / 巨石 / 碎石 会给周围 3×3 的建筑带来效果。
    /// 查询时地形与摆在上面的物件都算（例如树林是物件，农田/巨石/碎石是地形）。
    /// </summary>
    public static class LandmarkBonus
    {
        public const string Woods = "树林";
        public const string House1 = "单层民居";
        public const string House2 = "两层民居";
        public const string Shed = "木板棚屋";
        public const string Farm = "农田";
        public const string Boulder = "巨石";
        public const string Gravel = "碎石";

        public const int Radius = 1;   // 3×3

        static IsoTileMap _map;

        static IsoTileMap Map
        {
            get
            {
                if (_map == null) _map = Object.FindObjectOfType<IsoTileMap>();
                return _map;
            }
        }

        /// <summary>以 cell 为中心的 3×3 里，有几个这种地块。</summary>
        public static int CountAround(Vector2Int cell, string tile, int radius = Radius)
        {
            int n = 0;
            IsoTileMap map = Map;
            FactoryWorld world = FactoryWorld.Instance;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var c = new Vector2Int(cell.x + dx, cell.y + dy);

                    if (map != null && map.TileAt(c.x, c.y) == tile)
                    {
                        n++;
                        continue;
                    }

                    if (world == null) continue;
                    PlacedTile occ = world.OccupantAt(c);
                    if (occ != null && occ.tileId == tile) n++;
                }
            }
            return n;
        }

        public static bool HasAround(Vector2Int cell, string tile) => CountAround(cell, tile) > 0;

        // ---------------- 具体效果 ----------------

        /// <summary>树林（绿化）：周围 3×3 的建筑运营费减半。</summary>
        public static float OperatingCostMultiplier(Vector2Int cell)
        {
            return HasAround(cell, Woods) ? 0.5f : 1f;
        }

        /// <summary>民居：单层 +10% 产量，两层 +20%（住得下更多工人）。</summary>
        public static float ProductionMultiplier(Vector2Int cell)
        {
            float mul = 1f;
            if (HasAround(cell, House1)) mul += 0.10f;
            if (HasAround(cell, House2)) mul += 0.20f;
            return mul;
        }

        /// <summary>农田：周围工厂的辅料消耗 -20%（就地供应淀粉/辅料）。</summary>
        public static float AuxiliaryMultiplier(Vector2Int cell)
        {
            return HasAround(cell, Farm) ? 0.8f : 1f;
        }

        /// <summary>木板棚屋：仓库/码头吞吐 +20%。</summary>
        public static float ThroughputMultiplier(Vector2Int cell)
        {
            return HasAround(cell, Shed) ? 1.2f : 1f;
        }

        /// <summary>巨石：周围 3×3 的成品品质 +5%（挡风、稳定水温）。</summary>
        public static float QualityBonus(Vector2Int cell)
        {
            return HasAround(cell, Boulder) ? 0.05f : 0f;
        }

        /// <summary>碎石：这一格新建造费用 -10%（就地取材）。</summary>
        public static float BuildCostMultiplier(Vector2Int cell)
        {
            return HasAround(cell, Gravel) ? 0.9f : 1f;
        }

        /// <summary>面板上显示的一行效果说明；没有效果返回空串。</summary>
        public static string Describe(string tile)
        {
            switch (tile)
            {
                case Woods: return "绿化：周围 3×3 的建筑运营费 <b>-50%</b>";
                case House1: return "工棚：周围 3×3 的工厂产量 <b>+10%</b>";
                case House2: return "宿舍：周围 3×3 的工厂产量 <b>+20%</b>";
                case Shed: return "储物棚：周围 3×3 的仓库/码头吞吐 <b>+20%</b>";
                case Farm: return "农田：周围 3×3 的工厂辅料消耗 <b>-20%</b>";
                case Boulder: return "巨石：周围 3×3 的成品品质 <b>+5%</b>";
                case Gravel: return "碎石：这一格新建造费用 <b>-10%</b>";
                default: return "";
            }
        }
    }
}
