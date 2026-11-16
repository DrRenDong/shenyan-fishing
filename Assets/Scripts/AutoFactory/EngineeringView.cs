using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 工程模式：把伪 3D 等距地图临时换成「上帝视角」俯视图（G 键或 HUD 按钮）。
    /// 俯视图用 Assets/Resources/Image/俯视瓦片 里的素材，按格子 (i, -j) 排布；
    /// 退出时恢复原相机与等距渲染。
    /// </summary>
    public class EngineeringView : MonoBehaviour
    {
        public static EngineeringView Instance { get; private set; }

        [Header("设置")]
        public Camera targetCamera;
        public KeyCode toggleKey = KeyCode.G;
        public string spriteFolder = "Image/俯视瓦片";
        [Tooltip("俯视图的 sortingOrder 起点")]
        public int layerBase = 2000;

        bool _on;
        GameObject _root;
        Vector3 _savedPos;
        float _savedSize;
        bool _savedOrtho;
        bool _savedCamCtl;

        readonly List<SpriteRenderer> _hidden = new List<SpriteRenderer>();

        public bool IsOn => _on;

        public static bool Active => Instance != null && Instance.IsOn;

        /// <summary>工程模式下是否正在左键拖动（拖动时不要触发点选）。</summary>
        public static bool Dragging => Instance != null && Instance._dragging;

        /// <summary>格子 → 世界坐标（工程模式用俯视方形网格，退出后回到等距）。</summary>
        public static Vector3 CellToWorld(int i, int j)
        {
            return Active ? new Vector3(i, -j, 0f) : PlacedTile.CellToWorld(i, j);
        }

        /// <summary>世界坐标 → 格子（工程模式用俯视方形网格）。</summary>
        public static Vector2Int WorldToCell(Vector3 world, float surfaceOffset = 0.66f)
        {
            if (!Active) return PlacedTile.WorldToCell(world, surfaceOffset);
            return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(-world.y));
        }

        [Header("俯视相机")]
        public float dragThreshold = 8f;
        Vector2 _dragStartScreen;
        Vector3 _dragCamStart;
        bool _dragging;

        public float minSize = 4f;
        public float maxSize = 26f;
        float _size;

        void HandleCamera()
        {
            if (targetCamera == null) return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float baseSize = _size > 0f ? _size : targetCamera.orthographicSize;
                _size = Mathf.Clamp(baseSize - scroll * 1.2f, minSize, maxSize);
                targetCamera.orthographicSize = _size;
            }

            // 左键拖动（移动端也能用）：拖动超过阈值才平移；建造模式下左键留给放置
            bool building = BuildController.Instance != null && BuildController.Instance.Active;

            if (!building)
            {
                Vector2 mouse = Input.mousePosition;

                // 点在 UI（右下角按钮、面板）上不算开始拖动，免得点「工程」时顺手把地图拽走
                bool overUI = UiPointer.OverAnyUI();

                if (Input.GetMouseButtonDown(0) && !overUI)
                {
                    _dragStartScreen = mouse;
                    _dragCamStart = targetCamera.transform.position;
                    _dragging = false;
                }

                if (Input.GetMouseButton(0) && !overUI)
                {
                    if (!_dragging && ((Vector2)Input.mousePosition - _dragStartScreen).magnitude > dragThreshold)
                        _dragging = true;

                    if (_dragging)
                    {
                        Vector3 worldStart = targetCamera.ScreenToWorldPoint(new Vector3(_dragStartScreen.x, _dragStartScreen.y, 0f));
                        Vector3 worldNow = targetCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f));
                        Vector3 p = _dragCamStart + (worldStart - worldNow);
                        p.z = targetCamera.transform.position.z;
                        targetCamera.transform.position = p;
                    }
                }

                // 没按着左键（或者指针跑到 UI 上）就立刻结束拖动：绝不能让镜头「跟手」
                if (!Input.GetMouseButton(0) || overUI) _dragging = false;
            }
            else
            {
                _dragging = false;
            }

            // 注意：这里不再用 Mouse X/Y 轴做右键平移。
            // 轴值是「每帧的鼠标位移」，只要按键状态被判成按住就会变成镜头跟手，
            // 出现过「点了工程按钮之后不按着也会跟着鼠标拖」的问题。左键拖动已经够用了。

            var map = FindObjectOfType<IsoTileMap>();
            if (map != null)
            {
                Vector3 p = targetCamera.transform.position;
                p.x = Mathf.Clamp(p.x, -6f, map.Columns + 6f);
                p.y = Mathf.Clamp(p.y, -map.Rows - 6f, 6f);
                targetCamera.transform.position = p;
            }
        }

        void Awake()
        {
            Instance = this;
            if (targetCamera == null) targetCamera = Camera.main;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
            if (_on) HandleCamera();
        }

        public void Toggle()
        {
            if (_on) Exit();
            else Enter();
        }

        public void Enter()
        {
            if (_on) return;
            _on = true;
            _dragging = false;      // 进入时先清掉拖动状态，别继承外面的按键状态

            GameAudio.Ensure().PlayBlueprintSfx();   // 展开图纸
            BuildTopView();
            HideIsometric(true);

            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            _savedPos = targetCamera.transform.position;
            _savedSize = targetCamera.orthographicSize;
            _savedOrtho = targetCamera.orthographic;

            var map = FindObjectOfType<IsoTileMap>();
            int cols = map != null ? map.Columns : 28;
            int rows = map != null ? map.Rows : 28;

            targetCamera.orthographic = true;
            _size = Mathf.Max(cols, rows) * 0.5f + 2f;
            targetCamera.orthographicSize = _size;
            targetCamera.transform.position = new Vector3((cols - 1) * 0.5f, -(rows - 1) * 0.5f, -10f);

            var camCtl = FindObjectOfType<MapCameraController>();
            if (camCtl != null)
            {
                _savedCamCtl = camCtl.enabled;
                camCtl.enabled = false;
            }
        }

        /// <summary>
        /// 工程模式里建造 / 拆除之后重铺俯视图：
        /// 否则地面换成泥土、新建的房子都不会出现在俯视图里，
        /// 而新建建筑自己的等距精灵又会留在场景中显示成「角落里一个斜的房子」。
        /// </summary>
        public void Refresh()
        {
            if (!_on) return;
            BuildTopView();
            HideIsometric(true);
        }

        public void Exit()
        {
            if (!_on) return;
            _on = false;
            _dragging = false;

            DestroyRoot();
            HideIsometric(false);

            if (targetCamera != null)
            {
                targetCamera.transform.position = _savedPos;
                targetCamera.orthographicSize = _savedSize;
                targetCamera.orthographic = _savedOrtho;
            }

            var camCtl = FindObjectOfType<MapCameraController>();
            if (camCtl != null) camCtl.enabled = _savedCamCtl;
        }

        void DestroyRoot()
        {
            if (_root == null)
            {
                _root = null;
                return;
            }
            if (Application.isPlaying) Destroy(_root);
            else DestroyImmediate(_root);
            _root = null;
        }

        // ---------------- 俯视图 ----------------

        void BuildTopView()
        {
            DestroyRoot();
            _root = new GameObject("EngineeringTopView");

            var map = FindObjectOfType<IsoTileMap>();
            if (map == null) return;

            var cache = new Dictionary<string, Sprite>();

            Sprite Get(string tile)
            {
                if (string.IsNullOrEmpty(tile)) return null;
                if (cache.TryGetValue(tile, out Sprite s)) return s;

                s = Resources.Load<Sprite>(spriteFolder + "/" + tile);
                // 俯视素材里这张叫「沙地」，地图数据里是「沙滩」
                if (s == null && tile == "沙滩") s = Resources.Load<Sprite>(spriteFolder + "/沙地");
                if (s == null && tile == "沙地") s = Resources.Load<Sprite>(spriteFolder + "/沙滩");
                cache[tile] = s;
                return s;
            }

            // 手工装饰（洞口顶盖这类）：俯视图里也要画，否则被封顶的格子会露成地面
            var extras = map.Extras;
            if (extras != null)
            {
                for (int k = 0; k < extras.Count; k++)
                {
                    var e = extras[k];
                    if (e == null || string.IsNullOrEmpty(e.tile)) continue;
                    Sprite sp = Get(e.tile);
                    if (sp == null) continue;
                    MakeTile(e.i, e.j, sp, layerBase + e.j * 10 + 2);
                }
            }

            // 地形
            for (int j = map.MinJ; j <= map.MaxJ; j++)
            {
                for (int i = map.MinI; i <= map.MaxI; i++)
                {
                    string tile = map.TileAt(i, j);
                    if (string.IsNullOrEmpty(tile)) continue;

                    // 俯视图只用俯视素材；找不到就留空，别把等距斜图塞进来
                    Sprite sprite = Get(tile);
                    if (sprite == null) continue;

                    MakeTile(i, j, sprite, layerBase + j * 10);
                }
            }

            // 建筑 / 地块物件：直接扫场景里的 PlacedTile（不依赖 FactoryWorld 是否已 Bind，编辑期也能看）
            var allPlaced = FindObjectsOfType<PlacedTile>();
            int placedCount = 0;
            foreach (var placed in allPlaced)
            {
                if (placed == null || string.IsNullOrEmpty(placed.tileId)) continue;
                if (placed is PlayerBoatVehicle) continue;

                // 接收塔四个朝向共用一张俯视图
                string topId = placed is ReceiverTower ? "接收塔" : placed.tileId;
                Sprite sprite = Get(topId);
                if (sprite == null) sprite = Get(placed.tileId);
                if (sprite == null) continue;

                var sr = MakeTile(placed.gridX, placed.gridY, sprite, layerBase + placed.gridY * 10 + 1);

                // 施工中的建筑半透明（和等距视图一致），一眼能看出「还没建好」
                if (sr != null && placed.UnderConstruction)
                {
                    Color c = sr.color;
                    c.a = 0.45f;
                    sr.color = c;
                }

                placedCount++;
            }
            Debug.Log($"[工程模式] 地形 i {map.MinI}..{map.MaxI} / j {map.MinJ}..{map.MaxJ}，场景地块 {allPlaced.Length}，画出建筑 {placedCount}");
        }

        SpriteRenderer MakeTile(int i, int j, Sprite sprite, int order)
        {
            var go = new GameObject($"top_{i}_{j}");
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(i, -j, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // ---------------- 隐藏等距渲染 ----------------

        void HideIsometric(bool hide)
        {
            if (hide)
            {
                _hidden.Clear();
                Collect(FindObjectOfType<IsoTileMap>());
                foreach (var t in FindObjectsOfType<PlacedTile>()) Collect(t);
                foreach (var s in FindObjectsOfType<SeaStreamer>()) Collect(s);
                // 工程模式是看图纸，路面上的搬运小人也不用显示
                foreach (var w in FindObjectsOfType<RoadWorker>()) Collect(w);
            }
            else
            {
                for (int k = 0; k < _hidden.Count; k++)
                    if (_hidden[k] != null) _hidden[k].enabled = true;
                _hidden.Clear();

                // 兜底：把等距层全部打开，避免因为遗漏记录导致原画面整片消失
                var map = FindObjectOfType<IsoTileMap>();
                if (map != null)
                    foreach (var sr in map.GetComponentsInChildren<SpriteRenderer>(true))
                        sr.enabled = true;

                foreach (var t in FindObjectsOfType<PlacedTile>())
                {
                    var sr = t.GetComponentInChildren<SpriteRenderer>(true);
                    if (sr != null) sr.enabled = true;
                }

                foreach (var s in FindObjectsOfType<SeaStreamer>())
                    foreach (var sr in s.GetComponentsInChildren<SpriteRenderer>(true))
                        sr.enabled = true;

                foreach (var w in FindObjectsOfType<RoadWorker>())
                    foreach (var sr in w.GetComponentsInChildren<SpriteRenderer>(true))
                        sr.enabled = true;
            }
        }

        void Collect(Component root)
        {
            if (root == null) return;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sr.enabled) continue;
                sr.enabled = false;
                _hidden.Add(sr);
            }
        }
    }
}
