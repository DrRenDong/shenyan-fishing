using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    public enum BuildCategory { Road, Factory, Facility, Port }

    /// <summary>可建造项：造价、工期、要贴着路网还是只能建在水里。</summary>
    public class BuildDef
    {
        public string Name;
        public string TileId;
        public BuildCategory Category;
        public FactoryType FactoryType;
        public int Cost;
        public int Turns;
        public bool NeedsRoad;
        public bool OnWater;        // 只能建在水体里（港口）
        public bool CargoDock;      // 港口类型：true=货运码头，false=捕捞港口
        public string Note = "";
    }

    /// <summary>建造目录：路面 / 10 种厂 / 分拣区。越高级的厂越贵、修得越久。</summary>
    public static class BuildCatalog
    {

        // ---------------- 科技解锁 ----------------

        /// <summary>一开始就能建的：路面、仓库、港口、能源设施靠王老板订单解锁。</summary>
        static readonly HashSet<string> DefaultUnlocked = new HashSet<string>
        {
            "路面", "小型仓库", "捕捞港口", "货运码头",
        };

        static readonly HashSet<string> Unlocked = new HashSet<string>();

        public static bool IsUnlocked(BuildDef def)
        {
            if (def == null) return false;
            return DefaultUnlocked.Contains(def.Name) || Unlocked.Contains(def.Name);
        }

        public static void Unlock(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            Unlocked.Add(name);
            Debug.Log("[科技] 解锁建造项：" + name);
        }

        public static bool IsUnlockedByName(string name)
        {
            return DefaultUnlocked.Contains(name) || Unlocked.Contains(name);
        }

        /// <summary>存档用：把已经解锁的工艺名拷一份出来。</summary>
        public static List<string> SnapshotUnlocked()
        {
            return new List<string>(Unlocked);
        }

        /// <summary>读档用：把存档里的解锁项补回来（默认解锁的几项本来就有，重复也没关系）。</summary>
        public static void ApplyUnlocked(List<string> names)
        {
            if (names == null) return;
            for (int k = 0; k < names.Count; k++)
                if (!string.IsNullOrEmpty(names[k])) Unlocked.Add(names[k]);
        }
        public static readonly BuildDef[] All =
        {
            new BuildDef { Name = "路面", TileId = "石砖路", Category = BuildCategory.Road,
                           Cost = 30, Turns = 0, NeedsRoad = false, Note = "物资靠路面流转" },

            new BuildDef { Name = "小型仓库", TileId = "小型仓库", Category = BuildCategory.Facility,
                           Cost = 300, Turns = 0, Note = "存物资，工厂靠它取料" },

            new BuildDef { Name = "分拣区", TileId = "分拣区", Category = BuildCategory.Facility,
                           Cost = 400, Turns = 2, NeedsRoad = true, Note = "渔获分拣" },

            new BuildDef { Name = "接收塔", TileId = "接收塔_右前", Category = BuildCategory.Facility,
                           Cost = 600, Turns = 0, Note = "截住电流束，周围 7×7 通电" },
            new BuildDef { Name = "棱镜塔", TileId = "棱镜塔", Category = BuildCategory.Facility,
                           Cost = 800, Turns = 0, Note = "电流束穿过时向两侧分叉" },

            new BuildDef { Name = "捕捞港口", TileId = "小型捕捞船港口", Category = BuildCategory.Port,
                           Cost = 900, Turns = 3, OnWater = true, CargoDock = false,
                           Note = "建在水里，要挨着陆地" },
            new BuildDef { Name = "货运码头", TileId = "小型货运港口", Category = BuildCategory.Port,
                           Cost = 800, Turns = 2, OnWater = true, CargoDock = true,
                           Note = "建在水里，要挨着陆地" },

            new BuildDef { Name = "预处理厂", TileId = "预处理厂", Category = BuildCategory.Factory,
                           FactoryType = FactoryType.Preprocess, Cost = 500, Turns = 2, NeedsRoad = true },
            new BuildDef { Name = "成型厂", TileId = "成型厂", Category = BuildCategory.Factory,
                           FactoryType = FactoryType.Forming, Cost = 600, Turns = 3, NeedsRoad = true },
            new BuildDef { Name = "灌装封口厂", TileId = "灌装封口厂", Category = BuildCategory.Factory,
                           FactoryType = FactoryType.Canning, Cost = 700, Turns = 3, NeedsRoad = true },
            new BuildDef { Name = "塑封分装厂", TileId = "塑封分装厂", Category = BuildCategory.Factory,
                           FactoryType = FactoryType.Sealing, Cost = 650, Turns = 3, NeedsRoad = true },
            new BuildDef { Name = "干燥厂", TileId = "干燥厂", Category = BuildCategory.Factory,
                           FactoryType = FactoryType.Drying, Cost = 550, Turns = 2, NeedsRoad = true },
            new BuildDef { Name = "发酵熟成厂", TileId = "发酵熟成厂", Category = BuildCategory.Factory,
                           FactoryType = FactoryType.Fermenting, Cost = 800, Turns = 4, NeedsRoad = true },
            new BuildDef { Name = "萃取精炼厂", TileId = "萃取精炼厂", Category = BuildCategory.Factory,
                           FactoryType = FactoryType.Extraction, Cost = 1200, Turns = 6, NeedsRoad = true },
            new BuildDef { Name = "冷冻冷藏厂", TileId = "冷冻冷藏厂", Category = BuildCategory.Factory,
                           FactoryType = FactoryType.Freezing, Cost = 900, Turns = 4, NeedsRoad = true },
            new BuildDef { Name = "烟熏房", TileId = "烟熏房", Category = BuildCategory.Factory,
                           FactoryType = FactoryType.Smoking, Cost = 600, Turns = 3, NeedsRoad = true },
            new BuildDef { Name = "油炸房", TileId = "油炸房", Category = BuildCategory.Factory,
                           FactoryType = FactoryType.Frying, Cost = 700, Turns = 3, NeedsRoad = true },
        };

        public static List<BuildDef> Factories()
        {
            var list = new List<BuildDef>();
            foreach (var d in All) if (d.Category == BuildCategory.Factory) list.Add(d);
            return list;
        }
    }
}
