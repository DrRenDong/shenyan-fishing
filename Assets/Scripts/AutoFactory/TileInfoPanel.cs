using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 右侧详情面板：点地块时从右边缓动滑出，显示该地块的信息。
    /// 面板完全由代码生成（Canvas + CanvasScaler + GraphicRaycaster 三件套），不依赖预制体。
    /// </summary>
    public class TileInfoPanel : MonoBehaviour
    {
        public static TileInfoPanel Instance { get; private set; }

        [Header("外观")]
        public float width = 440f;
        public float padding = 26f;
        [Tooltip("底部给 HUD 操作按钮留出的高度（否则详情面板的按钮会和右下角按钮条重叠）")]
        public float hudReserve = 120f;
        public float slideDuration = 0.24f;
        public Color backgroundColor = new Color(0.055f, 0.07f, 0.09f, 1f);
        public Color accentColor = new Color(0.36f, 0.78f, 0.86f, 1f);

        Font _font;
        RectTransform _panel;
        RectTransform _actionsRoot;
        RectTransform _bodyArea;
        ScrollRect _bodyScroll;
        Text _title;
        Text _body;
        Image _background;
        CanvasGroup _group;

        float _shown;      // 0 = 收起，1 = 展开
        bool _targetShown;
        PlacedTile _current;

        void Awake()
        {
            Instance = this;
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
            BuildUI();
            Apply(0f, true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>构建面板 UI（Awake 自动调用；已存在则重建，避免场景里留着一份又新加一份）。</summary>
        public void BuildUI()
        {
            Transform existing = transform.Find("TileInfoCanvas");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
            var canvasGo = new GameObject("TileInfoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            // 用 Screen Space - Camera 而不是 Overlay：这样主相机截图/录屏能带上 UI
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5000;   // UI 必须高于船（950）等精灵，否则会被地图物体压住
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
            _panel.anchoredPosition = Vector2.zero;

            _background = panelGo.GetComponent<Image>();
            _background.color = backgroundColor;

            // 左侧高亮竖条
            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(_panel, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accent.anchoredPosition = Vector2.zero;
            accentGo.GetComponent<Image>().color = accentColor;

            _title = MakeText("Title", _panel, 27, FontStyle.Bold, new Color(0.96f, 0.97f, 0.98f));
            var titleRect = _title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(-(padding * 2f), 44f);
            titleRect.anchoredPosition = new Vector2(padding, -padding);

            var dividerGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            var divider = dividerGo.GetComponent<RectTransform>();
            divider.SetParent(_panel, false);
            divider.anchorMin = new Vector2(0f, 1f);
            divider.anchorMax = new Vector2(1f, 1f);
            divider.pivot = new Vector2(0.5f, 1f);
            divider.sizeDelta = new Vector2(-(padding * 2f), 2f);
            divider.anchoredPosition = new Vector2(padding, -padding - 52f);
            dividerGo.GetComponent<Image>().color = new Color(0.36f, 0.78f, 0.86f, 0.35f);

            // 正文：ScrollRect（仓库库存这种列表超过面板高度时可以滚）
            var bodyAreaGo = new GameObject("BodyArea", typeof(RectTransform), typeof(ScrollRect));
            _bodyArea = bodyAreaGo.GetComponent<RectTransform>();
            _bodyArea.SetParent(_panel, false);
            _bodyArea.anchorMin = new Vector2(0f, 0f);
            _bodyArea.anchorMax = new Vector2(1f, 1f);
            _bodyArea.offsetMin = new Vector2(padding, padding + hudReserve);
            _bodyArea.offsetMax = new Vector2(-padding, -padding - 66f);

            _bodyScroll = bodyAreaGo.GetComponent<ScrollRect>();
            _bodyScroll.horizontal = false;
            _bodyScroll.vertical = true;
            _bodyScroll.movementType = ScrollRect.MovementType.Clamped;
            _bodyScroll.scrollSensitivity = 42f;

            var bodyViewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var bodyViewport = bodyViewportGo.GetComponent<RectTransform>();
            bodyViewport.SetParent(_bodyArea, false);
            bodyViewport.anchorMin = Vector2.zero;
            bodyViewport.anchorMax = Vector2.one;
            bodyViewport.offsetMin = Vector2.zero;
            bodyViewport.offsetMax = new Vector2(-14f, 0f);

            _body = MakeText("Body", bodyViewport, 22, FontStyle.Normal, new Color(0.82f, 0.87f, 0.90f));
            var bodyRect = _body.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.sizeDelta = Vector2.zero;
            bodyRect.anchoredPosition = Vector2.zero;
            _body.lineSpacing = 1.4f;
            _body.alignment = TextAnchor.UpperLeft;
            _body.verticalOverflow = VerticalWrapMode.Overflow;

            var bodyFitter = _body.gameObject.AddComponent<ContentSizeFitter>();
            bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _bodyScroll.viewport = bodyViewport;
            _bodyScroll.content = bodyRect;

            var bodyBarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var bodyBar = bodyBarGo.GetComponent<RectTransform>();
            bodyBar.SetParent(_bodyArea, false);
            bodyBar.anchorMin = new Vector2(1f, 0f);
            bodyBar.anchorMax = new Vector2(1f, 1f);
            bodyBar.pivot = new Vector2(1f, 0.5f);
            bodyBar.sizeDelta = new Vector2(10f, 0f);
            bodyBar.anchoredPosition = Vector2.zero;
            bodyBarGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.07f);

            var bodyHandleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var bodyHandle = bodyHandleGo.GetComponent<RectTransform>();
            bodyHandle.SetParent(bodyBar, false);
            bodyHandle.anchorMin = Vector2.zero;
            bodyHandle.anchorMax = Vector2.one;
            bodyHandle.sizeDelta = Vector2.zero;
            var bodyHandleImage = bodyHandleGo.GetComponent<Image>();
            bodyHandleImage.color = new Color(0.36f, 0.78f, 0.86f, 0.55f);

            var bodySb = bodyBarGo.GetComponent<Scrollbar>();
            bodySb.direction = Scrollbar.Direction.BottomToTop;
            bodySb.handleRect = bodyHandle;
            bodySb.targetGraphic = bodyHandleImage;
            bodySb.size = 0.35f;
            _bodyScroll.verticalScrollbar = bodySb;
            _bodyScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var actionsGo = new GameObject("Actions", typeof(RectTransform));
            _actionsRoot = actionsGo.GetComponent<RectTransform>();
            _actionsRoot.SetParent(_panel, false);
            _actionsRoot.anchorMin = new Vector2(0f, 0f);
            _actionsRoot.anchorMax = new Vector2(1f, 0f);
            _actionsRoot.pivot = new Vector2(0.5f, 0f);
            _actionsRoot.sizeDelta = new Vector2(-(padding * 2f), 200f);
            _actionsRoot.anchoredPosition = new Vector2(0f, padding + hudReserve);
            _actionsRoot.gameObject.SetActive(false);
        }

        /// <summary>清空底部按钮区。</summary>
        void ClearActions()
        {
            for (int k = _actionsRoot.childCount - 1; k >= 0; k--) DestroyImmediate(_actionsRoot.GetChild(k).gameObject);
            _actionsRoot.gameObject.SetActive(false);
        }

        Button MakeButton(string label, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_actionsRoot, false);
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

        /// <summary>捕捞港口：按钮用来进入「指定捕捞点」模式。</summary>
        void BuildPortActions(FishingPortBuilding port)
        {
            _actionsRoot.gameObject.SetActive(true);

            var caption = MakeText("Caption", _actionsRoot, 20, FontStyle.Bold, new Color(0.36f, 0.78f, 0.86f));
            var capRect = caption.rectTransform;
            capRect.anchorMin = new Vector2(0f, 1f);
            capRect.anchorMax = new Vector2(1f, 1f);
            capRect.pivot = new Vector2(0.5f, 1f);
            capRect.sizeDelta = new Vector2(0f, 26f);
            capRect.anchoredPosition = Vector2.zero;
            caption.text = "操作";
            caption.alignment = TextAnchor.UpperLeft;

            // 右上角：升级（Lv2/3 提升捕捞速率，日费同步上涨）
            AddUpgradeButton(port, width - padding * 2f - 130f);

            // 还在施工的港口不能指定捕捞点：按钮置灰，等施工条走完再点
            bool ready = !port.UnderConstruction;
            Color pickColor = ready ? new Color(0.20f, 0.52f, 0.60f, 1f) : new Color(0.22f, 0.24f, 0.26f, 1f);
            Color autoColor = ready ? new Color(0.18f, 0.38f, 0.34f, 1f) : new Color(0.22f, 0.24f, 0.26f, 1f);

            float w = width - padding * 2f;
            var pick = MakeButton("指定捕捞点（点一片水面）", 0f, -34f, w, 40f, pickColor);
            pick.interactable = ready;
            pick.onClick.AddListener(() =>
            {
                var picker = FindObjectOfType<TilePicker>();
                if (picker != null) picker.BeginPickTarget(port);
                Hide();
            });

            var auto = MakeButton("自动选最近的聚集点", 0f, -80f, w, 36f, autoColor);
            auto.interactable = ready;
            auto.onClick.AddListener(() =>
            {
                if (TryPickNearestSpot(port)) Show(port);
            });

            if (port.HasTarget)
            {
                var clear = MakeButton("取消捕捞点", 0f, -124f, w, 34f, new Color(0.34f, 0.22f, 0.22f, 1f));
                clear.onClick.AddListener(() =>
                {
                    port.ClearTarget();
                    Show(port);
                });
            }

            // 停工 / 开工：停工时不捞也不扣日费
            var toggle = MakeButton(port.running ? "停工（不捕捞、不扣费）" : "开工", 0f, -166f, w, 34f,
                port.running ? new Color(0.45f, 0.26f, 0.24f, 1f) : new Color(0.20f, 0.42f, 0.30f, 1f));
            toggle.onClick.AddListener(() =>
            {
                port.running = !port.running;
                Show(port);
            });
        }

        /// <summary>把捕捞点指到最近的、还活着的海产聚集点上。</summary>
        static bool TryPickNearestSpot(FishingPortBuilding port)
        {
            SeaSpotBuilding best = null;
            int bestDist = int.MaxValue;
            foreach (var spot in FindObjectsOfType<SeaSpotBuilding>())
            {
                if (spot == null || !spot.gameObject.activeInHierarchy) continue;
                int d = Mathf.Max(Mathf.Abs(spot.gridX - port.gridX), Mathf.Abs(spot.gridY - port.gridY));
                if (d < bestDist) { bestDist = d; best = spot; }
            }
            if (best == null) return false;

            port.SetTarget(new Vector2Int(best.gridX, best.gridY));
            return true;
        }

        /// <summary>给工厂列出可选配方，点一下就切换。</summary>
        void BuildRecipeActions(FactoryBuilding factory)
        {
            _actionsRoot.gameObject.SetActive(true);

            var caption = MakeText("Caption", _actionsRoot, 20, FontStyle.Bold, new Color(0.36f, 0.78f, 0.86f));
            var capRect = caption.rectTransform;
            capRect.anchorMin = new Vector2(0f, 1f);
            capRect.anchorMax = new Vector2(1f, 1f);
            capRect.pivot = new Vector2(0.5f, 1f);
            capRect.sizeDelta = new Vector2(0f, 26f);
            capRect.anchoredPosition = Vector2.zero;
            caption.text = "可选配方";
            caption.alignment = TextAnchor.UpperLeft;

            // 右上角：开工 / 停工
            bool running = factory.running;
            var toggleBtn = MakeButton(running ? "停工" : "开工", width - padding * 2f - 110f, -2f, 110f, 30f,
                running ? new Color(0.34f, 0.24f, 0.24f, 1f) : new Color(0.22f, 0.40f, 0.28f, 1f));
            toggleBtn.onClick.AddListener(() =>
            {
                factory.SetRunning(!factory.running);
                Show(factory);
            });

            AddUpgradeButton(factory, width - padding * 2f - 130f - 148f);

            // 原料子菜单：读取相连容器，过滤出这栋厂能加工的东西
            string pickLabel = factory.HasSelectedPrimary
                ? $"原料：{ItemDatabase.Get(factory.selectedPrimary).DisplayName}（点这里换）"
                : "原料：自动（库存最多）　点这里指定";
            var pick = MakeButton(pickLabel, 0f, -30f, width - padding * 2f, 30f,
                new Color(0.16f, 0.34f, 0.40f, 1f));
            pick.onClick.AddListener(() =>
            {
                var input = FindObjectOfType<FactoryInputPanel>();
                if (input != null) input.Open(factory);
                Hide();
            });

            float w = (width - padding * 2f - 10f) * 0.5f;
            Recipe[] options = RecipeDatabase.ForType(factory.factoryType);
            string currentId = factory.CurrentRecipe.Id;

            for (int k = 0; k < options.Length; k++)
            {
                Recipe recipe = options[k];
                bool current = recipe.Id == currentId;
                float x = (k % 2) * (w + 10f);
                float y = -70f - (k / 2) * 46f;

                var btn = MakeButton(recipe.DisplayName, x, y, w, 40f,
                    current ? new Color(0.20f, 0.52f, 0.60f, 1f) : new Color(0.14f, 0.18f, 0.22f, 1f));
                string id = recipe.Id;
                btn.onClick.AddListener(() =>
                {
                    factory.SetRecipe(id);
                    Show(factory);
                });
            }
        }

        /// <summary>等级与倍率那一行（只有可升级的建筑才显示）。</summary>
        static string LevelLine(PlacedTile tile)
        {
            bool upgradeable = tile is FactoryBuilding || tile is SortingFacility
                               || tile is CargoDockBuilding || tile is FishingPortBuilding;
            if (!upgradeable) return "";
            return "\n" + $"<color=#7FD1E8>等级 {tile.level}/{PlacedTile.MaxLevel}</color>　" +
                   $"速率 ×{tile.LevelSpeedMultiplier:0.##}　运营费 ×{tile.LevelCostMultiplier:0.##}";
        }

        /// <summary>分拣区 / 货运码头的操作区：只有升级按钮。</summary>
        void BuildManageActions(PlacedTile tile)
        {
            _actionsRoot.gameObject.SetActive(true);

            var caption = MakeText("Caption", _actionsRoot, 20, FontStyle.Bold, new Color(0.36f, 0.78f, 0.86f));
            var capRect = caption.rectTransform;
            capRect.anchorMin = new Vector2(0f, 1f);
            capRect.anchorMax = new Vector2(1f, 1f);
            capRect.pivot = new Vector2(0.5f, 1f);
            capRect.sizeDelta = new Vector2(0f, 26f);
            capRect.anchoredPosition = Vector2.zero;
            caption.text = "管理";
            caption.alignment = TextAnchor.UpperLeft;

            // 分拣区也能手动停工（不挑拣、不扣日运营费）
            if (tile is SortingFacility sorter)
            {
                bool running = sorter.running;
                var toggle = MakeButton(running ? "停工（不挑拣、不扣费）" : "开工",
                    0f, -34f, width - padding * 2f, 36f,
                    running ? new Color(0.45f, 0.26f, 0.24f, 1f) : new Color(0.20f, 0.42f, 0.30f, 1f));
                toggle.onClick.AddListener(() =>
                {
                    sorter.SetRunning(!sorter.running);
                    Show(sorter);
                });
            }

            AddUpgradeButton(tile, width - padding * 2f - 130f);
        }

        /// <summary>右上角的升级按钮。</summary>
        void AddUpgradeButton(PlacedTile tile, float x)
        {
            if (!tile.CanUpgrade)
            {
                var label = MakeText("MaxLevel", _actionsRoot, 18, FontStyle.Normal, new Color(0.55f, 0.60f, 0.64f));
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(130f, 30f);
                rect.anchoredPosition = new Vector2(x, -2f);
                label.text = "已满级";
                label.alignment = TextAnchor.MiddleCenter;
                return;
            }

            var btn = MakeButton($"升级 {tile.UpgradeCost}", x, -2f, 130f, 30f,
                new Color(0.30f, 0.44f, 0.26f, 1f));
            btn.onClick.AddListener(() =>
            {
                if (tile.TryUpgrade()) Show(tile);
            });
        }

        Text MakeText(string name, RectTransform parent, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            return text;
        }

        public void Show(PlacedTile tile)
        {
            if (tile == null)
            {
                Hide();
                return;
            }
            _current = tile;
            _portTurnsShown = tile is FishingPortBuilding port0 && port0.UnderConstruction ? port0.BuildTurnsLeft : 0;
            _title.text = BuildTitle(tile);
            _body.text = BuildBody(tile) + LevelLine(tile);
            _targetShown = true;

            if (tile is FishingPortBuilding portTile) ShowPath(portTile);
            else HidePath();

            if (_actionsRoot != null)
            {
                ClearActions();
                bool hasActions = tile is FactoryBuilding || tile is FishingPortBuilding
                                  || tile is SortingFacility || tile is CargoDockBuilding;

                if (tile is FactoryBuilding factory) BuildRecipeActions(factory);
                else if (tile is FishingPortBuilding port) BuildPortActions(port);
                else if (hasActions) BuildManageActions(tile);

                // 有按钮时正文往上收，避免压住按钮
                _bodyArea.offsetMin = new Vector2(padding, padding + hudReserve + (hasActions ? 212f : 0f));
            }

            if (_bodyScroll != null) _bodyScroll.verticalNormalizedPosition = 1f;
        }

        public void Show(string title, string body)
        {
            _current = null;
            _title.text = title;
            _body.text = body;
            _targetShown = true;
            if (_actionsRoot != null)
            {
                ClearActions();
                _bodyArea.offsetMin = new Vector2(padding, padding + hudReserve);
            }
            if (_bodyScroll != null) _bodyScroll.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
            _current = null;
            _targetShown = false;
            HidePath();
        }

        // ---------------- 捕捞路径线 ----------------

        LineRenderer _pathLine;

        /// <summary>航线统一用红色，和地面/海面区分开。</summary>
        public static readonly Color RouteColor = new Color(0.95f, 0.30f, 0.28f, 0.95f);

        void EnsurePathLine()
        {
            if (_pathLine == null)
            {
                Transform old = transform.Find("FishingPath");
                if (old != null) _pathLine = old.GetComponent<LineRenderer>();

                if (_pathLine == null)
                {
                    var go = new GameObject("FishingPath");
                    go.transform.SetParent(transform, false);
                    _pathLine = go.AddComponent<LineRenderer>();
                }
            }

            // 复用旧线时也重设样式，保证颜色跟代码一致（避免场景里存着旧颜色）
            _pathLine.useWorldSpace = true;
            _pathLine.loop = false;
            _pathLine.widthMultiplier = 0.05f;
            _pathLine.numCapVertices = 2;
            _pathLine.numCornerVertices = 2;
            _pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _pathLine.receiveShadows = false;

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = RouteColor;
            _pathLine.sharedMaterial = mat;
            _pathLine.startColor = RouteColor;
            _pathLine.endColor = RouteColor;
            _pathLine.sortingOrder = 420;
            _pathLine.enabled = false;
        }

        /// <summary>把捕捞港到捕捞点的水面最短路径画成一条线。</summary>
        void ShowPath(FishingPortBuilding port)
        {
            port.RefreshWaterState();
            var path = port.WaterPath;
            if (path == null || path.Count < 2)
            {
                HidePath();
                return;
            }

            EnsurePathLine();
            _pathLine.positionCount = path.Count;
            for (int k = 0; k < path.Count; k++)
            {
                var cell = path[k];
                Vector3 p = PlacedTile.CellToWorld(cell.x, cell.y) + new Vector3(0f, 0.72f, 0f);
                _pathLine.SetPosition(k, p);
            }
            _pathLine.enabled = true;
        }

        void HidePath()
        {
            if (_pathLine != null) _pathLine.enabled = false;
        }

        public bool IsShown => _targetShown;
        public PlacedTile Current => _current;

        /// <summary>屏幕点是否落在面板矩形里（用来挡住「点面板顺带点了地图」）。</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (_panel == null) return false;
            Canvas canvas = _panel.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(_panel, screenPoint, cam);
        }

        /// <summary>直接把滑出进度设到某个值（0 收起 / 1 展开），用于截图和验证。</summary>
        public void SnapTo(float t)
        {
            _shown = Mathf.Clamp01(t);
            Apply(_shown, true);
        }

        public string CurrentTitle => _title != null ? _title.text : "";
        public string CurrentBody => _body != null ? _body.text : "";

        void Update()
        {
            float speed = 1f / Mathf.Max(0.01f, slideDuration);
            _shown = Mathf.MoveTowards(_shown, _targetShown ? 1f : 0f, Time.unscaledDeltaTime * speed);
            Apply(_shown, false);

            // 港口施工中：剩几回合变了就刷一次面板，施工完按钮自动从灰变亮
            if (_targetShown && _current is FishingPortBuilding port)
            {
                int left = port.UnderConstruction ? port.BuildTurnsLeft : 0;
                if (left != _portTurnsShown)
                {
                    _portTurnsShown = left;
                    Show(port);
                }
            }
        }

        int _portTurnsShown;

        void Apply(float t, bool instant)
        {
            float eased = 1f - Mathf.Pow(1f - t, 3f);          // ease-out cubic
            float x = Mathf.Lerp(width, 0f, eased);
            _panel.anchoredPosition = new Vector2(x, 0f);
            if (_group != null) _group.alpha = Mathf.Clamp01(t * 1.4f);
        }

        // ---------- 文案 ----------

        static string BuildTitle(PlacedTile tile)
        {
            if (tile is SeaSpotBuilding spot)
                return $"海产聚集点（{spot.gridX},{spot.gridY}）";

            string name = string.IsNullOrEmpty(tile.tileId) ? tile.GetType().Name : tile.tileId;
            return $"{name}（{tile.gridX},{tile.gridY}）";
        }

        static string BuildBody(PlacedTile tile)
        {
            switch (tile)
            {
                case SeaSpotBuilding spot:
                    return SeaSpotBody(spot);
                case FactoryBuilding factory:
                    return FactoryBody(factory);
                case FishingPortBuilding port:
                    return FishingPortBody(port);
                case WarehouseBuilding warehouse:
                    return WarehouseBody(warehouse);
                case SortingFacility sorter:
                    return SortingBody(sorter);
                case ReceiverTower receiver:
                    return ReceiverBody(receiver);
                case WreckSiteTile wreck:
                    return $"{wreck.KindName}\n" +
                           (wreck.isTreasure
                               ? "内含：<color=#F0D48A>金币</color>\n"
                               : (!string.IsNullOrEmpty(wreck.LootName)
                                   ? $"内含：<color=#F0D48A>{wreck.LootName}</color>\n"
                                   : "")) +
                           "<color=#8A98A6>海底一次性物资，捞过就没了。</color>";
                case PrismTower _:
                    return "棱镜塔\n电流束穿过它时，会从塔身向左右两侧各射出一条新的电流束。\n" +
                           "<color=#8A98A6>把它建在光束路径上，就能把电铺到更远的地方。</color>";
                case RoadTile _:
                    return "路面\n<color=#8A98A6>只负责把相邻的建筑连起来，物资瞬间传递。</color>";
                default:
                {
                    string bonus = LandmarkBonus.Describe(tile.tileId);
                    return string.IsNullOrEmpty(bonus)
                        ? ""
                        : bonus + "\n<color=#8A98A6>（作用范围：以这一格为中心的 3×3）</color>";
                }
            }
        }

        static string ReceiverBody(ReceiverTower r)
        {
            var sb = new StringBuilder();
            sb.Append(r.PoweredByBeam
                ? "<color=#8FE08A>正在接收电流束</color>"
                : "<color=#FF8A8A>没有电流束打到这里</color>").Append('\n');

            int size = Mathf.Max(1, r.powerRadius) * 2 + 1;
            sb.Append($"供电范围：{size}×{size} 地块（以塔为中心）").Append('\n');

            if (r.PoweredByBeam)
            {
                sb.Append($"电流束从「{DirName(r.beamFrom)}」射来，塔已朝向那一侧").Append('\n');
                sb.Append($"<color=#8A98A6>电流束到这一格就被截断，后面的地块不会通电。</color>");
            }
            else
            {
                sb.Append("<color=#8A98A6>把它建在电流束经过的格子上（按 P 看电力图）。</color>");
            }
            return sb.ToString();
        }

        /// <summary>方向 → 中文叫法（和等距四方位一致）。</summary>
        static string DirName(Vector2Int dir)
        {
            if (dir.x > 0) return "右前";
            if (dir.y > 0) return "左前";
            if (dir.x < 0) return "左后";
            if (dir.y < 0) return "右后";
            return "未知";
        }

        static string SortingBody(SortingFacility sorter)
        {
            var sb = new StringBuilder();
            sb.Append($"每回合吃 <color=#F0D48A>{sorter.junkPerTurn:0.#}KG</color> 杂鱼，产出 <color=#8FE08A>{sorter.outputPerTurn:0.#}KG</color> 随机鱼").Append('\n');
            sb.Append("<color=#8A98A6>等级越低的鱼越常见，偶尔会挑出名贵品种。</color>").Append('\n');
            sb.Append($"品质：{Mathf.RoundToInt(sorter.quality * 100f)}%　不需要通电").Append('\n');
            sb.Append(sorter.running
                ? "状态：<color=#8FE08A>作业中</color>"
                : "状态：<color=#FFC46B>已停工（不挑拣、不扣日运营费）</color>").Append('\n');
            if (!string.IsNullOrEmpty(sorter.LastSpecies))
                sb.Append($"最近产出：{sorter.LastSpecies} {sorter.LastOutput:0.#}KG");
            return sb.ToString();
        }

        static string SeaSpotBody(SeaSpotBuilding spot)
        {
            return
                $"品种：{spot.species}\n" +
                $"品质：{Mathf.RoundToInt(spot.quality * 100f)}%\n" +
                $"数量：{spot.amountKg:N0}KG\n" +
                $"离散时间：{Mathf.CeilToInt(spot.RemainingHours / GameClock.HoursPerTurn)}回合后";
        }

        static string FishingPortBody(FishingPortBuilding p)
        {
            p.RefreshWaterState();

            var sb = new StringBuilder();
            sb.Append(p.HasPower
                ? "已通电"
                : "<color=#FFC46B>没有接在电网上（只影响冷链品质，不影响捕捞）</color>").Append('\n');
            sb.Append(p.running ? "状态：<color=#8FE08A>作业中</color>" : "状态：<color=#FFC46B>已停工（不捕捞、不扣日费）</color>").Append('\n');
            sb.Append(p.dailyCost > 0f
                ? $"运营费+员工工资：{p.dailyCost:N0}/日"
                : "运营费+员工工资：？/日").Append('\n');

            if (p.UnderConstruction)
            {
                sb.Append($"<color=#FFC46B>施工中：还剩 {p.BuildTurnsLeft} 回合</color>").Append('\n');
                sb.Append("<color=#8A98A6>施工完成之后才能指定捕捞点</color>");
                return sb.ToString();
            }

            if (!p.HasTarget)
            {
                sb.Append("<color=#8A98A6>还没有指定捕捞点（点下面的按钮再点一片水面）</color>");
                return sb.ToString();
            }

            sb.Append($"捕捞点：（{p.targetCell.x},{p.targetCell.y}）　距离 {p.Distance} 格").Append('\n');

            SeaSpotBuilding spot = p.TargetSpot;
            if (spot != null)
                sb.Append($"目标：海产聚集点 · {spot.species}（剩 {spot.amountKg:N0}KG）").Append('\n');
            else if (p.TargetIsWater)
                sb.Append("目标：普通水体（只出杂鱼）").Append('\n');
            else
                sb.Append("<color=#FF8A8A>目标不是水体，无法作业</color>").Append('\n');

            if (!p.ConnectedToTarget)
                sb.Append("<color=#FF8A8A>和捕捞点不在同一片水域，船开不过去 → 捞不到</color>").Append('\n');
            else if (p.TargetIsInland)
                sb.Append($"<color=#FFC46B>捕捞点是内陆湖，速率 ×{p.inlandRateFactor:0.##}</color>").Append('\n');

            ItemDef def = ItemDatabase.Get(p.CatchSpecies);
            sb.Append($"产出：{def.DisplayName} {p.CatchRate:0.###}KG/回合").Append('\n');
            sb.Append($"品质：{Mathf.RoundToInt(p.CatchQuality * 100f)}%");
            if (p.ColdChainActive)
                sb.Append("　<color=#7FD8FF>冷链运输已开启（不掉品质）</color>");
            else if (p.Distance > 0)
                sb.Append($"<color=#8A98A6>（距离 -{p.Distance}%）</color>");

            if (AbilityData.QualityLevel > 0)
                sb.Append($"\n<color=#9CD7A6>动物交流 · 优质加成：+{AbilityData.CatchQualityBonus * 100f:0}% 品质</color>");

            return sb.ToString();
        }

        static string FactoryBody(FactoryBuilding f)
        {
            Recipe recipe = f.CurrentRecipe;
            var sb = new StringBuilder();
            sb.Append(f.powered ? "已通电" : "<color=#FF8A8A>未通电</color>").Append('\n');

            // 电力：一级烟熏房 / 一级预处理厂不用电，其余厂没电就停机
            if (!f.NeedsPower)
                sb.Append("用电：<color=#8FE08A>不需要电</color>（一级" +
                          (f.factoryType == FactoryType.Smoking ? "烟熏房烧柴" : "预处理靠手工") + "）").Append('\n');
            else if (f.HasPower)
                sb.Append("用电：<color=#8FE08A>已供电</color>").Append('\n');
            else
                sb.Append("用电：<color=#FF8A8A>没有电，已停机</color>（建在电流束上，或造接收塔）").Append('\n');

            sb.Append(f.dailyCost > 0f
                ? $"运营费+员工工资：{f.dailyCost:N0}/日"
                : "运营费+员工工资：？/日").Append('\n');
            sb.Append($"端口：{(recipe.DynamicPrimary ? 1 : recipe.PrimaryItems.Length) + recipe.AuxItems.Length} 进 · " +
                      $"{1 + recipe.ByproductCount} 出").Append('\n');

            // 批次型（灌装 / 发酵 / 萃取）：攒够一整批料 → 闷头做 N 回合 → 一次性发放
            sb.Append(recipe.IsBatch
                ? $"节奏：<color=#E6C55A>批次型 · 每 {recipe.BatchTurns} 回合发一批</color>（要攒够一整批料才开工）"
                : "节奏：连续生产（每回合都出）").Append('\n');

            sb.Append(f.HasSelectedPrimary
                ? $"选料：<color=#7FD1E8>{ItemDatabase.Get(f.selectedPrimary).DisplayName}</color>（只加工这一种，没货就等料）"
                : "选料：自动（库存最多的可加工原料）").Append('\n');

            sb.Append("<color=#5AA9E6>主料</color>：");
            if (recipe.DynamicPrimary)
            {
                if (recipe.RequiresStages == null || recipe.RequiresStages.Length == 0)
                    sb.Append("任意生鱼");
                else
                {
                    sb.Append("任意");
                    for (int k = 0; k < recipe.RequiresStages.Length; k++)
                    {
                        if (k > 0) sb.Append('/');
                        sb.Append(ItemDatabase.StageWord(recipe.RequiresStages[k]));
                    }
                    sb.Append("过的鱼");
                }
                sb.Append($" {recipe.DynamicPrimaryPerUnit * f.ratePerTurn:0.###}KG/回合");
            }
            else
            {
                AppendItems(sb, recipe.PrimaryItems, recipe.PrimaryPerUnit, f.ratePerTurn);
            }
            sb.Append('\n');

            if (recipe.AuxItems.Length > 0)
            {
                sb.Append("<color=#E6C55A>辅料</color>：");
                AppendItems(sb, recipe.AuxItems, recipe.AuxPerUnit, f.ratePerTurn);
                sb.Append('\n');
            }

            if (recipe.DynamicPrimary)
            {
                sb.Append($"<color=#6FD08C>成品</color>：{recipe.DisplayName}" +
                          $"（{ItemDatabase.StageWord(recipe.AppliesStage.Value)}·名称随鱼种自动生成） " +
                          $"{recipe.OutputPerUnit * f.ratePerTurn:0.###}KG/回合").Append('\n');
            }
            else
            {
                ItemDef product = ItemDatabase.Get(recipe.ProductId);
                sb.Append($"<color=#6FD08C>成品</color>：{product.DisplayName} " +
                          $"{recipe.OutputPerUnit * f.ratePerTurn:0.###}{product.UnitText}/回合").Append('\n');
            }

            for (int b = 0; b < recipe.ByproductIds.Length; b++)
            {
                ItemDef by = ItemDatabase.Get(recipe.ByproductIds[b]);
                sb.Append($"<color=#E89A5A>副产</color>：{by.DisplayName} " +
                          $"{recipe.ByproductPerUnit[b] * f.ratePerTurn:0.###}{by.UnitText}/回合").Append('\n');
            }

            if (f.IsBatchRunning)
                sb.Append($"<color=#7FD8E8>状态：批次生产中，还剩 {f.BatchTurnsLeft} / {f.BatchTotalTurns} 回合</color>" +
                          $"（本批 {f.BatchCopies:0.##} 份，做完一次性入仓）");
            else if (f.LastTickStalled)
                sb.Append(recipe.IsBatch
                    ? "<color=#FFC46B>状态：等待原料（批次型要攒够一整批才开工）</color>"
                    : "<color=#FFC46B>状态：等待原料</color>");
            else
                sb.Append($"<color=#8A98A6>累计产出：{f.TotalProduced:0.#}</color>");

            return sb.ToString();
        }

        static void AppendItems(StringBuilder sb, string[] items, float[] perUnit, float rate)
        {
            for (int k = 0; k < items.Length; k++)
            {
                ItemDef def = ItemDatabase.Get(items[k]);
                if (k > 0) sb.Append("，");
                sb.Append($"{def.DisplayName} {perUnit[k] * rate:0.###}{def.UnitText}/回合");
            }
        }

        static string WarehouseBody(WarehouseBuilding w)
        {
            var sb = new StringBuilder("存储：\n");
            int n = 0;
            foreach (var stock in w.storage.All)
            {
                ItemDef def = ItemDatabase.Get(stock.ItemId);
                sb.Append($"· {def.DisplayName} {ItemDatabase.Describe(stock.ItemId, stock.Amount)}");
                if (def.HasQuality) sb.Append($"　品质 {Mathf.RoundToInt(stock.Quality * 100f)}%");
                sb.Append('\n');
                n++;
            }
            if (n == 0) sb.Append("<color=#8A98A6>（空）</color>");
            return sb.ToString();
        }

        /// <summary>每秒刷新一次当前面板的数值（产量、库存会变）。</summary>
        float _refreshTimer;

        void LateUpdate()
        {
            if (_current == null) return;
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < 0.25f) return;
            _refreshTimer = 0f;
            // 定时刷新要带上等级那一行，否则点开面板 0.25 秒后等级就"消失"了
            _body.text = BuildBody(_current) + LevelLine(_current);
        }
    }
}
