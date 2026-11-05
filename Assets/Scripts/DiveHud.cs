using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 屏幕右侧的潜望条（常驻，非战斗专用）：
    /// 相机停在默认高度时是满的，玩家把视角往下滑就逐渐消耗，滑到神眼极限见底。
    /// </summary>
    [DisallowMultipleComponent]
    public class DiveHud : MonoBehaviour
    {
        [Header("布局")]
        [SerializeField] Vector2 barSize = new Vector2(22f, 320f);
        [SerializeField] float rightMargin = 46f;

        [Header("颜色")]
        [SerializeField] Color frameColor = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] Color fillColor = new Color(0.45f, 0.72f, 0.95f);

        Canvas _canvas;
        Image _fill;
        CameraFollow _follow;
        static Sprite _white;

        void Awake()
        {
            Build();
        }

        void Build()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("DiveHudCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 14;                 // 比战斗 HUD 低一层

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 18);

            // 底框
            var bgGo = new GameObject("DiveBar", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.pivot = new Vector2(1f, 0.5f);
            bgRt.anchoredPosition = new Vector2(-rightMargin, 0f);
            bgRt.sizeDelta = barSize;

            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = White;
            bgImg.color = frameColor;

            // 填充（自下而上）
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bgGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);

            _fill = fillGo.GetComponent<Image>();
            _fill.sprite = White;
            _fill.color = fillColor;
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Vertical;
            _fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _fill.fillAmount = 1f;
            _fill.raycastTarget = false;

            // 标签
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(bgGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 1f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 10f);
            labelRt.sizeDelta = new Vector2(120f, 24f);

            var label = labelGo.GetComponent<Text>();
            label.font = font;
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.95f, 0.97f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = "潜望";

            var outline = labelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        void Update()
        {
            if (_fill == null) return;
            if (_follow == null) _follow = FindObjectOfType<CameraFollow>();
            _fill.fillAmount = _follow != null ? _follow.DiveRatio : 1f;
        }

        static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                }
                return _white;
            }
        }
    }
}
