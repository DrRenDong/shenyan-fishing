using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 等距场景的接触阴影。
    ///
    /// 场景里的精灵全走 Sprites/Default（unlit），真的加 Light 也不会产生投影，
    /// 所以这里给每个立在地面上的物件脚底铺一张程序生成的软椭圆阴影贴图，
    /// 让物件和地面之间有「压下去的接触感」。
    ///
    /// 阴影是物件的子物体，但按世界坐标摆在地表（父物体的抬升量会抵消掉），
    /// 排序号用「同格地面」那一层，靠 z 比地面更靠近相机来保证画在地面之上、本体之下。
    /// </summary>
    public static class IsoShadow
    {
        /// <summary>等距格「顶面中心」相对格原点的高度（与 PlacedTile.WorldToCell 的 surfaceOffset 一致）。</summary>
        public const float SurfaceCenterY = 0.66f;

        /// <summary>
        /// 影子相对底座中心的偏移：光照统一设成「右上打光」，
        /// 所以影子往左下偏一点，看起来才像被光推开的。
        /// </summary>
        public static readonly Vector2 LightOffset = new Vector2(-0.08f, -0.04f);

        /// <summary>默认不透明度。</summary>
        public const float DefaultAlpha = 0.34f;

        const int TexWidth = 128;
        const int TexHeight = 64;
        const float PixelsPerUnit = 100f;

        static Sprite _sprite;

        public static Sprite ShadowSprite
        {
            get
            {
                if (_sprite == null) BuildSprite();
                return _sprite;
            }
        }

        static void BuildSprite()
        {
            var tex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false, true);
            tex.name = "IsoShadowTex";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var px = new Color32[TexWidth * TexHeight];
            for (int y = 0; y < TexHeight; y++)
            {
                for (int x = 0; x < TexWidth; x++)
                {
                    float nx = (x + 0.5f) / TexWidth * 2f - 1f;
                    float ny = (y + 0.5f) / TexHeight * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);

                    // 中心浓、边缘柔：1-d 的平方让边缘过渡更自然
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a) * 0.85f + a * 0.15f;

                    px[y * TexWidth + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);

            _sprite = Sprite.Create(tex, new Rect(0, 0, TexWidth, TexHeight),
                                    new Vector2(0.5f, 0.5f), PixelsPerUnit,
                                    0, SpriteMeshType.FullRect);
            _sprite.name = "IsoShadow";
        }

        /// <summary>
        /// 在 parent 下挂一张影子。localPos 用父物体局部坐标（世界位置要靠父物体自己算好）。
        /// </summary>
        public static SpriteRenderer Attach(Transform parent, Vector2 localPos, int sortingOrder,
                                            float worldWidth, float alpha = 0.30f)
        {
            if (parent == null) return null;

            // 幂等：已有影子就复用，避免重复 BuildVisual 时叠出多份
            Transform existing = parent.Find("shadow");
            SpriteRenderer sr;
            if (existing != null)
            {
                sr = existing.GetComponent<SpriteRenderer>();
                if (sr == null) sr = existing.gameObject.AddComponent<SpriteRenderer>();
                existing.localPosition = new Vector3(localPos.x, localPos.y, -0.002f);
            }
            else
            {
                var go = new GameObject("shadow");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.002f);
                sr = go.AddComponent<SpriteRenderer>();
            }

            sr.sprite = ShadowSprite;
            sr.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            sr.sortingOrder = sortingOrder;

            // 目标世界宽度 → 局部缩放（同时抵消父物体的缩放，避免被放大两次）
            float spriteWidth = TexWidth / PixelsPerUnit;      // 1.28 世界单位
            float k = Mathf.Max(0.01f, worldWidth) / spriteWidth;
            Vector3 ps = parent.localScale;
            sr.transform.localScale = new Vector3(
                k / Mathf.Max(0.0001f, ps.x),
                k / Mathf.Max(0.0001f, ps.y),
                1f);
            return sr;
        }
    }
}
