using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>瓦片分类，决定它在玩法里的默认用途。</summary>
    public enum TileCategory
    {
        Terrain,   // 通用地形（平地，直接铺）
        Water,     // 水面（海面，铺在地形之上）
        Prop,      // 自带底座、需要抬到与地面齐平的物件（民居 / 树林 / 棚屋）
        Factory,   // 自动化工厂厂房
        Port,      // 港口与仓储建筑
        Facility,  // 渔场设施（水体地块 / 分拣区）
        Energy,    // 能源设施（接收塔 / 棱镜塔）
    }

    /// <summary>一种等距瓦片 / 建筑的元数据。</summary>
    public class TileDef
    {
        public string Id;             // Sprite 资源名
        public TileCategory Category;
        public int Footprint = 1;     // 占几格（1 或 2）
        public float LiftPixels;      // 摆放时相对格点向上的像素抬升（负数 = 下沉）
        public string Note = "";

        public TileDef(string id, TileCategory category, float liftPixels, int footprint = 1, string note = "")
        {
            Id = id;
            Category = category;
            LiftPixels = liftPixels;
            Footprint = footprint;
            Note = note;
        }
    }

    /// <summary>
    /// 全部等距素材的登记表：格数、摆放抬升、来源目录。
    /// 抬升规则：普通地块 = 0；自带薄底座/平台的建筑 = 129 - 平台厚度（本文件已换算好）。
    /// 所有 Sprite 都是 384×384 画布、底面菱形 304×152、锚点在底面「前角」（距画布底 12px）。
    /// </summary>
    public static class TileCatalog
    {
        public const string TerrainFolder = "Image/地图瓦片";
        public const string FactoryFolder = "Image/厂房";
        public const string PortFolder = "Image/港口";
        public const string FacilityFolder = "Image/渔场";
        public const string EnergyFolder = "Image/能源设施";

        /// <summary>普通地形方块的「地面」高度（像素，从方块底部到顶面）。</summary>
        public const float SurfacePixels = 129f;

        public static readonly TileDef[] All =
        {
            // ---- 地形（18 种，全部 1 格，直接铺在地面上）----
            new TileDef("草地", TileCategory.Terrain, 0f),
            new TileDef("沙滩", TileCategory.Terrain, 0f),
            new TileDef("碎石", TileCategory.Terrain, 0f),
            new TileDef("浅水", TileCategory.Water, 0f),
            new TileDef("深水", TileCategory.Water, 0f),
            new TileDef("水面A", TileCategory.Water, 0f),
            new TileDef("水面B", TileCategory.Water, 0f, 1, "与水面A同一组的两帧"),
            new TileDef("巨石", TileCategory.Terrain, 0f),
            new TileDef("农田", TileCategory.Terrain, 0f),
            new TileDef("湿润泥土", TileCategory.Terrain, 0f),
            new TileDef("泥土半块", TileCategory.Terrain, 0f),
            new TileDef("泥土整块", TileCategory.Terrain, 0f),
            new TileDef("巨石半块", TileCategory.Terrain, 0f),
            new TileDef("石砖路", TileCategory.Terrain, 0f),
            new TileDef("洞穴", TileCategory.Terrain, 0f, 1, "压暗的地面，用来做山洞内部"),
            new TileDef("单层民居", TileCategory.Prop, 98f, 1, "底座 32px"),
            new TileDef("两层民居", TileCategory.Prop, 104f, 1, "底座 26px"),
            new TileDef("木板棚屋", TileCategory.Prop, 94f, 1, "底座 36px"),
            new TileDef("树林", TileCategory.Prop, 78f, 1, "底座 52px"),

            // ---- 自动化工厂厂房（10 栋，1 格）----
            new TileDef("预处理厂", TileCategory.Factory, 118f),
            new TileDef("成型厂", TileCategory.Factory, 117f),
            new TileDef("灌装封口厂", TileCategory.Factory, 118f),
            new TileDef("塑封分装厂", TileCategory.Factory, 118f),
            new TileDef("干燥厂", TileCategory.Factory, 119f),
            new TileDef("发酵熟成厂", TileCategory.Factory, 119f),
            new TileDef("萃取精炼厂", TileCategory.Factory, 119f),
            new TileDef("冷冻冷藏厂", TileCategory.Factory, 120f),
            new TileDef("烟熏房", TileCategory.Factory, 120f),
            new TileDef("油炸房", TileCategory.Factory, 116f),

            // ---- 港口与仓储（5 栋，1 格）----
            new TileDef("小型捕捞船港口", TileCategory.Port, 104f),
            new TileDef("小型货运港口", TileCategory.Port, 102f),
            new TileDef("大型仓库", TileCategory.Port, 119f),
            new TileDef("港口管理站", TileCategory.Port, 96f),
            new TileDef("小型仓库", TileCategory.Port, 119f),

            // ---- 渔场设施（2 块，1 格）----
            new TileDef("聚集点", TileCategory.Facility, -25f, 1, "水面与海面齐平，底部下沉 25px"),
            new TileDef("分拣区", TileCategory.Facility, 75f, 1, "平台与地面齐平"),
            new TileDef("沉船", TileCategory.Facility, 0f, 1, "海底沉船，和其他水体方块齐平"),
            new TileDef("宝藏", TileCategory.Facility, 0f, 1, "海底宝箱，和其他水体方块齐平"),

            // ---- 能源设施（1 格，画布 384×512，锚点仍在底面「前角」12px）----
            new TileDef("接收塔_右前", TileCategory.Energy, 90f, 1, "朝 +i（屏幕右下）"),
            new TileDef("接收塔_左前", TileCategory.Energy, 90f, 1, "朝 +j（屏幕左下），右前的镜像"),
            new TileDef("接收塔_左后", TileCategory.Energy, 90f, 1, "朝 -i（屏幕左上）"),
            new TileDef("接收塔_右后", TileCategory.Energy, 90f, 1, "朝 -j（屏幕右上），左后的镜像"),
            new TileDef("棱镜塔", TileCategory.Energy, 90f, 1, "电流束穿过后向两侧分叉"),
        };

        static readonly Dictionary<string, TileDef> Lookup = BuildLookup();

        static Dictionary<string, TileDef> BuildLookup()
        {
            var map = new Dictionary<string, TileDef>(All.Length);
            foreach (var def in All) map[def.Id] = def;
            return map;
        }

        public static TileDef Find(string id)
        {
            return Lookup.TryGetValue(id, out TileDef def) ? def : null;
        }

        public static string FolderOf(TileCategory category)
        {
            switch (category)
            {
                case TileCategory.Factory: return FactoryFolder;
                case TileCategory.Port: return PortFolder;
                case TileCategory.Facility: return FacilityFolder;
                case TileCategory.Energy: return EnergyFolder;
                default: return TerrainFolder;
            }
        }

        public static Sprite LoadSprite(string id)
        {
            TileDef def = Find(id);
            if (def == null)
            {
                Debug.LogWarning($"[TileCatalog] 未登记的瓦片：{id}");
                return null;
            }
            return Resources.Load<Sprite>(FolderOf(def.Category) + "/" + id);
        }
    }
}
