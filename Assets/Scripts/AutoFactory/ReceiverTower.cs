using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 接收塔：电流束打到它就到这里为止（截断吸收），并把周围 7×7 的地块供上电。
    /// 摆放后会自动朝向电流束射来的方向（电力图每次重掷光束时同步刷新）。
    /// </summary>
    public class ReceiverTower : PlacedTile
    {
        public override bool CanDemolish => true;

        [Tooltip("供电半径：3 = 以塔为中心的 7×7")]
        public int powerRadius = 3;

        [Tooltip("电流束射来的方向（单位向量），(0,0) = 还没有束打到")]
        public Vector2Int beamFrom = Vector2Int.zero;

        public bool PoweredByBeam => beamFrom != Vector2Int.zero;

        /// <summary>来流方向 → 对应的朝向贴图（塔面朝向来电的那一侧）。</summary>
        public static string TileIdOf(Vector2Int from)
        {
            if (from.x > 0) return "接收塔_右前";
            if (from.y > 0) return "接收塔_左前";
            if (from.x < 0) return "接收塔_左后";
            if (from.y < 0) return "接收塔_右后";
            return "接收塔_右前";
        }

        public static bool IsReceiverTile(string id)
        {
            return id == "接收塔_右前" || id == "接收塔_左前"
                || id == "接收塔_左后" || id == "接收塔_右后";
        }

        /// <summary>换成朝向来流方向的贴图；没变化就什么都不做。</summary>
        public void SetBeamFrom(Vector2Int from)
        {
            beamFrom = from;
            ApplyTileId(TileIdOf(from));
        }

        /// <summary>只换贴图与抬升，不动格坐标。</summary>
        public void ApplyTileId(string id)
        {
            if (tileId == id) return;
            tileId = id;

            SpriteRenderer sr = Renderer;
            if (sr == null) return;

            sr.sprite = TileCatalog.LoadSprite(id);
            TileDef def = TileCatalog.Find(id);
            if (def != null) sr.transform.localPosition = new Vector3(0f, def.LiftPixels / 304f, 0f);
        }
    }
}
