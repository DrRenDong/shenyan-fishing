using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 大鱼战斗的耐力条：上=鱼耐力（压制它才会掉），下=人耐力（被它拽着跑会掉）。
    /// 只在战斗状态显示，平时隐藏。和 BoatHud 一样用代码生成 Canvas。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleHud : MonoBehaviour
    {
        [Header("布局")]
        [SerializeField] Vector2 barSize = new Vector2(420f, 22f);
        [SerializeField] float topOffset = 64f;
        [SerializeField] float rowGap = 34f;

        [Header("颜色")]
        [SerializeField] Color fishColor = new Color(0.95f, 0.42f, 0.28f);
        [SerializeField] Color playerColor = new Color(0.36f, 0.82f, 0.55f);

        Canvas _canvas;
        Image _fishFill, _playerFill;
        Text _summonText;
        static Sprite _white;

        void Awake()
        {
            Build();
            Hide();
        }

        void Build()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("BattleHudCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 15;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);

            _fishFill = MakeBar(canvasGo.transform, font, "鱼耐力", -topOffset, fishColor);
            _playerFill = MakeBar(canvasGo.transform, font, "体力", -topOffset - rowGap, playerColor);

            // 召唤次数提示（条下方）
            var tipsGo = new GameObject("SummonTips", typeof(RectTransform), typeof(Text));
            tipsGo.transform.SetParent(canvasGo.transform, false);
            var tipsRt = tipsGo.GetComponent<RectTransform>();
            tipsRt.anchorMin = tipsRt.anchorMax = new Vector2(0.5f, 1f);
            tipsRt.pivot = new Vector2(0.5f, 1f);
            tipsRt.anchoredPosition = new Vector2(0f, -topOffset - rowGap * 2f);
            tipsRt.sizeDelta = new Vector2(700f, 32f);

            _summonText = tipsGo.GetComponent<Text>();
            _summonText.font = font;
            _summonText.fontSize = 20;
            _summonText.alignment = TextAnchor.MiddleCenter;
            _summonText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _summonText.color = new Color(1f, 0.95f, 0.75f);
            _summonText.raycastTarget = false;
            _summonText.text = "";

            var tipsOutline = tipsGo.AddComponent<Outline>();
            tipsOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            tipsOutline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        /// <summary>底部提示，例如「Q 海龟 ×1」。</summary>
        public void SetSummon(string text)
        {
            if (_summonText == null) return;
            if (_summonText.text != text) _summonText.text = text;
        }

        Image MakeBar(Transform parent, Font font, string label, float y, Color fill)
        {
            // 底条
            var bgGo = new GameObject("Bar_" + label, typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(parent, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 1f);
            bgRt.pivot = new Vector2(0.5f, 1f);
            bgRt.anchoredPosition = new Vector2(0f, y);
            bgRt.sizeDelta = barSize;

            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = White;
            bgImg.color = new Color(0f, 0f, 0f, 0.55f);

            // 填充
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bgGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = White;
            fillImg.color = fill;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;

            // 标签（放在条左边外侧）
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(bgGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(1f, 0.5f);
            labelRt.anchoredPosition = new Vector2(-10f, 0f);
            labelRt.sizeDelta = new Vector2(140f, 30f);

            var text = labelGo.GetComponent<Text>();
            text.font = font;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleRight;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.color = Color.white;
            text.text = label;

            var outline = labelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return fillImg;
        }

        public void Show()
        {
            Build();
            if (_canvas != null) _canvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        public void SetStamina(float fish, float player)
        {
            if (_fishFill != null) _fishFill.fillAmount = Mathf.Clamp01(fish);
            if (_playerFill != null) _playerFill.fillAmount = Mathf.Clamp01(player);
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
