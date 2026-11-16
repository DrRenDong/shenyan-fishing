using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 点地块 → 打开右侧详情面板。用 2D 碰撞体点选（烘焙好的轮廓），
    /// 叠在一起时取 sortingOrder 最高的那个（即离镜头最近的）。
    /// 点到空白处收起面板；点在面板上不触发。
    /// 另外负责「指定捕捞点」的选择模式与顶部提示条。
    /// </summary>
    public class TilePicker : MonoBehaviour
    {
        public Camera targetCamera;
        public TileInfoPanel panel;
        public CargoDockPanel dockPanel;
        public FactoryInputPanel inputPanel;
        public MapCameraController cameraController;

        FishingPortBuilding _pendingPort;

        /// <summary>本次按下是不是从 UI 上开始的（手机抬手后查不到触摸，只能按下时记一次）。</summary>
        bool _pressOverUI;

        CanvasGroup _hintGroup;
        Text _hintLabel;
        LineRenderer _cellHi;
        LineRenderer _boatLine;
        IsoTileMap _map;

        const string TargetHintText = "点击水体或海产聚集点以指定捕捞点（Esc 取消）";
        const string BoatHintText = "点击水面设置航线（Esc 取消）";

        /// <summary>进入「指定捕捞点」模式：下一次点水面就设为该港口的捕捞点。港口没建好之前不能指定。</summary>
        public void BeginPickTarget(FishingPortBuilding port)
        {
            if (port == null) return;
            if (port.UnderConstruction)
            {
                Debug.Log($"[捕捞港] 还在施工（剩 {port.BuildTurnsLeft} 回合），建好之后才能指定捕捞点");
                return;
            }

            _pendingPort = port;
            BuildCellHighlight();
            ShowHint(_pendingPort != null, TargetHintText);
        }

        public bool IsPickingTarget => _pendingPort != null;

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (panel == null) panel = FindObjectOfType<TileInfoPanel>();
            if (dockPanel == null) dockPanel = FindObjectOfType<CargoDockPanel>();
            if (inputPanel == null) inputPanel = FindObjectOfType<FactoryInputPanel>();
            if (cameraController == null) cameraController = FindObjectOfType<MapCameraController>();
            BuildHint();
            BuildCellHighlight();
        }

        void Update()
        {
            UpdateCellHighlight();
            UpdateBoatLine();

            // 按下时先记一次「是不是从 UI 上按下来的」：手机抬手后 touchCount 已经归零，
            // 抬起那一帧再查 IsPointerOverGameObject 会漏判（点航线按钮穿透到下面的格子就是这样）。
            if (Input.GetMouseButtonDown(0)) _pressOverUI = UiPointer.OverAnyUI();

            // 松开左键时判定：拖动过就不算点选
            if (Input.GetMouseButtonUp(0))
            {
                bool overUI = _pressOverUI || UiPointer.OverAnyUI();
                _pressOverUI = false;

                // 工程模式：左键拖动是平移，拖动结束不算点选
                if (EngineeringView.Active && EngineeringView.Dragging) return;
                if (cameraController != null && cameraController.MovedSincePress > cameraController.dragThreshold)
                    return;
                if (overUI) return;

                Vector2 screen = Input.mousePosition;

                // 建造模式由 BuildController 自己处理点击与拖动，这里不参与
                if (BuildController.Instance != null && BuildController.Instance.Active) return;

                // 点面板不穿透到地图：EventSystem 在 ScreenSpaceCamera 画布上偶尔漏判，这里再兜一层
                if (panel != null && panel.IsShown && panel.ContainsScreenPoint(screen)) return;
                if (dockPanel != null && dockPanel.IsShown && dockPanel.ContainsScreenPoint(screen)) return;
                if (inputPanel != null && inputPanel.IsShown && inputPanel.ContainsScreenPoint(screen)) return;

                Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));

                // 建造模式优先：点格子就直接下单
                if (BuildController.Instance != null && BuildController.Instance.Active)
                {
                    BuildController.Instance.TryPlace(EngineeringView.WorldToCell(world));
                    return;
                }

                if (BoatPathMode)
                {
                    if (TrySetBoatPath(world)) SetBoatPathMode(false);
                    return;
                }

                if (_pendingPort != null)
                {
                    PickTargetForPort(world);
                    return;
                }

                PlacedTile picked;
                if (EngineeringView.Active)
                {
                    // 工程模式：俯视是方形网格，直接用格子坐标找地块（碰撞体还是等距位置）
                    Vector2Int gridCell = EngineeringView.WorldToCell(world);
                    picked = FactoryWorld.Instance != null ? FactoryWorld.Instance.OccupantAt(gridCell) : null;
                }
                else
                {
                    picked = Pick(new Vector2(world.x, world.y));
                }

                // 点船 = 给船划航线
                if (picked is PlayerBoatVehicle)
                {
                    if (panel != null) panel.Hide();
                    if (dockPanel != null) dockPanel.Hide();
                    SetBoatPathMode(true);
                    return;
                }

                if (picked is CargoDockBuilding dock)
                {
                    if (panel != null) panel.Hide();
                    if (dockPanel != null) dockPanel.Show(dock);
                }
                else
                {
                    if (dockPanel != null) dockPanel.Hide();
                    if (picked != null && panel != null) panel.Show(picked);
                    else if (panel != null) panel.Hide();
                }
            }

            if (Input.GetKeyDown(KeyCode.V))
            {
                SetBoatPathMode(!BoatPathMode);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _pendingPort = null;
                BoatPathMode = false;
                ShowHint(false);
                if (panel != null) panel.Hide();
                if (dockPanel != null) dockPanel.Hide();
            }
        }

        void PickTargetForPort(Vector3 world)
        {
            var map = FindObjectOfType<IsoTileMap>();
            Vector2Int cell = EngineeringView.WorldToCell(world);
            if (map == null) return;

            // 港口还没建好（或者已经没了）：这次点选直接作废
            var port = _pendingPort;
            if (port == null || port.UnderConstruction)
            {
                _pendingPort = null;
                ShowHint(false);
                BuildCellHighlight();
                return;
            }

            PlacedTile occ = FactoryWorld.Instance != null ? FactoryWorld.Instance.OccupantAt(cell) : null;
            bool knownSpot = occ is SeaSpotBuilding spot && spot.revealed;
            bool water = map.IsWaterAt(cell.x, cell.y);
            // 地图外的开放海域没有地形数据（TileAt 为空），它照样是能下网的海面；
            // 只认 IsWaterAt 会让「岛缘浅滩外那一圈空白海」永远指定不了。
            bool openSea = string.IsNullOrEmpty(map.TileAt(cell.x, cell.y));

            // 空白海面、有地形的水面、已发现的海产聚集点，三种都能当捕捞点
            if (!water && !openSea && !knownSpot) return;

            // 空白黑区不能指定；视野外的已知聚集点可以（它是地图上的标记）
            var streamer = FindObjectOfType<SeaStreamer>();
            if (!knownSpot && streamer != null && !streamer.IsInVision(cell)) return;

            port.SetTarget(cell);
            _pendingPort = null;
            ShowHint(false);
            if (dockPanel != null) dockPanel.Hide();
            if (panel != null) panel.Show(port);
        }

        /// <summary>公开：当前是否在等玩家点水面（给别的系统查询用）。</summary>
        public bool WaitingForTarget => _pendingPort != null;

        // ---------------- 划船航线 ----------------

        /// <summary>是否处于「给船划路线」模式（按 V 切换）。</summary>
        public bool BoatPathMode { get; private set; }

        public void SetBoatPathMode(bool on)
        {
            BoatPathMode = on;
            if (on)
            {
                _pendingPort = null;
                BuildCellHighlight();
                ShowHint(true, BoatHintText);
            }
            else
            {
                ShowHint(false);
            }
        }

        bool TrySetBoatPath(Vector3 world)
        {
            var boat = FindObjectOfType<PlayerBoatVehicle>();
            var map = FindObjectOfType<IsoTileMap>();
            if (boat == null || map == null) return false;

            Vector2Int cell = EngineeringView.WorldToCell(world);

            // 地图外（还没铺海面、一片漆黑）也能划航线：只要不是陆地就放行
            bool openSea = string.IsNullOrEmpty(map.TileAt(cell.x, cell.y));
            if (!openSea && !map.IsWaterAt(cell.x, cell.y)) return false;

            var path = map.FindWaterPath(boat.Cell, cell, true);
            if (path == null) { return false; }

            if (path.Count > 0 && path[0] == boat.Cell) path.RemoveAt(0);
            boat.SetPath(path);
            return true;
        }

        // ---------------- 船的航线（红线） ----------------

        /// <summary>创建（或找回）船航线的红线。</summary>
        void EnsureBoatLine()
        {
            if (_boatLine == null)
            {
                Transform old = transform.Find("BoatPath");
                if (old != null) _boatLine = old.GetComponent<LineRenderer>();

                if (_boatLine == null)
                {
                    var go = new GameObject("BoatPath");
                    go.transform.SetParent(transform, false);
                    _boatLine = go.AddComponent<LineRenderer>();
                }
            }

            LineRenderer line = _boatLine;
            line.useWorldSpace = true;
            line.loop = false;
            line.widthMultiplier = 0.05f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = TileInfoPanel.RouteColor;
            line.sharedMaterial = mat;
            line.startColor = TileInfoPanel.RouteColor;
            line.endColor = TileInfoPanel.RouteColor;
            line.sortingOrder = 421;
            line.enabled = false;
        }

        /// <summary>
        /// 航线模式里画船当前的航线；不在模式里但船还有没走完的航线也继续画，
        /// 这样设置完之后仍能看见上次的路线。
        /// </summary>
        void UpdateBoatLine()
        {
            var boat = FindObjectOfType<PlayerBoatVehicle>();
            bool show = boat != null && (BoatPathMode || boat.HasPath);

            if (!show)
            {
                if (_boatLine != null) _boatLine.enabled = false;
                return;
            }

            EnsureBoatLine();

            var points = new List<Vector3>();
            points.Add(EngineeringView.CellToWorld(boat.gridX, boat.gridY) + new Vector3(0f, 0.72f, 0f));
            if (boat.movePath != null)
                for (int k = 0; k < boat.movePath.Count; k++)
                    points.Add(EngineeringView.CellToWorld(boat.movePath[k].x, boat.movePath[k].y) + new Vector3(0f, 0.72f, 0f));

            int maxDiag = boat.gridX + boat.gridY;
            if (boat.movePath != null)
                for (int k = 0; k < boat.movePath.Count; k++)
                    maxDiag = Mathf.Max(maxDiag, boat.movePath[k].x + boat.movePath[k].y);

            // 整条线按最远的斜列定排序号，否则远处的水体方块会把它盖住
            _boatLine.sortingOrder = maxDiag * 4 + 21;

            _boatLine.positionCount = points.Count;
            for (int k = 0; k < points.Count; k++) _boatLine.SetPosition(k, points[k]);
            _boatLine.enabled = points.Count >= 2;
        }

        // ---------------- 选点高亮 ----------------

        /// <summary>非空时强制高亮这一格（截图/验证用）。</summary>
        public Vector2Int? PreviewCell;

        public void BuildCellHighlight()
        {
            if (_cellHi != null) return;

            // 热重载后字段会丢，先找场景里已有的，别叠出第二份
            Transform existing = transform.Find("CellHighlight");
            if (existing != null)
            {
                _cellHi = existing.GetComponent<LineRenderer>();
                if (_cellHi != null)
                {
                    _cellHi.enabled = false;
                    return;
                }
            }

            var go = new GameObject("CellHighlight");
            go.transform.SetParent(transform, false);
            _cellHi = go.AddComponent<LineRenderer>();
            _cellHi.useWorldSpace = true;
            _cellHi.loop = true;
            _cellHi.positionCount = 4;
            _cellHi.widthMultiplier = 0.022f;
            _cellHi.numCornerVertices = 0;
            _cellHi.numCapVertices = 0;
            _cellHi.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cellHi.receiveShadows = false;

            var mat = new Material(Shader.Find("Sprites/Default"));
            var color = new Color(0.30f, 0.85f, 0.95f, 1f);
            mat.color = color;
            _cellHi.sharedMaterial = mat;
            _cellHi.startColor = color;
            _cellHi.endColor = color;
            _cellHi.sortingOrder = 400;       // 画在最上面
            _cellHi.enabled = false;
        }

        /// <summary>选点模式下，鼠标指到的水体格子描一圈青边。</summary>
        void UpdateCellHighlight()
        {
            if (_cellHi == null) return;

            bool active = _pendingPort != null || BoatPathMode || PreviewCell.HasValue;
            if (!active || targetCamera == null)
            {
                _cellHi.enabled = false;
                return;
            }

            if (!PreviewCell.HasValue)
            {
                if (UiPointer.OverAnyUI())
                {
                    _cellHi.enabled = false;
                    return;
                }
                if (panel != null && panel.IsShown && panel.ContainsScreenPoint(Input.mousePosition))
                {
                    _cellHi.enabled = false;
                    return;
                }
                if (dockPanel != null && dockPanel.IsShown && dockPanel.ContainsScreenPoint(Input.mousePosition))
                {
                    _cellHi.enabled = false;
                    return;
                }
            }

            if (_map == null) _map = FindObjectOfType<IsoTileMap>();

            Vector2Int cell;
            if (PreviewCell.HasValue) cell = PreviewCell.Value;
            else
            {
                Vector3 world = targetCamera.ScreenToWorldPoint(
                    new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f));
                cell = EngineeringView.WorldToCell(world);
            }

            bool water = _map != null && _map.IsWaterAt(cell.x, cell.y);
            // 划航线 / 指定捕捞点时，地图外那片没有地形的空白海面也能点（它同样是海）
            bool openSea = _map != null && string.IsNullOrEmpty(_map.TileAt(cell.x, cell.y));
            if (_map == null || (!water && !openSea))
            {
                _cellHi.enabled = false;
                return;
            }

            // 一格顶面菱形（和 WorldToCell 用的地面高度一致）
            // 地图外还没铺海面的格子也画（那里一片漆黑，更要提示"可以点"）
            Vector3 corner = EngineeringView.CellToWorld(cell.x, cell.y);
            Vector3 c = corner + new Vector3(0f, 0.66f, 0f);
            _cellHi.SetPosition(0, c + new Vector3(0f, 0.25f, 0f));
            _cellHi.SetPosition(1, c + new Vector3(0.5f, 0f, 0f));
            _cellHi.SetPosition(2, c + new Vector3(0f, -0.25f, 0f));
            _cellHi.SetPosition(3, c + new Vector3(-0.5f, 0f, 0f));
            _cellHi.enabled = true;
        }

        void ShowHint(bool on, string text = null)
        {
            if (_hintGroup == null) BuildHint();
            if (_hintGroup == null) return;
            if (on && text != null && _hintLabel != null) _hintLabel.text = text;
            _hintGroup.gameObject.SetActive(on);
        }

        /// <summary>顶部中间的提示条：告诉玩家现在处于「选捕捞点」状态。</summary>
        void BuildHint()
        {
            if (_hintGroup != null) return;

            // 热重载后字段会丢，先找场景里已有的提示条
            Transform existing = transform.Find("PickHintCanvas");
            if (existing != null)
            {
                _hintGroup = existing.GetComponent<CanvasGroup>();
                Transform label = existing.Find("Bg/Text");
                _hintLabel = label != null ? label.GetComponent<Text>() : null;
                // 复用旧提示条时也要保证它盖在船等精灵上面
                var oldCanvas = existing.GetComponent<Canvas>();
                if (oldCanvas != null) oldCanvas.sortingOrder = 5040;
                if (_hintGroup != null) return;
            }

            var canvasGo = new GameObject("PickHintCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5040;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _hintGroup = canvasGo.AddComponent<CanvasGroup>();
            _hintGroup.blocksRaycasts = false;
            _hintGroup.interactable = false;

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            var bg = bgGo.GetComponent<RectTransform>();
            bg.SetParent(canvasGo.transform, false);
            bg.anchorMin = new Vector2(0.5f, 1f);
            bg.anchorMax = new Vector2(0.5f, 1f);
            bg.pivot = new Vector2(0.5f, 1f);
            bg.sizeDelta = new Vector2(760f, 54f);
            bg.anchoredPosition = new Vector2(0f, -36f);
            bgGo.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 1f);

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(bg, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accentGo.GetComponent<Image>().color = new Color(0.36f, 0.78f, 0.86f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rect = textGo.GetComponent<RectTransform>();
            rect.SetParent(bg, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(20f, 0f);
            rect.offsetMax = new Vector2(-20f, 0f);
            var text = textGo.GetComponent<Text>();
            _hintLabel = text;
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
            text.fontSize = 22;
            text.color = new Color(0.95f, 0.97f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = "点击水体或海产聚集点以指定捕捞点（Esc 取消）";

            canvasGo.SetActive(false);
        }

        PlacedTile Pick(Vector2 worldPoint)
        {
            return PickAt(worldPoint);
        }

        /// <summary>世界坐标 → 命中的地块（取 sortingOrder 最高、也就是离镜头最近的那个）。</summary>
        public static PlacedTile PickAt(Vector2 worldPoint)
        {
            int count = Physics2D.OverlapPointNonAlloc(worldPoint, Buffer);
            PlacedTile best = null;
            int bestOrder = int.MinValue;
            for (int k = 0; k < count; k++)
            {
                var tile = Buffer[k].GetComponentInParent<PlacedTile>();
                if (tile == null) continue;

                int order = tile.Renderer != null ? tile.Renderer.sortingOrder : 0;
                if (order > bestOrder)
                {
                    bestOrder = order;
                    best = tile;
                }
            }
            return best;
        }

        static readonly Collider2D[] Buffer = new Collider2D[16];
    }
}
