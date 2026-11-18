using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 远景海雾背景。
    ///
    /// 地图是无限延伸的，画面外围本来只有相机的纯色底，显得空、也显得平。
    /// 这里在相机后面铺三份横向镜像交替的海雾贴图（镜像是为了掩盖左右接缝），
    /// 排序号压到最低，任何地块和海面都盖在它上面；再让它缓慢横向漂移，
    /// 远海就不会是一块死色。
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class SeaFogBackdrop : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("素材")]
        public Sprite fogSprite;
        [Tooltip("资源路径（fogSprite 为空时用）")]
        public string resourcePath = "Image/背景/海雾";

        [Header("排布")]
        [Tooltip("相邻两份的缩放：1 正好铺满屏幕高，略大一点留出漂移余量")]
        public float coverPadding = 1.2f;
        [Tooltip("横向铺几份（至少 2；奇数最好）")]
        public int copies = 3;

        [Header("漂移")]
        [Tooltip("横向漂移幅度（占屏宽比例）")]
        [Range(0f, 0.4f)] public float driftAmount = 0.08f;
        [Tooltip("漂移速度")]
        public float driftSpeed = 0.04f;

        [Header("颜色")]
        [Tooltip("整体压一层淡蓝，和主色调统一")]
        public Color tint = new Color(0.72f, 0.82f, 0.95f, 1f);
        [Tooltip("排序号要低于所有地块（地块最高约 (i+j)*4，±500 时到 4000）")]
        public int sortingOrder = -32000;

        SpriteRenderer[] _copies;
        float _baseWidth;

        void Start()
        {
            EnsureBuilt();
        }

        void OnDisable()
        {
            if (_copies == null) return;
            foreach (var c in _copies)
                if (c != null) c.enabled = false;
        }

        void EnsureBuilt()
        {
            if (_copies != null && _copies.Length == copies) return;

            if (fogSprite == null)
                fogSprite = Resources.Load<Sprite>(resourcePath);
            if (fogSprite == null) return;

            _baseWidth = fogSprite.bounds.size.x;

            // 清掉旧的
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var ch = transform.GetChild(i);
                if (!ch.name.StartsWith("fog_copy")) continue;
                if (Application.isPlaying) Destroy(ch.gameObject);
                else DestroyImmediate(ch.gameObject);
            }

            _copies = new SpriteRenderer[Mathf.Max(2, copies)];
            for (int i = 0; i < _copies.Length; i++)
            {
                var go = new GameObject("fog_copy" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = fogSprite;
                sr.color = tint;
                sr.sortingOrder = sortingOrder;
                // 镜像交替：相邻两份互为镜像，接缝两侧像素天然对齐
                sr.flipX = (i % 2) == 1;
                _copies[i] = sr;
            }
        }

        void LateUpdate()
        {
            EnsureBuilt();
            if (_copies == null || _copies.Length == 0 || fogSprite == null) return;

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            float viewH = cam.orthographic ? cam.orthographicSize * 2f : 10f;
            float viewW = viewH * Mathf.Max(0.1f, cam.aspect);

            float scale = Mathf.Max(viewH / fogSprite.bounds.size.y,
                                    viewW / fogSprite.bounds.size.x) * Mathf.Max(1f, coverPadding);

            float step = _baseWidth * scale;
            float drift = Mathf.Sin(Time.time * Mathf.Max(0.0001f, driftSpeed)) * viewW * driftAmount;

            Vector3 camPos = cam.transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, 0f);

            int mid = _copies.Length / 2;
            for (int i = 0; i < _copies.Length; i++)
            {
                var sr = _copies[i];
                if (sr == null) continue;
                sr.enabled = true;
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                sr.transform.localPosition = new Vector3((i - mid) * step + drift, 0f, 0f);
                sr.sortingOrder = sortingOrder;
                sr.color = tint;
            }
        }
    }
}
