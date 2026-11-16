using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 工厂的「原料」子菜单：像货运码头一样读一遍相连容器（仓库 + 紧邻工厂暂存），
    /// 只列出这栋厂当前配方能加工的东西，点一行就固定加工它；点「自动」恢复按库存自动挑。
    /// </summary>
    public class FactoryInputPanel : MonoBehaviour
    {
        public static FactoryInputPanel Instance { get; private set; }

        [Header("外观")]
        public float width = 520f;
        public float padding = 24f;
        public Color backgroundColor = new Color(0.055f, 0.07f, 0.09f, 1f);
        public Color accentColor = new Color(0.36f, 0.78f, 0.86f, 1f);
        public Color rowColor = new Color(0.10f, 0.13f, 0.16f, 1f);
        public Color rowSelected = new Color(0.14f, 0.34f, 0.38f, 1f);

        Font _font;
        RectTransform _panel;
        Text _title;
        Text _subtitle;
        RectTransform _rowsRoot;
        ScrollRect _scroll;
        Button _autoBtn;
        CanvasGroup _group;

        FactoryBuilding _factory;
        bool _targetShown;

        readonly List<Inventory> _sources = new List<Inventory>();
        readonly List<string> _ids = new List<string>();
        readonly List<float> _amounts = new List<float>();
        readonly List<float> _qualities = new List<float>();
        float _refreshTimer;

        public bool IsShown => _targetShown;

        void Awake()
        {
            Instance = this;
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
            BuildUI();
            _group.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (_panel == null) return false;
            Canvas canvas = _panel.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(_panel, screenPoint, cam);
        }

        public void Open(FactoryBuilding factory)
        {
            if (factory == null) return;
            _factory = factory;
            _group.gameObject.SetActive(true);
            if (_panel != null && _panel.anchoredPosition.x > width - 5f)
                _panel.anchoredPosition = new Vector2(width, 0f);
            _targetShown = true;
            Refresh();
        }

        public void Close()
        {
            _targetShown = false;
            // 关掉子菜单后回到工厂详情面板
            if (_factory != null)
            {
                var info = FindObjectOfType<TileInfoPanel>();
                if (info != null) info.Show(_factory);
            }
            _factory = null;
        }

        void Update()
        {
            if (_panel != null)
            {
                float target = _targetShown ? 0f : width;
                Vector2 p = _panel.anchoredPosition;
                p.x = Mathf.Lerp(p.x, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                _panel.anchoredPosition = p;
            }

            if (Input.GetKeyDown(KeyCode.Escape) && _targetShown)
            {
                Close();
                return;
            }

            if (!_targetShown || _factory == null) return;

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 0.6f;
                Collect();
                RebuildRows(keepScroll: true);
            }
        }

        // ---------------- 数据 ----------------

        void Collect()
        {
            _ids.Clear();
            _amounts.Clear();
            _qualities.Clear();

            var world = FactoryWorld.Instance;
            if (world == null || _factory == null) return;

            world.CollectSources(_factory, _sources);
            if (_sources.Count == 0) return;

            Recipe recipe = _factory.CurrentRecipe;
            var seen = new HashSet<string>();

            for (int s = 0; s < _sources.Count; s++)
            {
                var all = _sources[s].All;
                for (int k = 0; k < all.Count; k++)
                {
                    string id = all[k].ItemId;
                    if (all[k].Amount <= 1e-5f) continue;
                    if (!seen.Add(id)) continue;
                    if (!RecipeDatabase.MatchesPrimary(recipe, id)) continue;   // 过滤：不是这栋厂能加工的不显示
                    _ids.Add(id);
                }
            }

            for (int k = 0; k < _ids.Count; k++)
            {
                string id = _ids[k];
                float amount = world.AvailableSources(_sources, id);
                float weighted = 0f;
                float total = 0f;
                for (int s = 0; s < _sources.Count; s++)
                {
                    var all = _sources[s].All;
                    for (int a = 0; a < all.Count; a++)
                    {
                        if (all[a].ItemId != id) continue;
                        weighted += all[a].Amount * all[a].Quality;
                        total += all[a].Amount;
                    }
                }
                _amounts.Add(amount);
                _qualities.Add(total > 1e-5f ? weighted / total : 0f);
            }

            // 库存多的排前面
            for (int i = 0; i < _ids.Count - 1; i++)
                for (int j = i + 1; j < _ids.Count; j++)
                    if (_amounts[j] > _amounts[i])
                    {
                        (_ids[i], _ids[j]) = (_ids[j], _ids[i]);
                        (_amounts[i], _amounts[j]) = (_amounts[j], _amounts[i]);
                        (_qualities[i], _qualities[j]) = (_qualities[j], _qualities[i]);
                    }
        }

        // ---------------- UI ----------------

        void Refresh()
        {
            if (_factory == null) return;
            _title.text = $"原料选择（{_factory.gridX},{_factory.gridY}）";
            _subtitle.text = $"当前配方：{_factory.CurrentRecipe.DisplayName}　" +
                             (_factory.HasSelectedPrimary ? "已指定原料" : "自动挑库存最多的");
            Collect();
            RebuildRows(keepScroll: false);
            _refreshTimer = 0.6f;
        }

        void RebuildRows(bool keepScroll)
        {
            if (_rowsRoot == null) return;

            float keep = _scroll != null ? _scroll.verticalNormalizedPosition : 1f;
            for (int k = _rowsRoot.childCount - 1; k >= 0; k--)
            {
                // 先从容器摘出去再销毁：不然本帧内重建会和新行叠在一起
                var child = _rowsRoot.GetChild(k).gameObject;
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            const float rowH = 54f;
            float y = 0f;

            // 「自动」
            var autoRow = MakeRow(y, rowH);
            var autoLabel = MakeText("AutoLabel", autoRow, 20, FontStyle.Bold,
                _factory != null && !_factory.HasSelectedPrimary ? new Color(0.36f, 0.78f, 0.86f) : new Color(0.85f, 0.90f, 0.94f));
            autoLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            autoLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            autoLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
            autoLabel.rectTransform.offsetMax = new Vector2(-110f, 0f);
            autoLabel.alignment = TextAnchor.MiddleLeft;
            autoLabel.text = "自动（库存最多的可加工原料）";
            if (_factory != null && !_factory.HasSelectedPrimary) autoRow.GetComponent<Image>().color = rowSelected;

            var autoBtn = MakeButton("选自动", 0f, 0f, 92f, 34f,
                _factory != null && !_factory.HasSelectedPrimary ? new Color(0.20f, 0.52f, 0.60f, 1f)
                                                                  : new Color(0.18f, 0.24f, 0.28f, 1f), autoRow);
            var autoRect = autoBtn.GetComponent<RectTransform>();
            autoRect.anchorMin = autoRect.anchorMax = new Vector2(1f, 0.5f);
            autoRect.pivot = new Vector2(1f, 0.5f);
            autoRect.anchoredPosition = new Vector2(-8f, 0f);
            autoBtn.onClick.AddListener(() =>
            {
                if (_factory == null) return;
                _factory.SetSelectedPrimary("");
                GameAudio.Ensure().PlayUiClick();
                Refresh();
            });
            y += rowH + 6f;

            if (_ids.Count == 0)
            {
                var empty = MakeText("Empty", MakeRow(y, 46f), 18, FontStyle.Normal, new Color(0.72f, 0.78f, 0.84f));
                empty.rectTransform.anchorMin = Vector2.zero;
                empty.rectTransform.anchorMax = Vector2.one;
                empty.rectTransform.offsetMin = new Vector2(14f, 0f);
                empty.rectTransform.offsetMax = new Vector2(-14f, 0f);
                empty.alignment = TextAnchor.MiddleLeft;
                empty.text = "相连的容器里没有这栋厂能加工的原料";
                y += 52f;
            }
            else
            {
                for (int k = 0; k < _ids.Count; k++)
                {
                    string id = _ids[k];
                    bool selected = _factory != null && _factory.selectedPrimary == id;

                    var row = MakeRow(y, rowH);
                    var rowImg = row.GetComponent<Image>();
                    if (selected) rowImg.color = rowSelected;

                    string name = ItemDatabase.Get(id).DisplayName;
                    var label = MakeText("Name", row, 19, FontStyle.Normal,
                        selected ? new Color(0.56f, 0.92f, 0.96f) : new Color(0.92f, 0.95f, 0.98f));
                    label.rectTransform.anchorMin = new Vector2(0f, 0f);
                    label.rectTransform.anchorMax = new Vector2(0f, 1f);
                    label.rectTransform.pivot = new Vector2(0f, 0.5f);
                    label.rectTransform.sizeDelta = new Vector2(190f, 0f);
                    label.rectTransform.anchoredPosition = new Vector2(14f, 0f);
                    label.alignment = TextAnchor.MiddleLeft;
                    label.text = name;

                    var amount = MakeText("Amount", row, 17, FontStyle.Normal, new Color(0.95f, 0.85f, 0.55f));
                    amount.rectTransform.anchorMin = new Vector2(1f, 0f);
                    amount.rectTransform.anchorMax = new Vector2(1f, 1f);
                    amount.rectTransform.pivot = new Vector2(1f, 0.5f);
                    amount.rectTransform.sizeDelta = new Vector2(88f, 0f);
                    amount.rectTransform.anchoredPosition = new Vector2(-100f, 0f);
                    amount.alignment = TextAnchor.MiddleRight;
                    amount.text = $"{_amounts[k]:0.#} KG";

                    var quality = MakeText("Quality", row, 17, FontStyle.Normal, new Color(0.72f, 0.85f, 0.90f));
                    quality.rectTransform.anchorMin = new Vector2(1f, 0f);
                    quality.rectTransform.anchorMax = new Vector2(1f, 1f);
                    quality.rectTransform.pivot = new Vector2(1f, 0.5f);
                    quality.rectTransform.sizeDelta = new Vector2(56f, 0f);
                    quality.rectTransform.anchoredPosition = new Vector2(-194f, 0f);
                    quality.alignment = TextAnchor.MiddleRight;
                    quality.text = $"{Mathf.RoundToInt(_qualities[k] * 100f)}%";

                    // 右边那颗「选它 / 已选」只是样子，整行都能点（和仓库界面一样）
                    var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image));
                    var pillRect = pill.GetComponent<RectTransform>();
                    pillRect.SetParent(row, false);
                    pillRect.anchorMin = pillRect.anchorMax = new Vector2(1f, 0.5f);
                    pillRect.pivot = new Vector2(1f, 0.5f);
                    pillRect.sizeDelta = new Vector2(80f, 34f);
                    pillRect.anchoredPosition = new Vector2(-8f, 0f);
                    var pillImg = pill.GetComponent<Image>();
                    pillImg.color = selected ? new Color(0.20f, 0.52f, 0.60f, 1f) : new Color(0.18f, 0.26f, 0.30f, 1f);
                    pillImg.raycastTarget = false;
                    var pillText = MakeText("Label", pillRect, 17, FontStyle.Normal, new Color(0.95f, 0.97f, 1f));
                    pillText.rectTransform.anchorMin = Vector2.zero;
                    pillText.rectTransform.anchorMax = Vector2.one;
                    pillText.rectTransform.offsetMin = Vector2.zero;
                    pillText.rectTransform.offsetMax = Vector2.zero;
                    pillText.alignment = TextAnchor.MiddleCenter;
                    pillText.text = selected ? "已选" : "选它";

                    string picked = id;
                    var rowBtn = row.gameObject.AddComponent<Button>();
                    rowBtn.transition = Selectable.Transition.ColorTint;
                    rowBtn.targetGraphic = rowImg;
                    var rowColors = rowBtn.colors;
                    rowColors.normalColor = Color.white;
                    rowColors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
                    rowColors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
                    rowColors.fadeDuration = 0.06f;
                    rowBtn.colors = rowColors;
                    rowBtn.onClick.AddListener(() =>
                    {
                        if (_factory == null) return;
                        _factory.SetSelectedPrimary(picked);
                        GameAudio.Ensure().PlayUiClick();
                        Refresh();
                    });

                    y += rowH + 6f;
                }
            }

            _rowsRoot.sizeDelta = new Vector2(0f, y + 6f);
            if (keepScroll && _scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = keep;
            }
            else if (_scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        void BuildUI()
        {
            Transform existing = transform.Find("InputCanvas");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);

            var canvasGo = new GameObject("InputCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5015;
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
            _panel.anchoredPosition = new Vector2(width, 0f);
            panelGo.GetComponent<Image>().color = backgroundColor;

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(_panel, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accentGo.GetComponent<Image>().color = accentColor;

            _title = MakeText("Title", _panel, 24, FontStyle.Bold, new Color(0.96f, 0.97f, 0.98f));
            _title.rectTransform.anchorMin = new Vector2(0f, 1f);
            _title.rectTransform.anchorMax = new Vector2(1f, 1f);
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);
            _title.rectTransform.sizeDelta = new Vector2(-(padding * 2f), 34f);
            _title.rectTransform.anchoredPosition = new Vector2(padding, -padding);

            _subtitle = MakeText("Subtitle", _panel, 17, FontStyle.Normal, new Color(0.75f, 0.85f, 0.90f));
            _subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            _subtitle.rectTransform.sizeDelta = new Vector2(-(padding * 2f), 26f);
            _subtitle.rectTransform.anchoredPosition = new Vector2(padding, -padding - 34f);

            var close = MakeButton("关闭", width - padding - 84f, -padding - 2f, 80f, 32f,
                new Color(0.16f, 0.20f, 0.24f, 1f), _panel);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0f, 1f);
            closeRect.anchorMax = new Vector2(0f, 1f);
            closeRect.pivot = new Vector2(0f, 1f);
            closeRect.anchoredPosition = new Vector2(width - padding - 84f, -padding - 2f);
            close.onClick.AddListener(Close);

            // 滚动区
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var vp = vpGo.GetComponent<RectTransform>();
            vp.SetParent(_panel, false);
            vp.anchorMin = new Vector2(0f, 0f);
            vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(padding, padding);
            vp.offsetMax = new Vector2(-padding - 16f, -padding - 68f);

            var rowsGo = new GameObject("Rows", typeof(RectTransform));
            _rowsRoot = rowsGo.GetComponent<RectTransform>();
            _rowsRoot.SetParent(vp, false);
            // 横向跟着视口拉伸：之前锚在 (0.5,1) 宽 0，行全是 0 宽，所以点不到
            _rowsRoot.anchorMin = new Vector2(0f, 1f);
            _rowsRoot.anchorMax = new Vector2(1f, 1f);
            _rowsRoot.pivot = new Vector2(0.5f, 1f);
            _rowsRoot.anchoredPosition = Vector2.zero;
            _rowsRoot.sizeDelta = new Vector2(0f, 400f);

            _scroll = vpGo.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            _scroll.viewport = vp;
            _scroll.content = _rowsRoot;

            // 右侧滚动条（常驻，列表长了也能拖）
            var barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var bar = barGo.GetComponent<RectTransform>();
            bar.SetParent(_panel, false);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(-padding - 12f, padding);
            bar.offsetMax = new Vector2(-padding, -padding - 68f);
            barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handle = handleGo.GetComponent<RectTransform>();
            handle.SetParent(bar, false);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.sizeDelta = Vector2.zero;
            handleGo.GetComponent<Image>().color = new Color(0.36f, 0.78f, 0.86f, 0.72f);

            var sb = barGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handle;
            sb.targetGraphic = handleGo.GetComponent<Image>();
            _scroll.verticalScrollbar = sb;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
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

        Text MakeText(string name, RectTransform parent, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        Button MakeButton(string label, float x, float y, float w, float h, Color color, RectTransform parent)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, y);
            go.GetComponent<Image>().color = color;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.fadeDuration = 0.06f;
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
    }
}
