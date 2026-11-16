using UnityEngine;
using UnityEngine.EventSystems;

namespace Shenyan
{
    /// <summary>
    /// 鼠标指向地块时，沿烘焙好的轮廓描一圈白边。
    /// 轮廓直接取地块自己的 PolygonCollider2D（也就是点击层），所以「看到的高亮」＝「点得到的范围」。
    /// </summary>
    public class TileHoverOutline : MonoBehaviour
    {
        public Camera targetCamera;
        public Color color = Color.white;
        public Color haloColor = new Color(0f, 0f, 0f, 0.55f);
        public float lineWidth = 0.014f;
        public float scale = 1.012f;

        LineRenderer _line;
        LineRenderer _halo;
        PlacedTile _current;

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            EnsureLine();
            // 场景里可能存着上次的 enabled=true，进游戏先全部关掉
            HideLines();
        }

        void HideLines()
        {
            if (_line != null) _line.enabled = false;
            if (_halo != null) _halo.enabled = false;
        }

        /// <summary>创建（或找回）白边用的 LineRenderer（外面一圈深色描边 + 里面一圈白线）。</summary>
        void EnsureLine()
        {
            if (_line != null) return;

            _halo = FindOrCreate("HoverOutlineHalo", haloColor, lineWidth * 2.2f);
            _line = FindOrCreate("HoverOutline", color, lineWidth);
        }

        LineRenderer FindOrCreate(string name, Color lineColor, float width)
        {
            Transform existing = transform.Find(name);
            LineRenderer line = existing != null ? existing.GetComponent<LineRenderer>() : null;
            if (line == null)
            {
                var go = existing != null ? existing.gameObject : new GameObject(name);
                go.transform.SetParent(transform, false);
                line = go.AddComponent<LineRenderer>();
            }

            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = width;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = lineColor;
            line.sharedMaterial = mat;
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.enabled = false;
            return line;
        }

        void Update()
        {
            if (targetCamera == null) return;

            // 工程模式（俯视）：直接用格子找地块，画方形描边
            if (EngineeringView.Active)
            {
                if (UiPointer.OverAnyUI())
                {
                    SetTarget(null);
                    return;
                }

                Vector3 w = targetCamera.ScreenToWorldPoint(
                    new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f));
                Vector2Int cell = EngineeringView.WorldToCell(w);
                PlacedTile t = FactoryWorld.Instance != null ? FactoryWorld.Instance.OccupantAt(cell) : null;
                SetTarget(t);
                return;
            }
            Vector2 screen = Input.touchCount > 0 ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            SetTarget(TilePicker.PickAt(new Vector2(world.x, world.y)));
        }

        /// <summary>直接把白边框到某个地块上（截图/验证用）。</summary>
        public void ShowFor(PlacedTile tile)
        {
            SetTarget(tile);
        }

        public PlacedTile Current => _current;

        /// <summary>工程模式：沿格子画一圈方形描边（俯视不需要烘焙轮廓）。</summary>
        void DrawSquare(PlacedTile tile)
        {
            EnsureLine();

            if (tile == null)
            {
                if (_line != null) _line.enabled = false;
                if (_halo != null) _halo.enabled = false;
                return;
            }

            Vector3 c = EngineeringView.CellToWorld(tile.gridX, tile.gridY);
            for (int pass = 0; pass < 2; pass++)
            {
                LineRenderer line = pass == 0 ? _halo : _line;
                if (line == null) continue;

                line.positionCount = 4;
                line.SetPosition(0, c + new Vector3(-0.5f, 0.5f, 0f));
                line.SetPosition(1, c + new Vector3(0.5f, 0.5f, 0f));
                line.SetPosition(2, c + new Vector3(0.5f, -0.5f, 0f));
                line.SetPosition(3, c + new Vector3(-0.5f, -0.5f, 0f));
                line.sortingOrder = 3000 + pass;
                line.enabled = true;
            }
        }

        /// <summary>能不能描边：需要有精灵 + 烘焙好的多边形轮廓。
        /// 农田 / 巨石 / 碎石这类「只有点击层、没有精灵」的地块不能，否则会残留上一个地块的白边。</summary>
        static bool CanOutline(PlacedTile tile)
        {
            if (tile == null || tile.Renderer == null) return false;
            var poly = tile.Renderer.GetComponent<PolygonCollider2D>();
            return poly != null && poly.points.Length >= 3;
        }

        void SetTarget(PlacedTile tile)
        {
            EnsureLine();

            if (EngineeringView.Active)
            {
                DrawSquare(tile);
                _current = tile;
                return;
            }

            if (tile == _current)
            {
                // 同步开关，避免场景里存着的 enabled 让白边一直亮。
                // 注意这里必须重新判一次「能不能描边」——指向农田这类无精灵地块时，
                // 以前会无条件打开，于是残留成上一个房子的轮廓。
                bool on = CanOutline(_current);
                if (_line != null) _line.enabled = on;
                if (_halo != null) _halo.enabled = on;
                return;
            }
            _current = tile;

            if (!CanOutline(tile))
            {
                if (_line != null) _line.enabled = false;
                if (_halo != null) _halo.enabled = false;
                return;
            }

            var poly = tile.Renderer.GetComponent<PolygonCollider2D>();
            Vector2[] pts = poly.points;
            Transform spriteTf = tile.Renderer.transform;
            for (int pass = 0; pass < 2; pass++)
            {
                LineRenderer line = pass == 0 ? _halo : _line;
                line.positionCount = pts.Length;
                for (int k = 0; k < pts.Length; k++)
                {
                    Vector3 local = new Vector3(pts[k].x * scale, pts[k].y * scale, 0f);
                    line.SetPosition(k, spriteTf.TransformPoint(local));
                }
                line.sortingOrder = tile.Renderer.sortingOrder + (pass == 0 ? 1 : 2);
                line.enabled = true;
            }
        }
    }
}
