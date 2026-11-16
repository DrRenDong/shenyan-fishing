using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>建造面板：右侧列出可建的厂/设施/路面，点一下就进入建造模式。</summary>
    public class BuildPanel : MonoBehaviour
    {
        public static BuildPanel Instance { get; private set; }

        [Header("外观")]
        public float width = 520f;
        public float padding = 26f;
        public Color backgroundColor = new Color(0.055f, 0.07f, 0.09f, 1f);
        public Color accentColor = new Color(0.36f, 0.78f, 0.86f, 1f);
        public Color rowColor = new Color(0.10f, 0.13f, 0.16f, 1f);
        public Color buttonColor = new Color(0.20f, 0.42f, 0.50f, 1f);

        Font _font;
        RectTransform _panel;
        RectTransform _rowsRoot;
        ScrollRect _scroll;
        Text _title;
        Text _footer;
        CanvasGroup _group;
        bool _shown;
        bool _targetShown;

        void Awake()
        {
            Instance = this;
            BuildUI();
            // 不再整个禁用物体（那样 Update 不跑、B 键也会失效），只让它停在屏幕右侧外
            var canvas = transform.Find("BuildCanvas");
            var panel = canvas != null ? canvas.Find("Panel") as RectTransform : null;
            if (panel != null) panel.anchoredPosition = new Vector2(width + 20f, 0f);
            _targetShown = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BuildUI()
        {
            Transform existing = transform.Find("BuildCanvas");
            if (existing != null) DestroyImmediate(existing.gameObject);
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);

            var canvasGo = new GameObject("BuildCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5020;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _group = canvasGo.AddComponent<CanvasGroup>();

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.SetParent(canvasGo.transform, false);
            _panel.anchorMin = new Vector2(1f, 0f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(1f, 0.5f);
            _panel.sizeDelta = new Vector2(width, 0f);
            _panel.anchoredPosition = new Vector2(width + 20f, 0f);   // 先在屏幕右侧外
            panelGo.GetComponent<Image>().color = backgroundColor;

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(_panel, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accentGo.GetComponent<Image>().color = accentColor;

            _title = MakeText("Title", _panel, 26, FontStyle.Bold, new Color(0.96f, 0.97f, 0.98f));
            _title.rectTransform.anchorMin = new Vector2(0f, 1f);
            _title.rectTransform.anchorMax = new Vector2(1f, 1f);
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);
            _title.rectTransform.sizeDelta = new Vector2(-(padding * 2f), 40f);
            _title.rectTransform.anchoredPosition = new Vector2(padding, -padding);

            // 列表区：滚动视口。16 项在标准尺寸刚好放得下，但 UI 放大后 Canvas 逻辑高度变小，
            // 不滚就永远看不到底部那几座厂，所以这里必须带 ScrollRect。
            var areaGo = new GameObject("RowsArea", typeof(RectTransform), typeof(ScrollRect));
            var area = areaGo.GetComponent<RectTransform>();
            area.SetParent(_panel, false);
            area.anchorMin = new Vector2(0f, 0f);
            area.anchorMax = new Vector2(1f, 1f);
            area.offsetMin = new Vector2(padding, padding + 52f);
            area.offsetMax = new Vector2(-padding, -padding - 52f);

            _scroll = areaGo.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.SetParent(area, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-14f, 0f);   // 右侧留出滚动条

            var rowsGo = new GameObject("Rows", typeof(RectTransform));
            _rowsRoot = rowsGo.GetComponent<RectTransform>();
            _rowsRoot.SetParent(viewport, false);
            _rowsRoot.anchorMin = new Vector2(0f, 1f);
            _rowsRoot.anchorMax = new Vector2(1f, 1f);
            _rowsRoot.pivot = new Vector2(0.5f, 1f);
            _rowsRoot.sizeDelta = Vector2.zero;
            _rowsRoot.anchoredPosition = Vector2.zero;

            _scroll.viewport = viewport;
            _scroll.content = _rowsRoot;

            // 右侧竖向滚动条（AutoHide：内容放得下时自动隐藏）
            var barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var barRect = barGo.GetComponent<RectTransform>();
            barRect.SetParent(area, false);
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(12f, 0f);
            barRect.anchoredPosition = Vector2.zero;
            barGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handle = handleGo.GetComponent<RectTransform>();
            handle.SetParent(barRect, false);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = new Vector2(1f, 1f);
            handle.offsetMax = new Vector2(-1f, -1f);
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = new Color(0.36f, 0.78f, 0.86f, 0.55f);

            var sb = barGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handle;
            sb.targetGraphic = handleImg;
            sb.size = 0.25f;
            _scroll.verticalScrollbar = sb;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            _footer = MakeText("Footer", _panel, 18, FontStyle.Normal, new Color(0.75f, 0.82f, 0.88f));
            _footer.rectTransform.anchorMin = new Vector2(0f, 0f);
            _footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            _footer.rectTransform.pivot = new Vector2(0.5f, 0f);
            _footer.rectTransform.sizeDelta = new Vector2(-(padding * 2f), 48f);
            _footer.rectTransform.anchoredPosition = new Vector2(0f, 8f);

            var close = MakeButton("关闭", width - padding - 90f, -padding - 4f, 80f, 34f,
                                   new Color(0.16f, 0.20f, 0.24f, 1f), _panel);
            close.onClick.AddListener(Close);
        }

        Text MakeText(string name, RectTransform parent, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Button MakeButton(string label, float x, float y, float w, float h, Color color, RectTransform parent)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent != null ? parent : _panel, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, y);
            go.GetComponent<Image>().color = color;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            btn.colors = colors;

            var text = MakeText("Label", rect, 18, FontStyle.Normal, new Color(0.95f, 0.97f, 1f));
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            return btn;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            _shown = true;
            Transform canvas = transform.Find("BuildCanvas");
            if (canvas != null) canvas.gameObject.SetActive(true);

            if (_panel != null && _panel.anchoredPosition.x > width - 5f)
                _panel.anchoredPosition = new Vector2(width + 20f, 0f);

            _targetShown = true;
            Refresh();
        }

        public void Hide()
        {
            _shown = false;
            _targetShown = false;
        }

        public void Close()
        {
            Hide();
            if (BuildController.Instance != null) BuildController.Instance.Cancel();
        }

        public bool IsShown => _targetShown;

        void Update()
        {
            if (_panel != null)
            {
                float target = _targetShown ? 0f : width + 20f;
                Vector2 p = _panel.anchoredPosition;
                p.x = Mathf.Lerp(p.x, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                _panel.anchoredPosition = p;
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                if (_shown) Close();
                else
                {
                    if (BuildController.Instance != null) BuildController.Instance.Cancel();
                    Open();
                }
            }

            // 面板开着的时候金币会变（点下一回合、自动出货），标题要跟着刷新
            if (_shown && _title != null)
            {
                _goldRefresh -= Time.unscaledDeltaTime;
                if (_goldRefresh <= 0f)
                {
                    _goldRefresh = 0.3f;
                    _title.text = $"建造（金币 {PlayerProgress.Gold:N0}）";
                }
            }
        }

        float _goldRefresh;

        public void Refresh()
        {
            if (_rowsRoot == null) return;
            for (int k = _rowsRoot.childCount - 1; k >= 0; k--) DestroyImmediate(_rowsRoot.GetChild(k).gameObject);

            _title.text = $"建造（金币 {PlayerProgress.Gold:N0}）";
            _footer.text = "选一项 → 点地图格子放下（绿框可建 / 红框不可建）\nEsc 取消建造";

            float y = 0f;

            // 拆除模式
            var demolishRow = MakeRow(y, 48f);
            var dLabel = MakeText("DemolishLabel", demolishRow, 19, FontStyle.Normal, new Color(0.95f, 0.80f, 0.80f));
            dLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            dLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            dLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
            dLabel.rectTransform.offsetMax = new Vector2(-110f, 0f);
            dLabel.alignment = TextAnchor.MiddleLeft;
            dLabel.text = "拆除模式（拆建筑退一半造价，地块不能拆）";

            var demolishBtn = MakeButton("拆除", 0f, 0f, 88f, 32f,
                                         new Color(0.45f, 0.24f, 0.24f, 1f), demolishRow);
            var dBtnRect = demolishBtn.GetComponent<RectTransform>();
            dBtnRect.anchorMin = new Vector2(1f, 0.5f);
            dBtnRect.anchorMax = new Vector2(1f, 0.5f);
            dBtnRect.pivot = new Vector2(1f, 0.5f);
            dBtnRect.anchoredPosition = new Vector2(-8f, 0f);
            demolishBtn.onClick.AddListener(() =>
            {
                Hide();
                BuildController.Instance.EnterDemolish();
            });
            y += 58f;

            y = BuildSection("路面 / 设施 / 港口", y);
            foreach (var def in BuildCatalog.All)
            {
                if (def.Category == BuildCategory.Factory) continue;
                y = BuildRow(def, y);
            }

            y = BuildSection("工厂", y + 8f);
            foreach (var def in BuildCatalog.All)
            {
                if (def.Category != BuildCategory.Factory) continue;
                y = BuildRow(def, y);
            }

            // 内容总高 = 最后一行底部；Rows 是顶端对齐的滚动内容
            if (_rowsRoot != null)
            {
                _rowsRoot.sizeDelta = new Vector2(0f, y + 6f);
                Canvas.ForceUpdateCanvases();
            }
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        RectTransform MakeRow(float y, float height)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(Image));
            var row = rowGo.GetComponent<RectTransform>();
            row.SetParent(_rowsRoot, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, height);
            row.anchoredPosition = new Vector2(0f, -y);
            rowGo.GetComponent<Image>().color = rowColor;
            return row;
        }

        float BuildSection(string text, float y)
        {
            var t = MakeText("Section", _rowsRoot, 20, FontStyle.Bold, new Color(0.36f, 0.78f, 0.86f));
            var rect = t.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 26f);
            rect.anchoredPosition = new Vector2(0f, -y);
            t.text = text;
            return y + 30f;
        }

        float BuildRow(BuildDef def, float y)
        {
            const float rowHeight = 48f;
            var row = MakeRow(y, rowHeight - 4f);

            // 缩略图
            var thumbGo = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
            var thumb = thumbGo.GetComponent<RectTransform>();
            thumb.SetParent(row, false);
            thumb.anchorMin = new Vector2(0f, 0.5f);
            thumb.anchorMax = new Vector2(0f, 0.5f);
            thumb.pivot = new Vector2(0f, 0.5f);
            thumb.sizeDelta = new Vector2(40f, 40f);
            thumb.anchoredPosition = new Vector2(8f, 0f);
            var thumbImg = thumbGo.GetComponent<Image>();
            thumbImg.sprite = TileCatalog.LoadSprite(def.TileId);
            thumbImg.preserveAspect = true;
            thumbImg.raycastTarget = false;

            bool unlocked = BuildCatalog.IsUnlocked(def);
            var nameColor = unlocked ? new Color(0.90f, 0.94f, 0.97f) : new Color(0.48f, 0.52f, 0.56f);
            var name = MakeText("Name", row, 19, FontStyle.Normal, nameColor);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(0f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 0.5f);
            name.rectTransform.sizeDelta = new Vector2(118f, 0f);
            name.rectTransform.anchoredPosition = new Vector2(72f, 0f);
            name.alignment = TextAnchor.MiddleLeft;
            name.fontSize = 18;
            name.text = def.Name;

            var cost = MakeText("Cost", row, 18, FontStyle.Normal, new Color(0.95f, 0.85f, 0.55f));
            cost.rectTransform.anchorMin = new Vector2(0f, 0f);
            cost.rectTransform.anchorMax = new Vector2(0f, 1f);
            cost.rectTransform.pivot = new Vector2(0f, 0.5f);
            cost.rectTransform.sizeDelta = new Vector2(80f, 0f);
            cost.rectTransform.anchoredPosition = new Vector2(192f, 0f);
            cost.alignment = TextAnchor.MiddleRight;
            cost.text = unlocked ? $"{def.Cost} 金" : "未解锁";

            var turns = MakeText("Turns", row, 18, FontStyle.Normal, new Color(0.72f, 0.85f, 0.90f));
            turns.rectTransform.anchorMin = new Vector2(0f, 0f);
            turns.rectTransform.anchorMax = new Vector2(0f, 1f);
            turns.rectTransform.pivot = new Vector2(0f, 0.5f);
            turns.rectTransform.sizeDelta = new Vector2(80f, 0f);
            turns.rectTransform.anchoredPosition = new Vector2(276f, 0f);
            turns.alignment = TextAnchor.MiddleRight;
            turns.text = def.Turns > 0 ? $"{def.Turns} 回合" : "即建";

            var btn = MakeButton(unlocked ? "建造" : "锁定", 0f, 0f, 84f, 34f,
                                  unlocked ? buttonColor : new Color(0.22f, 0.24f, 0.26f, 1f), row);
            btn.interactable = unlocked;
            var btnRect = btn.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-8f, 0f);

            var captured = def;
            btn.onClick.AddListener(() =>
            {
                Hide();
                BuildController.Instance.Enter(captured);
            });

            return y + rowHeight;
        }
    }
}
