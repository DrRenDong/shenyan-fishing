using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 「安放地块」模式：土地局批下的一组 2×3 地块要贴到现有陆地边上（填海造陆）。
    /// 鼠标处显示半透明预览，绿=可放、红=不可放；点一下落地。
    /// 规则：6 格必须都在水面上（不能压已有地块），且至少有一格四邻接已有陆地。
    /// </summary>
    public class LandPlacementController : MonoBehaviour
    {
        public static LandPlacementController Instance { get; private set; }

        [Header("外观")]
        public Camera targetCamera;
        public float ghostAlpha = 0.55f;
        public int ghostSortingOrder = 700;

        LandGrant _grant;
        Action _onDone;
        IsoTileMap _map;
        Vector2Int _anchor;
        bool _active;
        bool _valid;
        bool _pressOverUI;

        readonly List<SpriteRenderer> _ghosts = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _propGhosts = new List<SpriteRenderer>();
        CanvasGroup _hintGroup;
        CanvasGroup _rotateGroup;

        public bool IsActive => _active;

        void Awake()
        {
            Instance = this;
            if (targetCamera == null) targetCamera = Camera.main;
            BuildHint();
            BuildRotateButton();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        Action _onCancel;

        /// <summary>进入放置模式；落地成功调用 onDone，玩家取消时调用 onCancel。</summary>
        public void Begin(LandGrant grant, Action onDone, Action onCancel = null)
        {
            if (grant == null) return;
            _grant = grant;
            _onDone = onDone;
            _onCancel = onCancel;
            _active = true;
            _map = FindObjectOfType<IsoTileMap>();
            BuildGhosts();
            UpdateGhost();
            if (_hintGroup != null) _hintGroup.gameObject.SetActive(true);
            if (_rotateGroup != null) _rotateGroup.gameObject.SetActive(true);
        }

        public void Cancel() => Stop(true);

        /// <summary>停止放置模式；notifyCancel=true 时把「取消」回调给上层（让土地局把等待状态复位）。</summary>
        void Stop(bool notifyCancel)
        {
            bool wasActive = _active;
            _active = false;
            ClearGhosts();
            _grant = null;

            Action cancel = _onCancel;
            _onDone = null;
            _onCancel = null;

            if (_hintGroup != null) _hintGroup.gameObject.SetActive(false);
            if (_rotateGroup != null) _rotateGroup.gameObject.SetActive(false);

            if (notifyCancel && wasActive) cancel?.Invoke();
        }

        void Update()
        {
            if (!_active) return;

            UpdateGhost();

            // R / 屏幕上的「旋转」按钮：整组地块转 90°
            if (Input.GetKeyDown(KeyCode.R)) Rotate();

            // 批地流程不能取消：Esc 不再退出放置模式

            if (Input.GetMouseButtonDown(0))
                _pressOverUI = UiPointer.OverAnyUI();   // 手机抬手后查不到触摸，按下时先记一次

            if (!Input.GetMouseButtonUp(0)) return;
            if (_pressOverUI || UiPointer.OverAnyUI()) return;
            if (BuildController.Instance != null && BuildController.Instance.Active) return;
            if (!_valid) return;

            Place();
        }

        /// <summary>把地块组顺时针转 90°（2×3 ↔ 3×2）。</summary>
        public void Rotate()
        {
            if (!_active || _grant == null) return;
            _grant = _grant.Rotate90();
            BuildGhosts();
            UpdateGhost();
        }

        // ---------------- 幽灵预览 ----------------

        void BuildGhosts()
        {
            ClearGhosts();
            for (int row = 0; row < _grant.rows; row++)
            {
                for (int col = 0; col < _grant.cols; col++)
                {
                    var go = new GameObject($"Ghost_{col}_{row}");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = TileCatalog.LoadSprite("草地");
                    sr.sortingOrder = ghostSortingOrder;
                    sr.color = new Color(1f, 1f, 1f, ghostAlpha);
                    _ghosts.Add(sr);

                    // 民居 / 树林这类摆在地面上的物件，单独再挂一层预览
                    var propGo = new GameObject("Prop");
                    propGo.transform.SetParent(go.transform, false);
                    var psr = propGo.AddComponent<SpriteRenderer>();
                    psr.sortingOrder = ghostSortingOrder + 1;
                    psr.color = new Color(1f, 1f, 1f, ghostAlpha);
                    psr.enabled = false;
                    _propGhosts.Add(psr);
                }
            }
        }

        void ClearGhosts()
        {
            foreach (var sr in _ghosts)
                if (sr != null) Destroy(sr.gameObject);
            _ghosts.Clear();
            _propGhosts.Clear();
        }

        void UpdateGhost()
        {
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (_map == null || targetCamera == null || _ghosts.Count == 0) return;

            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f));
            _anchor = EngineeringView.WorldToCell(world);
            _valid = CanPlace(_anchor);

            int k = 0;
            for (int row = 0; row < _grant.rows; row++)
            {
                for (int col = 0; col < _grant.cols; col++, k++)
                {
                    SpriteRenderer sr = _ghosts[k];
                    if (sr == null) continue;

                    string tile = _grant.At(col, row);
                    SpriteRenderer propSr = k < _propGhosts.Count ? _propGhosts[k] : null;
                    if (string.IsNullOrEmpty(tile))
                    {
                        sr.enabled = false;
                        if (propSr != null) propSr.enabled = false;
                        continue;
                    }

                    sr.enabled = true;
                    var cell = new Vector2Int(_anchor.x + col, _anchor.y + row);
                    sr.transform.position = EngineeringView.CellToWorld(cell.x, cell.y);
                    sr.sortingOrder = ghostSortingOrder + (cell.x + cell.y);

                    bool prop = LandGrant.IsProp(tile);
                    string ground = prop ? "草地" : tile;
                    Sprite sprite = TileCatalog.LoadSprite(ground);
                    if (sprite != null) sr.sprite = sprite;

                    var tint = _valid
                        ? new Color(0.75f, 1f, 0.85f, ghostAlpha)
                        : new Color(1f, 0.45f, 0.40f, ghostAlpha);
                    sr.color = tint;

                    if (propSr != null)
                    {
                        Sprite propSprite = prop ? TileCatalog.LoadSprite(tile) : null;
                        if (propSprite != null)
                        {
                            TileDef def = TileCatalog.Find(tile);
                            float lift = def != null ? def.LiftPixels : 0f;

                            propSr.sprite = propSprite;
                            propSr.transform.localPosition = new Vector3(0f, lift / 304f, 0f);
                            propSr.sortingOrder = sr.sortingOrder + 1;
                            propSr.color = tint;
                            propSr.enabled = true;
                        }
                        else
                        {
                            propSr.enabled = false;
                        }
                    }
                }
            }
        }

        // ---------------- 规则 ----------------

        bool CanPlace(Vector2Int anchor)
        {
            if (_map == null || _grant == null) return false;

            bool touchesLand = false;
            for (int row = 0; row < _grant.rows; row++)
            {
                for (int col = 0; col < _grant.cols; col++)
                {
                    // 缺失格什么都不放，所以可以压在任意地形（甚至已有陆地上），
                    // 这样才能把 L 形地块的缺口对准已有的凹槽拼进去。
                    if (string.IsNullOrEmpty(_grant.At(col, row))) continue;

                    var cell = new Vector2Int(anchor.x + col, anchor.y + row);

                    // 缺地形（地图外）也当作海面：世界是无限延伸的，可以往外填海造陆
                    string ground = _map.TileAt(cell.x, cell.y);
                    if (!string.IsNullOrEmpty(ground) && !_map.IsWaterAt(cell.x, cell.y)) return false;   // 压到陆地/建筑地面
                    if (FactoryWorld.Instance != null && FactoryWorld.Instance.OccupantAt(cell) != null) return false;

                    if (!touchesLand && HasLandNeighbour(cell)) touchesLand = true;
                }
            }
            return touchesLand;   // 必须贴着已有陆地，不能凭空出现在远海
        }

        bool HasLandNeighbour(Vector2Int cell)
        {
            if (_map == null) return false;
            return IsLand(cell + new Vector2Int(1, 0)) || IsLand(cell + new Vector2Int(-1, 0))
                || IsLand(cell + new Vector2Int(0, 1)) || IsLand(cell + new Vector2Int(0, -1));
        }

        bool IsLand(Vector2Int cell)
        {
            string ground = _map.TileAt(cell.x, cell.y);
            return !string.IsNullOrEmpty(ground) && !_map.IsWaterAt(cell.x, cell.y);
        }

        // ---------------- 落地 ----------------

        void Place()
        {
            // 双保险：只有合法位置才真正落地
            if (!CanPlace(_anchor))
            {
                Debug.LogWarning($"[土地局] ({_anchor.x},{_anchor.y}) 不合法：要贴陆地、且 6 格都在水面上");
                return;
            }

            var created = new List<PlacedTile>();
            var cells = new List<Vector2Int>();
            for (int row = 0; row < _grant.rows; row++)
            {
                for (int col = 0; col < _grant.cols; col++)
                {
                    string tile = _grant.At(col, row);
                    if (string.IsNullOrEmpty(tile)) continue;      // 缺失格留海面

                    var cell = new Vector2Int(_anchor.x + col, _anchor.y + row);
                    cells.Add(cell);
                    bool prop = LandGrant.IsProp(tile);
                    _map.SetTileAt(cell.x, cell.y, prop ? "草地" : tile);

                    // 民居/树林画精灵；农田/巨石/碎石只挂点击层；
                    // 草地是空地，不能挂东西（否则这一格会被判成「已经有东西」，建筑就修不上去了）
                    if (prop) created.Add(CreateProp(cell, tile, false));
                    else if (tile != "草地") created.Add(CreateProp(cell, tile, true));
                }
            }

            BuildShoreline(cells);

            var streamer = FindObjectOfType<SeaStreamer>();
            if (streamer != null) streamer.Refresh();

            Action done = _onDone;
            Stop(false);        // 正常落地：不触发「取消」回调
            GameAudio.Ensure().PlayLandSfx();   // 地块组落地音效
            Debug.Log($"[土地局] 新地块落在 ({_anchor.x},{_anchor.y})，新增 {created.Count} 处地物");
            done?.Invoke();
        }

        /// <summary>
        /// 新地块外围一圈的深水改成浅水，形成「草地 → 浅海 → 深海」的自然过渡。
        /// （不用沙滩：地块本身就是草地，直接接浅海。）
        /// </summary>
        void BuildShoreline(List<Vector2Int> cells)
        {
            if (_map == null || cells == null || cells.Count == 0) return;

            var inside = new HashSet<Vector2Int>(cells);
            var ring = new HashSet<Vector2Int>();

            for (int k = 0; k < cells.Count; k++)
            {
                Vector2Int c = cells[k];
                for (int d = 0; d < 4; d++)
                {
                    Vector2Int n = c + Neighbour(d);
                    if (inside.Contains(n)) continue;

                    string ground = _map.TileAt(n.x, n.y);
                    // 地图外（没有地形）也要补成浅海：以前只处理「深水」，
                    // 在主岛外缘填海时四周是空的，结果边缘不会出现浅海。
                    if (!string.IsNullOrEmpty(ground) && ground != "深水") continue;
                    ring.Add(n);
                }
            }

            foreach (var n in ring) _map.SetTileAt(n.x, n.y, "浅水");
            Debug.Log($"[土地局] 地块外围新增浅海 {ring.Count} 格");
        }

        static Vector2Int Neighbour(int index)
        {
            switch (index)
            {
                case 0: return new Vector2Int(1, 0);
                case 1: return new Vector2Int(-1, 0);
                case 2: return new Vector2Int(0, 1);
                default: return new Vector2Int(0, -1);
            }
        }

        PlacedTile CreateProp(Vector2Int cell, string tile, bool colliderOnly = false)
        {
            var go = new GameObject($"{tile}_{cell.x}_{cell.y}");
            go.SetActive(false);
            go.transform.SetParent(FactoryWorld.Instance != null ? FactoryWorld.Instance.transform : transform, false);

            var placed = go.AddComponent<PlacedTile>();
            placed.gridX = cell.x;
            placed.gridY = cell.y;
            placed.tileId = tile;
            placed.colliderOnly = colliderOnly;

            go.SetActive(true);
            placed.EnsureVisual();
            if (FactoryWorld.Instance != null) FactoryWorld.Instance.Register(placed);
            return placed;
        }

        /// <summary>屏幕上的「旋转」按钮（移动端没有 R 键）。</summary>
        void BuildRotateButton()
        {
            Transform existing = transform.Find("LandRotateCanvas");
            if (existing != null)
            {
                _rotateGroup = existing.GetComponent<CanvasGroup>();
                if (_rotateGroup != null) return;
            }

            var canvasGo = new GameObject("LandRotateCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5046;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _rotateGroup = canvasGo.AddComponent<CanvasGroup>();

            var btnGo = new GameObject("RotateBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = btnGo.GetComponent<RectTransform>();
            rect.SetParent(canvasGo.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(180f, 50f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            btnGo.GetComponent<Image>().color = new Color(0.22f, 0.42f, 0.32f, 1f);

            var btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(Rotate);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
            text.fontSize = 22;
            text.color = new Color(0.95f, 0.97f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = "旋转（R）";

            canvasGo.SetActive(false);
        }

        // ---------------- 顶部提示条 ----------------

        void BuildHint()
        {
            Transform existing = transform.Find("LandHintCanvas");
            if (existing != null)
            {
                _hintGroup = existing.GetComponent<CanvasGroup>();
                if (_hintGroup != null) return;
            }

            var canvasGo = new GameObject("LandHintCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5045;
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
            bg.sizeDelta = new Vector2(820f, 54f);
            bg.anchoredPosition = new Vector2(0f, -36f);
            bgGo.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 1f);

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(bg, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accentGo.GetComponent<Image>().color = new Color(0.55f, 0.85f, 0.55f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rect = textGo.GetComponent<RectTransform>();
            rect.SetParent(bg, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(20f, 0f);
            rect.offsetMax = new Vector2(-20f, 0f);
            var text = textGo.GetComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
            text.fontSize = 22;
            text.color = new Color(0.95f, 0.97f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = "把地块贴到陆地边上（绿色可放，红色不可放）　R 旋转　点击落地";

            canvasGo.SetActive(false);
        }
    }
}
