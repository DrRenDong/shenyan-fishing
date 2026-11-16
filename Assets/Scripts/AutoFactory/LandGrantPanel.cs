using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 土地局批复：一次给 3 组地块，玩家选 1 组。
    /// 卡片上画 2×3 的小方格预览，颜色代表地形。
    /// </summary>
    public class LandGrantPanel : MonoBehaviour
    {
        public static LandGrantPanel Instance { get; private set; }

        [Header("外观")]
        public Color backgroundColor = new Color(0.055f, 0.07f, 0.09f, 1f);
        public Color cardColor = new Color(0.10f, 0.13f, 0.16f, 1f);
        public Color accentColor = new Color(0.55f, 0.85f, 0.55f, 1f);

        Font _font;
        CanvasGroup _group;
        RectTransform _cardsRoot;
        Text _subtitle;

        Action<LandGrant> _onPick;
        LandGrant[] _offers;

        static readonly Dictionary<string, Color> TileColors = new Dictionary<string, Color>
        {
            { "草地",     new Color(0.37f, 0.66f, 0.29f) },
            { "农田",     new Color(0.62f, 0.76f, 0.29f) },
            { "树林",     new Color(0.18f, 0.42f, 0.23f) },
            { "单层民居", new Color(0.78f, 0.71f, 0.55f) },
            { "两层民居", new Color(0.69f, 0.55f, 0.36f) },
            { "木板棚屋", new Color(0.63f, 0.47f, 0.30f) },
            { "巨石",     new Color(0.54f, 0.54f, 0.54f) },
            { "碎石",     new Color(0.69f, 0.66f, 0.61f) },
        };

        void Awake()
        {
            Instance = this;
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
            BuildUI();
            _group.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsShown => _targetShown;

        Action _onCancel;

        RectTransform _panel;
        bool _targetShown;
        public float slideDuration = 0.35f;
        public float shownY = 24f;
        const float hiddenY = -540f;

        public void Open(LandGrant[] offers, Action<LandGrant> onPick, Action onCancel = null)
        {
            _offers = offers;
            _onPick = onPick;
            _onCancel = onCancel;
            RebuildCards();
            _group.gameObject.SetActive(true);

            if (_panel != null && _panel.anchoredPosition.y < shownY - 40f)
                _panel.anchoredPosition = new Vector2(0f, hiddenY);

            _targetShown = true;
            FactoryTutorial.OnLandPanelOpened();   // 第一次批地时讲一次地块组拼接
        }

        public void Close()
        {
            _targetShown = false;
        }

        /// <summary>批地界面不允许取消：必须选一个方案。</summary>
        public void CancelPick()
        {
            Debug.Log("[土地局] 批地界面不能取消，请选择一个方案");
        }

        // ---------------- UI ----------------

        public void BuildUI()
        {
            Transform existing = transform.Find("LandGrantCanvas");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);

            var canvasGo = new GameObject("LandGrantCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5070;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _group = canvasGo.AddComponent<CanvasGroup>();

            // 不再压一层黑遮罩：面板从屏幕下方滑入，地图保持可见

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var panel = panelGo.GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(1000f, 520f);
            panel.anchoredPosition = new Vector2(0f, -540f);   // 先在屏幕外
            _panel = panel;
            panelGo.GetComponent<Image>().color = backgroundColor;

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(panel, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accentGo.GetComponent<Image>().color = accentColor;

            var title = MakeText(panel, "土地局批复", 30, FontStyle.Bold, new Color(0.96f, 0.97f, 0.98f));
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-64f, 40f);
            title.rectTransform.anchoredPosition = new Vector2(32f, -20f);

            _subtitle = MakeText(panel, "挑一块地，之后贴到现有陆地边上填海造陆", 20, FontStyle.Normal,
                new Color(0.72f, 0.80f, 0.86f));
            _subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            _subtitle.rectTransform.sizeDelta = new Vector2(-64f, 28f);
            _subtitle.rectTransform.anchoredPosition = new Vector2(32f, -62f);

            var cardsGo = new GameObject("Cards", typeof(RectTransform));
            _cardsRoot = cardsGo.GetComponent<RectTransform>();
            _cardsRoot.SetParent(panel, false);
            _cardsRoot.anchorMin = new Vector2(0f, 0f);
            _cardsRoot.anchorMax = new Vector2(1f, 1f);

            // 关闭按钮（没有遮罩了，给个明确的退出方式）
            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.SetParent(panel, false);
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(110f, 38f);
            closeRect.anchoredPosition = new Vector2(-22f, -14f);
            closeGo.GetComponent<Image>().color = new Color(0.24f, 0.28f, 0.32f, 1f);

            var closeBtn = closeGo.GetComponent<Button>();
            closeBtn.onClick.AddListener(CancelPick);
            closeGo.AddComponent<UIScaleOnHover>().Configure(1.06f, false);
            closeGo.AddComponent<UiPressScale>();

            var closeText = MakeText(closeRect, "关闭", 20, FontStyle.Normal, new Color(0.95f, 0.97f, 1f));
            closeText.rectTransform.anchorMin = Vector2.zero;
            closeText.rectTransform.anchorMax = Vector2.one;
            closeText.rectTransform.offsetMin = Vector2.zero;
            closeText.rectTransform.offsetMax = Vector2.zero;
            closeText.alignment = TextAnchor.MiddleCenter;            _cardsRoot.offsetMin = new Vector2(40f, 24f);
            _cardsRoot.offsetMax = new Vector2(-40f, -96f);

            // 批地不能取消：把关闭按钮藏掉
            closeGo.SetActive(false);
        }

        void RebuildCards()
        {
            for (int k = _cardsRoot.childCount - 1; k >= 0; k--) DestroyImmediate(_cardsRoot.GetChild(k).gameObject);
            if (_offers == null) return;

            const float cardW = 280f;
            const float cardH = 400f;
            const float gap = 30f;
            float totalW = _offers.Length * cardW + (_offers.Length - 1) * gap;
            float startX = -totalW * 0.5f + cardW * 0.5f;

            for (int k = 0; k < _offers.Length; k++)
            {
                LandGrant grant = _offers[k];
                var cardGo = new GameObject($"Card_{k + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                var card = cardGo.GetComponent<RectTransform>();
                card.SetParent(_cardsRoot, false);
                card.anchorMin = new Vector2(0.5f, 0.5f);
                card.anchorMax = new Vector2(0.5f, 0.5f);
                card.pivot = new Vector2(0.5f, 0.5f);
                card.sizeDelta = new Vector2(cardW, cardH);
                card.anchoredPosition = new Vector2(startX + k * (cardW + gap), 0f);
                cardGo.GetComponent<Image>().color = cardColor;

                var btn = cardGo.GetComponent<Button>();
                var hover = cardGo.AddComponent<UIScaleOnHover>();
                hover.Configure(1.04f, false);
                cardGo.AddComponent<UiPressScale>();

                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                btn.colors = colors;
                LandGrant captured = grant;
                btn.onClick.AddListener(() => Pick(captured));

                var label = MakeText(card, $"方案 {k + 1}", 22, FontStyle.Bold, new Color(0.92f, 0.96f, 1f));
                label.rectTransform.anchorMin = new Vector2(0f, 1f);
                label.rectTransform.anchorMax = new Vector2(1f, 1f);
                label.rectTransform.pivot = new Vector2(0.5f, 1f);
                label.rectTransform.sizeDelta = new Vector2(-20f, 28f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -14f);

                BuildMiniGrid(card, grant, new Vector2(0f, 0f));

                var desc = MakeText(card, grant.Describe(), 18, FontStyle.Normal, new Color(0.80f, 0.86f, 0.90f));
                desc.rectTransform.anchorMin = new Vector2(0f, 0f);
                desc.rectTransform.anchorMax = new Vector2(1f, 0f);
                desc.rectTransform.pivot = new Vector2(0.5f, 0f);
                desc.rectTransform.sizeDelta = new Vector2(-24f, 90f);
                desc.rectTransform.anchoredPosition = new Vector2(0f, 16f);
            }
        }

        /// <summary>
        /// 用主场景同一套伪 3D 瓦片画 2×3 预览：
        /// 按等距坐标错位摆放；民居/棚屋先垫一块「泥土整块」再把房子抬到与地面齐平，
        /// 树林垫「草地」并同样抬高（和地图上的摆法一致，不会被埋进地里）。
        /// </summary>
        void BuildMiniGrid(RectTransform card, LandGrant grant, Vector2 center)
        {
            const float unit = 66f;                        // 一格底面宽（像素）
            float stepX = unit * 0.5f;
            float stepY = unit * 0.25f;
            float canvas = unit * (384f / 304f);           // 瓦片画布尺寸
            float pivotOffsetY = canvas * (0.5f - 12f / 384f);   // 瓦片锚点在底面「前角」

            var cells = new System.Collections.Generic.List<(int col, int row, float x, float y)>();
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            for (int row = 0; row < grant.rows; row++)
            {
                for (int col = 0; col < grant.cols; col++)
                {
                    float x = (col - row) * stepX;
                    float y = -(col + row) * stepY;
                    cells.Add((col, row, x, y));
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            }

            float cx = (minX + maxX) * 0.5f;
            float cy = (minY + maxY) * 0.5f;

            // 远的先画（col+row 大的靠前，后画）
            cells.Sort((a, b) => (a.col + a.row).CompareTo(b.col + b.row));

            void DrawTile(string tileName, Vector2 pos, float liftPx)
            {
                Sprite sprite = TileCatalog.LoadSprite(tileName);
                if (sprite == null) return;

                var cellGo = new GameObject("Cell_" + tileName, typeof(RectTransform), typeof(Image));
                var rect = cellGo.GetComponent<RectTransform>();
                rect.SetParent(card, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(canvas, canvas);
                rect.anchoredPosition = new Vector2(pos.x, pos.y + pivotOffsetY + liftPx);

                var img = cellGo.GetComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            for (int k = 0; k < cells.Count; k++)
            {
                var c = cells[k];
                string tile = grant.At(c.col, c.row);
                if (string.IsNullOrEmpty(tile)) continue;      // 缺失格留空

                var pos = new Vector2(center.x + (c.x - cx), center.y + (c.y - cy));
                bool prop = LandGrant.IsProp(tile);

                if (!prop)
                {
                    DrawTile(tile, pos, 0f);
                    continue;
                }

                // 民居 / 棚屋：脚下先垫一块泥土整块；树林：垫草地
                string ground = tile == "树林" ? "草地" : "泥土整块";
                DrawTile(ground, pos, 0f);

                TileDef def = TileCatalog.Find(tile);
                float liftPx = (def != null ? def.LiftPixels : 0f) / 304f * unit;
                DrawTile(tile, pos, liftPx);
            }
        }

        void Update()
        {
            if (_panel == null) return;
            float target = _targetShown ? shownY : hiddenY;
            Vector2 p = _panel.anchoredPosition;
            p.y = Mathf.Lerp(p.y, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            _panel.anchoredPosition = p;
        }
        void Pick(LandGrant grant)
        {
            Action<LandGrant> callback = _onPick;
            _onPick = null;
            _onCancel = null;
            Close();
            callback?.Invoke(grant);
        }

        Text MakeText(RectTransform parent, string text, int size, FontStyle style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.text = text;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
