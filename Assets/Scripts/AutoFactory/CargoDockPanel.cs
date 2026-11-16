using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 货运码头的售出 / 购入面板（订单制）。
    /// 下单后货会按码头运转速率慢慢流过去；左边读相连仓库与工厂的存货，右边是玩家买不到的原料。
    /// 顶部有「自动供货」开关：开启后按整片路网的需求自动补货。
    /// </summary>
    public class CargoDockPanel : MonoBehaviour
    {
        public static CargoDockPanel Instance { get; private set; }

        [Header("外观")]
        public float width = 540f;
        public float padding = 26f;
        [Tooltip("底部给 HUD 操作按钮留出的高度（避免物品清单压在右下角按钮条上）")]
        public float hudReserve = 120f;
        public float slideDuration = 0.24f;
        public Color backgroundColor = new Color(0.055f, 0.07f, 0.09f, 1f);
        public Color accentColor = new Color(0.36f, 0.78f, 0.86f, 1f);
        public Color rowColor = new Color(0.10f, 0.13f, 0.16f, 1f);
        public Color buttonColor = new Color(0.20f, 0.42f, 0.50f, 1f);
        public Color cancelColor = new Color(0.34f, 0.22f, 0.22f, 1f);

        Font _font;
        RectTransform _panel;
        RectTransform _rowsRoot;
        ScrollRect _rowsScroll;
        Text _header;
        Text _title;
        Text _levelText;
        Button _upgradeButton;
        Text _autoSupplyLabel;
        CanvasGroup _group;

        CargoDockBuilding _dock;
        float _shown;
        bool _targetShown;
        float _refreshTimer;

        readonly List<Stock> _sellList = new List<Stock>();
        readonly List<Text> _orderTexts = new List<Text>();

        static string FormatOrder(DockOrder order)
        {
            ItemDef def = ItemDatabase.Get(order.ItemId);
            string dir = order.IsBuy ? "<color=#7FD1E8>买入</color>" : "<color=#E8C97F>卖出</color>";
            return $"{dir} {def.DisplayName} 剩余 {ItemDatabase.Describe(order.ItemId, order.Remaining)}　" +
                   $"单价 {order.UnitPrice:0.0}/{def.UnitText}" +
                   (order.AutoCreated ? "　<color=#8A98A6>自动</color>" : "");
        }

        void Awake()
        {
            Instance = this;
            BuildUI();
            Apply(0f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BuildUI()
        {
            Transform existing = transform.Find("CargoDockCanvas");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);

            var canvasGo = new GameObject("CargoDockCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5010;   // 必须高于场景里所有 SpriteRenderer 的排序号，否则会被地图压住
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
            panelGo.GetComponent<Image>().color = backgroundColor;

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(_panel, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accentGo.GetComponent<Image>().color = accentColor;

            _title = MakeText("Title", _panel, 27, FontStyle.Bold, new Color(0.96f, 0.97f, 0.98f), TextAnchor.UpperLeft);
            _title.rectTransform.anchorMin = new Vector2(0f, 1f);
            _title.rectTransform.anchorMax = new Vector2(1f, 1f);
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);
            _title.rectTransform.sizeDelta = new Vector2(-(padding * 2f), 40f);
            _title.rectTransform.anchoredPosition = new Vector2(padding, -padding);

            _header = MakeText("Header", _panel, 21, FontStyle.Normal, new Color(0.80f, 0.86f, 0.90f), TextAnchor.UpperLeft);
            _header.rectTransform.anchorMin = new Vector2(0f, 1f);
            _header.rectTransform.anchorMax = new Vector2(1f, 1f);
            _header.rectTransform.pivot = new Vector2(0.5f, 1f);
            _header.rectTransform.sizeDelta = new Vector2(-(padding * 2f), 120f);
            _header.rectTransform.anchoredPosition = new Vector2(padding, -padding - 46f);
            _header.lineSpacing = 1.35f;

            // 列表区：ScrollRect（列表超过面板高度时可以滚动）
            var areaGo = new GameObject("RowsArea", typeof(RectTransform), typeof(ScrollRect));
            var area = areaGo.GetComponent<RectTransform>();
            area.SetParent(_panel, false);
            area.anchorMin = new Vector2(0f, 0f);
            area.anchorMax = new Vector2(1f, 1f);
            area.offsetMin = new Vector2(padding, padding + hudReserve);
            area.offsetMax = new Vector2(-padding, -padding - 222f);

            _rowsScroll = areaGo.GetComponent<ScrollRect>();
            _rowsScroll.horizontal = false;
            _rowsScroll.vertical = true;
            _rowsScroll.movementType = ScrollRect.MovementType.Clamped;
            _rowsScroll.scrollSensitivity = 42f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.SetParent(area, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-14f, 0f);   // 右边给滚动条留位置

            var rowsGo = new GameObject("Rows", typeof(RectTransform));
            _rowsRoot = rowsGo.GetComponent<RectTransform>();
            _rowsRoot.SetParent(viewport, false);
            _rowsRoot.anchorMin = new Vector2(0f, 1f);
            _rowsRoot.anchorMax = new Vector2(1f, 1f);
            _rowsRoot.pivot = new Vector2(0.5f, 1f);
            _rowsRoot.sizeDelta = Vector2.zero;
            _rowsRoot.anchoredPosition = Vector2.zero;

            _rowsScroll.viewport = viewport;
            _rowsScroll.content = _rowsRoot;

            // 竖向滚动条
            var barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var bar = barGo.GetComponent<RectTransform>();
            bar.SetParent(area, false);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(10f, 0f);
            bar.anchoredPosition = Vector2.zero;
            barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.07f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handle = handleGo.GetComponent<RectTransform>();
            handle.SetParent(bar, false);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.sizeDelta = Vector2.zero;
            var handleImage = handleGo.GetComponent<Image>();
            handleImage.color = new Color(0.36f, 0.78f, 0.86f, 0.55f);

            var sb = barGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handle;
            sb.targetGraphic = handleImage;
            sb.size = 0.35f;
            _rowsScroll.verticalScrollbar = sb;
            _rowsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var close = MakeButton(_panel, "关闭", width - 96f, -padding - 4f, 80f, 34f, new Color(0.16f, 0.20f, 0.24f, 1f));
            close.onClick.AddListener(Hide);

            // 等级与升级：Lv2/3 提升转运速率，日费同步上涨
            _levelText = MakeText("Level", _panel, 19, FontStyle.Normal, new Color(0.58f, 0.82f, 0.90f), TextAnchor.MiddleLeft);
            _levelText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _levelText.rectTransform.anchorMax = new Vector2(0f, 1f);
            _levelText.rectTransform.pivot = new Vector2(0f, 1f);
            _levelText.rectTransform.sizeDelta = new Vector2(width - padding * 2f - 140f, 32f);
            _levelText.rectTransform.anchoredPosition = new Vector2(padding, -padding - 172f);

            _upgradeButton = MakeButton(_panel, "升级", width - padding - 130f, -padding - 172f, 130f, 32f,
                new Color(0.30f, 0.44f, 0.26f, 1f));
            _upgradeButton.onClick.AddListener(() =>
            {
                if (_dock != null && _dock.TryUpgrade()) Refresh();
            });
        }

        Text MakeText(string name, RectTransform parent, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            return text;
        }

        Button MakeButton(RectTransform parent, string label, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
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

            var text = MakeText("Label", rect, 18, FontStyle.Normal, new Color(0.95f, 0.97f, 1f), TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.text = label;
            return btn;
        }

        /// <summary>在行里放一格固定位置的文字（用来对齐成列）。</summary>
        Text MakeCell(RectTransform row, int size, Color color, TextAnchor anchor, float x, float w)
        {
            var text = MakeText("Cell", row, size, FontStyle.Normal, color, anchor);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(w, 0f);
            rect.anchoredPosition = new Vector2(x, 0f);
            return text;
        }

        public void Show(CargoDockBuilding dock)
        {
            _dock = dock;
            _targetShown = true;
            Refresh();
        }

        public void Hide()
        {
            _dock = null;
            _targetShown = false;
        }

        public bool IsShown => _targetShown;

        /// <summary>屏幕点是否落在面板矩形里（用来挡住「点面板顺带点了地图」）。</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (_panel == null) return false;
            Canvas canvas = _panel.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(_panel, screenPoint, cam);
        }

        public void SnapTo(float t)
        {
            _shown = Mathf.Clamp01(t);
            Apply(_shown);
        }

        void Update()
        {
            float speed = 1f / Mathf.Max(0.01f, slideDuration);
            _shown = Mathf.MoveTowards(_shown, _targetShown ? 1f : 0f, Time.unscaledDeltaTime * speed);
            Apply(_shown);

            if (_dock == null) return;
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < 0.25f) return;
            _refreshTimer = 0f;
            UpdateHeader();

            // 售出/购入列表读的是相连仓库和工厂的实时存货，光刷订单文字不够，
            // 这里大约每秒整表重建一次（行数不多）。
            _listRefresh += 0.25f;
            if (_listRefresh >= 1f)
            {
                _listRefresh = 0f;
                Refresh(true);
                return;
            }

            if (_orderTexts.Count != _dock.orders.Count) Refresh(true);
            else
            {
                for (int k = 0; k < _orderTexts.Count; k++)
                    if (_orderTexts[k] != null) _orderTexts[k].text = FormatOrder(_dock.orders[k]);
            }
        }

        float _listRefresh;

        void Apply(float t)
        {
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            _panel.anchoredPosition = new Vector2(Mathf.Lerp(width, 0f, eased), 0f);
            if (_group != null) _group.alpha = Mathf.Clamp01(t * 1.4f);
        }

        void UpdateHeader()
        {
            if (_dock == null) return;
            _title.text = $"货运码头（{_dock.gridX},{_dock.gridY}）";
            _header.text =
                (_dock.powered ? "已通电" : "<color=#FF8A8A>未通电</color>") + "\n" +
                $"每日费用：{_dock.dailyCost:N0}/日　　运转速率：{_dock.EffectiveThroughput:0.##}/回合\n" +
                $"金币：{PlayerProgress.Gold:N0}" +
                (_dock.Enclosed ? "\n<color=#FF8A8A>这片水体被陆地围死了，船进不来 → 转运停摆</color>" : "");
            if (_autoSupplyLabel != null)
            {
                _autoSupplyLabel.text = "";
            }
            UpdateLevelRow();
        }

        /// <summary>等级行与升级按钮的状态（满级时置灰）。</summary>
        void UpdateLevelRow()
        {
            if (_dock == null) return;
            if (_levelText != null)
                _levelText.text = $"等级 {_dock.level}/{PlacedTile.MaxLevel}　" +
                                  $"速率 ×{_dock.LevelSpeedMultiplier:0.##}　运营费 ×{_dock.LevelCostMultiplier:0.##}";
            if (_upgradeButton == null) return;
            bool can = _dock.CanUpgrade;
            _upgradeButton.interactable = can;
            var label = _upgradeButton.GetComponentInChildren<Text>();
            if (label != null) label.text = can ? $"升级 {_dock.UpgradeCost}" : "已满级";
            var image = _upgradeButton.GetComponent<Image>();
            if (image != null)
                image.color = can ? new Color(0.30f, 0.44f, 0.26f, 1f) : new Color(0.22f, 0.24f, 0.28f, 1f);
        }

        public void Refresh(bool keepScroll = false)
        {
            if (_dock == null || _rowsRoot == null) return;
            UpdateHeader();

            float scroll = keepScroll && _rowsScroll != null ? _rowsScroll.verticalNormalizedPosition : 1f;

            for (int k = _rowsRoot.childCount - 1; k >= 0; k--) DestroyImmediate(_rowsRoot.GetChild(k).gameObject);
            _orderTexts.Clear();

            float y = 0f;

            // ---- 两个开关 ----
            y = BuildToggleRow(y);

            // ---- 进行中的订单 ----
            y = BuildSection($"进行中的订单（{_dock.orders.Count}）", y);
            if (_dock.orders.Count == 0)
            {
                BuildRow("<color=#8A98A6>没有订单</color>", "取消", null, y, null);
                y += RowHeight;
            }
            else
            {
                int index = 0;
                foreach (var order in _dock.orders)
                {
                    var captured = order;
                    var text = BuildRow(FormatOrder(order), "取消", cancelColor, y,
                                        () => { _dock.CancelOrder(captured); Refresh(); });
                    _orderTexts.Add(text);
                    y += RowHeight;
                    index++;
                }
            }

            // ---- 售出 ----
            y = BuildSection("售出（相连仓库 / 工厂）", y + 10f);
            y = BuildColumnHeader("物品", "数量", "品质", "单价", y);
            _dock.BuildSellList(_sellList);
            int shown = 0;
            foreach (var stock in _sellList)
            {
                ItemDef def = ItemDatabase.Get(stock.ItemId);
                string amount = ItemDatabase.Describe(stock.ItemId, stock.Amount);
                string quality = def.HasQuality ? $"{Mathf.RoundToInt(stock.Quality * 100f)}%" : "—";
                string price = $"{ItemDatabase.SellPrice(stock.ItemId, stock.Quality):0.0}/{def.UnitText}";
                string id = stock.ItemId;
                float sellAmount = stock.Amount;
                BuildSellRow(def.DisplayName, amount, quality, price, y,
                             () => { _dock.QueueSell(id, sellAmount); Refresh(); });
                y += RowHeight;
                shown++;
            }
            if (_sellList.Count == 0)
            {
                BuildRow("<color=#8A98A6>相连的仓库里没有可卖的东西</color>", null, null, y, null);
                y += RowHeight;
            }

            // ---- 购入 ----
            y = BuildSection("购入原料（玩家不能自产）", y + 10f);
            y = BuildColumnHeader("物品", "单价", null, null, y);
            if (_dock != null) _dock.EnsureBuyCatalog();   // 老码头只有 3 种，先把工厂要的辅料补齐
            foreach (var itemId in _dock.buyCatalog)
            {
                ItemDef def = ItemDatabase.Get(itemId);
                string id = itemId;
                BuildBuyRow(def.DisplayName, $"{def.BasePrice:0.0}/{def.UnitText}", y,
                            () => { _dock.QueueBuy(id, 50f); Refresh(); });
                y += RowHeight;
            }

            // 内容高度交给滚动条判断（列表长了就能滚）
            _rowsRoot.sizeDelta = new Vector2(0f, y + 6f);

            // 定时刷新时保留玩家当前的滚动位置，否则下拉列表会被拉回最顶上
            if (_rowsScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _rowsScroll.verticalNormalizedPosition = scroll;
            }
        }

        const float RowHeight = 38f;
        const float ColNameX = 8f;
        const float ColNameW = 132f;
        const float ColAmountX = 146f;
        const float ColAmountW = 76f;
        const float ColQualityX = 226f;
        const float ColQualityW = 60f;
        const float ColPriceX = 290f;
        const float ColPriceW = 92f;

        RectTransform MakeRowBackground(float y)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(Image));
            var row = rowGo.GetComponent<RectTransform>();
            row.SetParent(_rowsRoot, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, RowHeight - 4f);
            row.anchoredPosition = new Vector2(0f, -y);
            rowGo.GetComponent<Image>().color = rowColor;
            return row;
        }

        /// <summary>一行两个开关：自动供货 / 自动出货。</summary>
        float BuildToggleRow(float y)
        {
            var row = MakeRowBackground(y);
            row.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.18f, 1f);

            float w = (width - padding * 2f - 14f) * 0.5f;

            var supply = MakeButton(row, _dock.autoSupply ? "自动供货 开" : "自动供货 关",
                                    8f, 0f, w, 30f,
                                    _dock.autoSupply ? new Color(0.20f, 0.52f, 0.60f, 1f) : new Color(0.22f, 0.24f, 0.28f, 1f));
            var supplyRect = supply.GetComponent<RectTransform>();
            supplyRect.anchorMin = new Vector2(0f, 0.5f);
            supplyRect.anchorMax = new Vector2(0f, 0.5f);
            supplyRect.pivot = new Vector2(0f, 0.5f);
            supplyRect.anchoredPosition = new Vector2(8f, 0f);
            supply.onClick.AddListener(() => { _dock.autoSupply = !_dock.autoSupply; Refresh(); });

            var sell = MakeButton(row, _dock.autoSell ? "自动出货 开" : "自动出货 关",
                                  0f, 0f, w, 30f,
                                  _dock.autoSell ? new Color(0.20f, 0.52f, 0.60f, 1f) : new Color(0.22f, 0.24f, 0.28f, 1f));
            var sellRect = sell.GetComponent<RectTransform>();
            sellRect.anchorMin = new Vector2(0f, 0.5f);
            sellRect.anchorMax = new Vector2(0f, 0.5f);
            sellRect.pivot = new Vector2(0f, 0.5f);
            sellRect.anchoredPosition = new Vector2(8f + w + 6f, 0f);
            sell.onClick.AddListener(() => { _dock.autoSell = !_dock.autoSell; Refresh(); });

            return y + RowHeight;
        }

        float BuildColumnHeader(string c1, string c2, string c3, string c4, float y)
        {
            var row = MakeRowBackground(y);
            row.GetComponent<Image>().color = new Color(0.13f, 0.16f, 0.19f, 1f);
            var gray = new Color(0.55f, 0.62f, 0.68f);

            MakeCell(row, 16, gray, TextAnchor.MiddleLeft, ColNameX, ColNameW).text = c1;
            if (!string.IsNullOrEmpty(c2))
                MakeCell(row, 16, gray, TextAnchor.MiddleRight, ColAmountX, ColAmountW).text = c2;
            if (!string.IsNullOrEmpty(c3))
                MakeCell(row, 16, gray, TextAnchor.MiddleRight, ColQualityX, ColQualityW).text = c3;
            if (!string.IsNullOrEmpty(c4))
                MakeCell(row, 16, gray, TextAnchor.MiddleRight, ColPriceX, ColPriceW).text = c4;
            return y + RowHeight;
        }

        float BuildSellRow(string name, string amount, string quality, string price, float y, System.Action onClick)
        {
            var row = MakeRowBackground(y);
            var white = new Color(0.88f, 0.92f, 0.95f);
            MakeCell(row, 18, white, TextAnchor.MiddleLeft, ColNameX, ColNameW).text = name;
            MakeCell(row, 18, white, TextAnchor.MiddleRight, ColAmountX, ColAmountW).text = amount;
            MakeCell(row, 18, new Color(0.72f, 0.85f, 0.90f), TextAnchor.MiddleRight, ColQualityX, ColQualityW).text = quality;
            MakeCell(row, 18, new Color(0.95f, 0.85f, 0.55f), TextAnchor.MiddleRight, ColPriceX, ColPriceW).text = price;
            AddRowButton(row, "全卖", buttonColor, onClick);
            return y + RowHeight;
        }

        float BuildBuyRow(string name, string price, float y, System.Action onClick)
        {
            var row = MakeRowBackground(y);
            var white = new Color(0.88f, 0.92f, 0.95f);
            MakeCell(row, 18, white, TextAnchor.MiddleLeft, ColNameX, ColNameW).text = name;
            MakeCell(row, 18, new Color(0.95f, 0.85f, 0.55f), TextAnchor.MiddleRight, ColPriceX, ColPriceW).text = price;
            AddRowButton(row, "买 50", buttonColor, onClick);
            return y + RowHeight;
        }

        void AddRowButton(RectTransform row, string label, Color tint, System.Action onClick)
        {
            if (onClick == null) return;
            var btn = MakeButton(row, label, 0f, 0f, 86f, 30f, tint);
            var btnRect = btn.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-8f, 0f);
            btn.onClick.AddListener(() => onClick());
        }

        float BuildSection(string text, float y)
        {
            var t = MakeText("Section", _rowsRoot, 20, FontStyle.Bold, new Color(0.36f, 0.78f, 0.86f), TextAnchor.UpperLeft);
            var rect = t.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 26f);
            rect.anchoredPosition = new Vector2(0f, -y);
            t.text = text;
            return y + 30f;
        }

        Text BuildRow(string label, string buttonLabel, Color? buttonTint, float y, System.Action onClick)
        {
            var row = MakeRowBackground(y);
            var text = MakeText("Label", row, 18, FontStyle.Normal, new Color(0.88f, 0.92f, 0.95f), TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(12f, 0f);
            text.rectTransform.offsetMax = new Vector2(-100f, 0f);
            text.text = label;

            if (buttonLabel != null)
                AddRowButton(row, buttonLabel, buttonTint ?? buttonColor, onClick);
            return text;
        }
    }
}
