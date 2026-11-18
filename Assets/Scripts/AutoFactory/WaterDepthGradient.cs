using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 水体纵深：从水面向下压一层「越深越暗、越偏深蓝」的渐变，
    /// 让水下有明确的深度感，同时把远处的鱼和海床压进水里。
    ///
    /// 贴图是运行时生成的竖条渐变（顶部全透明、底部最深），
    /// 顶边钉在水面，随相机横向跟随、按视野高度拉伸。
    /// </summary>
    [DefaultExecutionOrder(542)]
    public class WaterDepthGradient : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("摆放")]
        public float surfaceY = 16f;
        [Tooltip("向下延伸超出画面底部多少")]
        public float bottomMargin = 6f;
        [Tooltip("横向铺开倍数（1 = 正好铺满屏宽）")]
        public float widthScale = 1.3f;

        [Header("外观")]
        public Color deepColor = new Color(0.03f, 0.10f, 0.19f, 1f);
        [Range(0f, 1f)] public float maxAlpha = 0.55f;
        [Tooltip("越大越「上半段还很透、下半段迅速变深」")]
        public float power = 1.5f;
        public int sortingOrder = 3050;

        const int TexW = 64;
        const int TexH = 256;
        const float PixelsPerUnit = 100f;

        static Sprite _gradient;
        SpriteRenderer _sr;

        void Start()
        {
            EnsureSprite();

            var go = new GameObject("depth_gradient");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sprite = _gradient;
            _sr.sortingOrder = sortingOrder;
        }

        void EnsureSprite()
        {
            if (_gradient != null) return;

            var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false, true);
            tex.name = "WaterDepthGradientTex";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var px = new Color32[TexW * TexH];
            for (int y = 0; y < TexH; y++)
            {
                // 贴图 y=0 是底部：底部最深，顶部全透明
                float k = 1f - y / (float)(TexH - 1);
                float a = Mathf.Pow(Mathf.Clamp01(k), Mathf.Max(0.1f, power));
                byte alpha = (byte)Mathf.RoundToInt(a * 255f);
                for (int x = 0; x < TexW; x++)
                    px[y * TexW + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);

            _gradient = Sprite.Create(tex, new Rect(0, 0, TexW, TexH),
                                      new Vector2(0.5f, 1f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            _gradient.name = "WaterDepthGradient";
        }

        void LateUpdate()
        {
            if (_sr == null || _gradient == null) return;
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            float viewH = cam.orthographic ? cam.orthographicSize * 2f : 10f;
            float viewW = viewH * Mathf.Max(0.1f, cam.aspect);
            Vector3 camPos = cam.transform.position;

            float bottom = camPos.y - viewH * 0.5f - bottomMargin;
            float height = Mathf.Max(1f, surfaceY - bottom);

            Vector2 size = _gradient.bounds.size;
            _sr.transform.position = new Vector3(camPos.x, surfaceY, 0f);
            _sr.transform.localScale = new Vector3(
                viewW * Mathf.Max(1f, widthScale) / Mathf.Max(0.01f, size.x),
                height / Mathf.Max(0.01f, size.y),
                1f);

            Color c = deepColor;
            c.a = maxAlpha;
            _sr.color = c;
        }
    }
}
