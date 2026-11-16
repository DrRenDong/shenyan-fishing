using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 沉船 / 宝藏地块：藏在海里的东西，点开会显示「未捞取」，捞过就没了。
    /// 具体坐标与状态由 WreckSiteSystem 管理（跟着存档走）。
    /// </summary>
    public class WreckSiteTile : PlacedTile
    {
        public override bool CanDemolish => false;

        [Tooltip("false = 沉船，true = 宝藏")]
        public bool isTreasure;

        public bool salvaged;

        [Tooltip("水下染色：偏蓝、略透明，让它看起来沉在海里而不是漂在水上")]
        public Color underwaterTint = new Color(0.62f, 0.82f, 0.95f, 0.88f);

        [Tooltip("视觉缩放：保持 1 = 正好一格，超过 1 会盖住相邻水体")]
        public float visualScale = 1f;

        public string KindName => isTreasure ? "海底宝藏" : "海底沉船";

        /// <summary>沉船里装的东西（贵重品 id，空 = 没东西）。</summary>
        public string LootName
        {
            get
            {
                var site = WreckSiteSystem.GetSite(new Vector2Int(gridX, gridY));
                return site != null ? site.loot : null;
            }
        }

        public string StatusText => salvaged ? "已捞取" : "未捞取";

        /// <summary>视野内正常色；视野外压暗（和聚集点一样的黑滤镜感）。</summary>
        public void SetInView(bool inView)
        {
            SpriteRenderer sr = Renderer;
            if (sr == null) return;

            sr.color = inView
                ? underwaterTint
                : new Color(underwaterTint.r * 0.45f, underwaterTint.g * 0.45f, underwaterTint.b * 0.45f, underwaterTint.a);
        }

        public override void EnsureVisual()
        {
            base.EnsureVisual();

            transform.localScale = Vector3.one * visualScale;
            SetInView(true);
        }
    }
}
