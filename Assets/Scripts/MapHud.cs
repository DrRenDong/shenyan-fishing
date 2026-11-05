using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 地图常驻 HUD（主界面用）：所持金、等级、经验条、待分配异能点。
    /// 和 BoatHud / BattleHud 一样运行时用代码生成 Canvas。
    /// </summary>
    [DisallowMultipleComponent]
    public class MapHud : MonoBehaviour
    {
        [Header("布局（参考分辨率 1280×720）")]
        [SerializeField] Vector2 screenOffset = new Vector2(28f, -24f);
        [SerializeField] Vector2 expBarSize = new Vector2(220f, 14f);

        [Header("颜色")]
        [SerializeField] Color expFill = new Color(0.95f, 0.78f, 0.32f);
        [SerializeField] Color textColor = new Color(1f, 0.96f, 0.86f);

        Canvas _canvas;
        Text _gold, _level, _points;
        Image _expFill;
        static Sprite _white;

        void Awake()
        {
            Build();
        }

        void Build()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("MapHudCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 18;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);

            // 左上角面板底
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var pRt = panel.GetComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = new Vector2(0f, 1f);
            pRt.pivot = new Vector2(0f, 1f);
            pRt.anchoredPosition = screenOffset;
            pRt.sizeDelta = new Vector2(260f, 118f);
            var pImg = panel.GetComponent<Image>();
            pImg.sprite = White;
            pImg.color = new Color(0f, 0f, 0f, 0.55f);
            pImg.raycastTarget = false;

            _gold = MakeText(panel.transform, font, "Gold", new Vector2(14f, -10f), new Vector2(232f, 26f),
                             TextAnchor.MiddleLeft, 22);
            _level = MakeText(panel.transform, font, "Level", new Vector2(14f, -36f), new Vector2(232f, 26f),
                              TextAnchor.MiddleLeft, 22);
            _points = MakeText(panel.transform, font, "Points", new Vector2(14f, -84f), new Vector2(232f, 26f),
                               TextAnchor.MiddleLeft, 20);

            // 经验条：独占一行，避免和等级文字打架
            var barGo = new GameObject("ExpBar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(panel.transform, false);
            var bRt = barGo.GetComponent<RectTransform>();
            bRt.anchorMin = bRt.anchorMax = new Vector2(0f, 1f);
            bRt.pivot = new Vector2(0f, 1f);
            bRt.anchoredPosition = new Vector2(14f, -66f);
            bRt.sizeDelta = new Vector2(232f, 14f);

            var bImg = barGo.GetComponent<Image>();
            bImg.sprite = White;
            bImg.color = new Color(0f, 0f, 0f, 0.55f);
            bImg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barGo.transform, false);
            var fRt = fillGo.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = new Vector2(2f, 2f);
            fRt.offsetMax = new Vector2(-2f, -2f);

            _expFill = fillGo.GetComponent<Image>();
            _expFill.sprite = White;
            _expFill.color = expFill;
            _expFill.type = Image.Type.Filled;
            _expFill.fillMethod = Image.FillMethod.Horizontal;
            _expFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _expFill.fillAmount = 0f;
            _expFill.raycastTarget = false;
        }

        Text MakeText(Transform parent, Font font, string name, Vector2 pos, Vector2 size,
                      TextAnchor align, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = textColor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        void Update()
        {
            if (_gold == null) return;

            _gold.text = "金币　" + PlayerProgress.Gold.ToString("N0");
            _level.text = "Lv." + PlayerProgress.Level + "　" + PlayerProgress.Exp + "/" + PlayerProgress.ExpToNext;
            _points.text = PlayerProgress.SkillPoints > 0
                ? "异能点　" + PlayerProgress.SkillPoints + "（待分配）"
                : "异能点　0";
            _expFill.fillAmount = PlayerProgress.ExpRatio;
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
