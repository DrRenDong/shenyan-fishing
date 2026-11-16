using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 「移动草地」：先点一块没有附属物的草地，再点一片水面/地图外的空地，把草地搬过去（原地变浅水）。
    /// 默认随时可用（主人先用来测试），以后可以改成需要地形改造 Lv3。
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class GrassMoveController : MonoBehaviour
    {
        public static GrassMoveController Instance { get; private set; }

        [Tooltip("关掉后需要地形改造 Lv3 才能用（HUD 上的移地按钮也只在解锁后出现）")]
        public bool alwaysAvailable = false;

        public bool Moving { get; private set; }

        bool _hasSource;
        Vector2Int _source;

        IsoTileMap _map;
        TilePicker _picker;
        BuildController _builder;
        LineRenderer _outline;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Toggle() => SetMode(!Moving);

        public void SetMode(bool on)
        {
            if (Moving == on) return;

            if (on && !alwaysAvailable && !AbilityData.CanMoveGrass)
            {
                Hint("需要地形改造 Lv3 才能移动草地。");
                return;
            }

            Moving = on;
            _hasSource = false;

            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_picker == null) _picker = FindObjectOfType<TilePicker>();
            if (_builder == null) _builder = FindObjectOfType<BuildController>();

            if (on)
            {
                if (_builder != null) _builder.Cancel();      // 退出建造/拆除模式，免得抢点击
                if (_picker != null) _picker.enabled = false;
                Hint("移动草地：先点一块草地（上面不能有东西）");
            }
            else
            {
                if (_picker != null) _picker.enabled = true;
                if (_outline != null) _outline.enabled = false;
                HideHint();
            }
        }

        void Update()
        {
            if (!Moving) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetMode(false);
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                _pressScreen = Input.mousePosition;
                _pressed = true;
                _pressOverUI = UiPointer.OverAnyUI();   // 手机抬手后查不到触摸，按下时先记一次
                return;
            }

            if (!Input.GetMouseButtonUp(0) || !_pressed) return;
            _pressed = false;

            // 拖动地图（工程模式的左键拖动）不算点选
            if (((Vector2)Input.mousePosition - _pressScreen).magnitude > 8f) return;

            if (_pressOverUI) return;

            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            Camera cam = Camera.main;
            if (_map == null || cam == null) return;

            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int cell = EngineeringView.Active
                ? EngineeringView.WorldToCell(world, 0f)
                : PlacedTile.WorldToCell(world, 0.66f);

            if (!_hasSource) PickSource(cell);
            else MoveTo(cell);
        }

        Vector2 _pressScreen;
        bool _pressed;
        bool _pressOverUI;

        void PickSource(Vector2Int cell)
        {
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_map == null) return;

            string ground = _map.TileAt(cell.x, cell.y);
            if (ground != "草地")
            {
                Hint("只能选草地（这一格是「" + (string.IsNullOrEmpty(ground) ? "空" : ground) + "」）");
                return;
            }

            PlacedTile occ = FactoryWorld.Instance != null ? FactoryWorld.Instance.OccupantAt(cell) : null;
            if (occ != null)
            {
                Hint($"「{occ.tileId}」在上面，不能选这一格");
                return;
            }

            if (_map.HeightAt(cell.x, cell.y) > 0.001f)
            {
                Hint("抬高的地块不能移动");
                return;
            }

            _source = cell;
            _hasSource = true;
            DrawOutline(cell);
            Hint($"已选草地（{cell.x},{cell.y}）→ 现在点一片水面 / 空地");
        }

        void MoveTo(Vector2Int cell)
        {
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_map == null) return;

            if (cell == _source)
            {
                Hint("目标不能是同一格，换一格水面");
                return;
            }

            string ground = _map.TileAt(cell.x, cell.y);
            bool free = string.IsNullOrEmpty(ground);
            bool water = !free && _map.IsWaterAt(cell.x, cell.y);
            if (!free && !water)
            {
                Hint("目标必须是水面或地图外的空地");
                return;
            }

            // 必须挨着别的陆地：不能把草地凭空扔到远海
            bool touchesLand = false;
            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };
            foreach (var d in dirs)
            {
                Vector2Int n = cell + d;
                string g = _map.TileAt(n.x, n.y);
                if (!string.IsNullOrEmpty(g) && !_map.IsWaterAt(n.x, n.y))
                {
                    touchesLand = true;
                    break;
                }
            }
            if (!touchesLand)
            {
                Hint("要挨着其他土地才能放（不能扔到远海）");
                return;
            }

            if (FactoryWorld.Instance != null && FactoryWorld.Instance.OccupantAt(cell) != null)
            {
                Hint("目标格上有东西，换个地方");
                return;
            }

            var from = _source;
            _map.SetTileAt(from.x, from.y, "浅水");      // 原地变成水
            _map.SetTileAt(cell.x, cell.y, "草地");      // 草地搬过去

            Debug.Log($"[移地] 草地（{from.x},{from.y}）→（{cell.x},{cell.y}）");

            _hasSource = false;
            if (_outline != null) _outline.enabled = false;

            var streamer = FindObjectOfType<SeaStreamer>();
            if (streamer != null) streamer.Refresh();

            // 工程模式：重铺俯视图，并把新生成的地形精灵重新隐藏（否则会露出正交视角的方块）
            if (EngineeringView.Active && EngineeringView.Instance != null)
                EngineeringView.Instance.Refresh();

            Hint("移动完成，可以继续选下一块草地（Esc 退出）");
        }

        void DrawOutline(Vector2Int c)
        {
            if (_outline == null)
            {
                var go = new GameObject("GrassMoveOutline");
                go.transform.SetParent(transform, false);
                _outline = go.AddComponent<LineRenderer>();
                _outline.useWorldSpace = true;
                _outline.positionCount = 5;
                _outline.startWidth = _outline.endWidth = 0.03f;
                _outline.numCapVertices = 0;
                _outline.material = new Material(Shader.Find("Sprites/Default"));
                _outline.startColor = _outline.endColor = new Color(0.45f, 0.95f, 0.55f, 0.95f);
                _outline.sortingLayerName = "Default";
                _outline.sortingOrder = 9000;
            }
            _outline.enabled = true;

            Vector3 center = EngineeringView.CellToWorld(c.x, c.y);
            Vector3[] pts;
            if (EngineeringView.Active)
            {
                pts = new[]
                {
                    center + new Vector3(-0.5f, 0.5f, 0f),
                    center + new Vector3(0.5f, 0.5f, 0f),
                    center + new Vector3(0.5f, -0.5f, 0f),
                    center + new Vector3(-0.5f, -0.5f, 0f),
                    center + new Vector3(-0.5f, 0.5f, 0f),
                };
            }
            else
            {
                Vector3 top = center + new Vector3(0f, 0.66f, 0f);
                pts = new[]
                {
                    top + new Vector3(-0.5f, 0f, 0f),
                    top + new Vector3(0f, 0.25f, 0f),
                    top + new Vector3(0.5f, 0f, 0f),
                    top + new Vector3(0f, -0.25f, 0f),
                    top + new Vector3(-0.5f, 0f, 0f),
                };
            }
            _outline.SetPositions(pts);
        }

        static void Hint(string message)
        {
            Debug.Log("[移地] " + message);
            var panel = TileInfoPanel.Instance;
            if (panel != null) panel.Show("移动草地", message);
        }

        static void HideHint()
        {
            var panel = TileInfoPanel.Instance;
            if (panel != null) panel.Hide();
        }
    }
}
