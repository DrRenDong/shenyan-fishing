using UnityEngine;
using UnityEngine.EventSystems;

namespace Shenyan
{
    /// <summary>
    /// 地图相机：左键拖动平移、滚轮朝鼠标位置缩放，并限制在地图范围内。
    /// 拖动超过阈值后会把 MovedSincePress 标记住，点选逻辑据此判断「这是拖动不是点击」。
    /// </summary>
    public class MapCameraController : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("范围")]
        public Vector2 boundsMin = new Vector2(-540f, -290f);
        public Vector2 boundsMax = new Vector2(540f, 290f);
        [Tooltip("启动时按「地图 + 所有沉船/宝藏点位」重新计算可拖动范围（地图是无限的，这样能一直拖到探索过的海域）")]
        public bool autoFitBounds = true;
        public float boundsPadding = 30f;

        [Header("缩放")]
        public float minSize = 1.6f;
        public float maxSize = 45f;
        public float zoomStep = 0.12f;
        public float dragThreshold = 6f;

        [Header("手机")]
        [Tooltip("双指捏合缩放灵敏度")]
        public float pinchSensitivity = 1f;

        bool _pressed;
        float _movedSincePress;
        float _boundsTimer;
        Vector3 _lastMouse;
        float _prevPinchDist = -1f;

        /// <summary>本次按下之后累计移动了多少屏幕像素（用来区分点击和拖动）。</summary>
        public float MovedSincePress => _movedSincePress;
        public bool IsDragging => _pressed && _movedSincePress > dragThreshold;

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (autoFitBounds) RecomputeBounds();
            FitSize();
        }

        void Start()
        {
            // 沉船/宝藏的点位通常比地图晚生成，再算一次
            if (autoFitBounds) RecomputeBounds();
        }

        /// <summary>把可拖动范围扩到「地图 + 所有海底点位」的外接矩形（含边距）。</summary>
        public void RecomputeBounds()
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            bool any = false;

            var map = FindObjectOfType<IsoTileMap>();
            if (map != null)
            {
                Vector3 a = map.CellToWorld(map.MinI, map.MinJ);
                Vector3 b = map.CellToWorld(map.MaxI, map.MaxJ);
                Vector3 c = map.CellToWorld(map.MinI, map.MaxJ);
                Vector3 d = map.CellToWorld(map.MaxI, map.MinJ);
                foreach (var p in new[] { a, b, c, d })
                {
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                    any = true;
                }
            }

            foreach (var s in WreckSiteSystem.Sites)
            {
                Vector3 p = PlacedTile.CellToWorld(s.i, s.j);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                any = true;
            }

            // 海产聚集点也是玩家要点的目标，范围得罩住它们（它们可能刷到很远的海域）
            foreach (var spot in FindObjectsOfType<SeaSpotBuilding>())
            {
                Vector3 p = spot.transform.position;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                any = true;
            }

            if (!any) return;

            // 和现有范围取并集：只允许变大，不允许把主人手设的范围缩回去
            // （沉船/宝藏点位是运行时才生成的，Awake 时可能还看不到）
            Vector2 lo = new Vector2(minX - boundsPadding, minY - boundsPadding);
            Vector2 hi = new Vector2(maxX + boundsPadding, maxY + boundsPadding);
            boundsMin = Vector2.Min(boundsMin, lo);
            boundsMax = Vector2.Max(boundsMax, hi);
        }

        void FitSize()
        {
            if (targetCamera == null) return;
            targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize, minSize, maxSize);
            ClampPosition();
        }

        void Update()
        {
            if (targetCamera == null) return;

            // H：把画面中心拉回渔船
            if (Input.GetKeyDown(KeyCode.H)) FocusBoat();

            TickFocus();

            // 聚集点会随视线不断刷出来（可能跑到很远），范围隔一会儿跟着长一次，
            // 否则镜头拖不过去、远处的聚集点就指定不了（主人反馈 0,-463 那种）。
            _boundsTimer += Time.unscaledDeltaTime;
            if (autoFitBounds && _boundsTimer >= 2f)
            {
                _boundsTimer = 0f;
                RecomputeBounds();
            }

            // 手机：双指捏合缩放（滚轮在手机上收不到）
            bool pinching = HandlePinch();

            if (!pinching)
            {
                if (Input.GetMouseButtonDown(0) && !UiPointer.OverAnyUI())
                {
                    _pressed = true;
                    _movedSincePress = 0f;
                    _lastMouse = Input.mousePosition;
                }

                if (_pressed && Input.GetMouseButton(0))
                {
                    Vector3 now = Input.mousePosition;
                    Vector2 delta = (Vector2)now - (Vector2)_lastMouse;
                    _movedSincePress += delta.magnitude;

                    if (_movedSincePress > dragThreshold)
                    {
                        Vector3 worldDelta = ScreenToWorld(now) - ScreenToWorld(_lastMouse);
                        targetCamera.transform.position -= worldDelta;
                        _focusTarget = null;      // 玩家开始拖动就取消定位缓动
                        ClampPosition();
                    }
                    _lastMouse = now;
                }

                if (Input.GetMouseButtonUp(0)) _pressed = false;

                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f && !UiPointer.OverAnyUI())
                    ZoomAt(Input.mousePosition, -scroll * zoomStep);
            }
        }

        /// <summary>
        /// 双指捏合缩放（手机端替代滚轮）。返回 true 表示这一帧在处理双指手势，
        /// 单指拖动逻辑要让位（否则两指按下去会被当成拖动地图）。
        /// </summary>
        bool HandlePinch()
        {
            if (Input.touchCount < 2)
            {
                _prevPinchDist = -1f;
                return false;
            }

            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);
            float dist = Vector2.Distance(a.position, b.position);

            if (_prevPinchDist > 1f && dist > 1f)
            {
                // orthographicSize 越小画面越大：两指张开（dist 变大）要缩小 size 才是「放大」
                float factor = -(dist / _prevPinchDist - 1f) * pinchSensitivity;
                if (Mathf.Abs(factor) > 0.0005f)
                    ZoomAt((a.position + b.position) * 0.5f, factor);
            }

            _prevPinchDist = dist;
            _pressed = false;
            return true;
        }

        /// <summary>把画面中心移到渔船所在位置（带缓动）。</summary>
        public void FocusBoat()
        {
            var boat = FindObjectOfType<PlayerBoatVehicle>();
            if (boat == null) return;
            FocusOn(boat.transform.position);
        }

        /// <summary>把画面中心缓动到某个世界坐标。</summary>
        public void FocusOn(Vector3 worldPos)
        {
            _focusTarget = new Vector3(worldPos.x, worldPos.y, -10f);
        }

        Vector3? _focusTarget;
        [Tooltip("定位缓动速度，越大越快")]
        public float focusLerpSpeed = 7f;

        void TickFocus()
        {
            if (!_focusTarget.HasValue || targetCamera == null) return;

            Vector3 cur = targetCamera.transform.position;
            float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, focusLerpSpeed) * Time.deltaTime);
            Vector3 next = Vector3.Lerp(cur, _focusTarget.Value, t);

            if ((next - _focusTarget.Value).sqrMagnitude < 0.0025f)
            {
                next = _focusTarget.Value;
                _focusTarget = null;
            }

            targetCamera.transform.position = next;
            ClampPosition();
        }

        /// <summary>朝鼠标方向缩放（factor &gt; 0 放大）。</summary>
        public void ZoomAt(Vector3 screenPoint, float factor)
        {
            if (targetCamera == null) return;
            float before = targetCamera.orthographicSize;
            float after = Mathf.Clamp(before * (1f + factor), minSize, maxSize);
            if (Mathf.Approximately(before, after)) return;

            Vector3 worldBefore = ScreenToWorld(screenPoint);
            targetCamera.orthographicSize = after;
            Vector3 worldAfter = ScreenToWorld(screenPoint);
            targetCamera.transform.position += worldBefore - worldAfter;
            _focusTarget = null;              // 缩放会改变取景，取消定位缓动
            ClampPosition();
        }

        void ClampPosition()
        {
            Vector3 p = targetCamera.transform.position;
            p.x = Mathf.Clamp(p.x, boundsMin.x, boundsMax.x);
            p.y = Mathf.Clamp(p.y, boundsMin.y, boundsMax.y);
            p.z = -10f;
            targetCamera.transform.position = p;
        }

        Vector3 ScreenToWorld(Vector3 screenPoint)
        {
            return targetCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, 0f));
        }

    }
}
