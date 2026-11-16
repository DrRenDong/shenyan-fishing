using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 沉船探测：在「设置航线」状态下，屏幕边缘会出现一个箭头，指向最近的、还没打捞的沉船。
    /// 暂时默认开启；以后做成「大地图 / 神眼升级解锁」时，把 requiresAbility 打开即可。
    /// </summary>
    public class WreckCompass : MonoBehaviour
    {
        public static WreckCompass Instance { get; private set; }

        [Header("开关")]
        [Tooltip("探测功能总开关")]
        public bool detectEnabled = true;
        [Tooltip("以后改成解锁制：为 true 时要神眼等级够才有")]
        public bool requiresAbility = false;
        public int requiredEyeLevel = 2;

        [Header("外观")]
        public Color arrowColor = new Color(1f, 0.85f, 0.35f, 0.95f);
        [Tooltip("箭头离屏幕边缘留多少像素")]
        public float edgeMargin = 70f;
        public float arrowSize = 54f;

        Canvas _canvas;
        RectTransform _arrow;
        Image _arrowImg;
        Sprite _arrowSprite;
        TilePicker _picker;
        PlayerBoatVehicle _boat;
        Camera _cam;

        public bool IsActive => detectEnabled && (!requiresAbility || AbilityData.EyeLevel >= requiredEyeLevel);

        void Awake()
        {
            Instance = this;
            BuildUI();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("WreckCompassCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = Camera.main;
            _canvas.planeDistance = 1f;
            _canvas.sortingOrder = 5044;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var go = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            _arrow = go.GetComponent<RectTransform>();
            _arrow.SetParent(canvasGo.transform, false);
            _arrow.anchorMin = new Vector2(0.5f, 0.5f);
            _arrow.anchorMax = new Vector2(0.5f, 0.5f);
            _arrow.pivot = new Vector2(0.5f, 0.5f);
            _arrow.sizeDelta = new Vector2(arrowSize, arrowSize);

            _arrowImg = go.GetComponent<Image>();
            _arrowSprite = MakeArrowSprite(64);
            _arrowImg.sprite = _arrowSprite;
            _arrowImg.color = arrowColor;
            _arrowImg.raycastTarget = false;

            go.SetActive(false);
        }

        void Update()
        {
            if (_arrow == null) return;

            if (_cam == null) _cam = Camera.main;
            if (_cam == null || !IsActive)
            {
                _arrow.gameObject.SetActive(false);
                return;
            }

            if (_picker == null) _picker = FindObjectOfType<TilePicker>();
            if (_picker == null || !_picker.BoatPathMode)
            {
                _arrow.gameObject.SetActive(false);
                return;
            }

            WreckSiteSystem.Site target = FindNearest();
            if (target == null)
            {
                _arrow.gameObject.SetActive(false);
                return;
            }

            Vector3 world = PlacedTile.CellToWorld(target.i, target.j) + new Vector3(0f, 0.4f, 0f);
            Vector3 sp = _cam.WorldToScreenPoint(world);
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 v = (Vector2)sp - center;
            if (v.sqrMagnitude < 1f) v = Vector2.up;

            // 从屏幕中心朝目标方向，找与「内缩 margin 的边框」的交点
            float halfW = Mathf.Max(1f, Screen.width * 0.5f - edgeMargin);
            float halfH = Mathf.Max(1f, Screen.height * 0.5f - edgeMargin);
            float tx = Mathf.Abs(v.x) > 0.001f ? halfW / Mathf.Abs(v.x) : float.MaxValue;
            float ty = Mathf.Abs(v.y) > 0.001f ? halfH / Mathf.Abs(v.y) : float.MaxValue;
            float t = Mathf.Min(1f, Mathf.Min(tx, ty));
            Vector2 pos = center + v * t;

            float scale = _canvas.scaleFactor > 0.001f ? _canvas.scaleFactor : 1f;
            _arrow.anchoredPosition = (pos - center) / scale;

            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f;   // 贴图箭头朝上
            _arrow.localRotation = Quaternion.Euler(0f, 0f, angle);

            _arrowImg.color = arrowColor;
            _arrow.gameObject.SetActive(true);
        }

        WreckSiteSystem.Site FindNearest()
        {
            if (_boat == null) _boat = FindObjectOfType<PlayerBoatVehicle>();
            Vector2 from = _boat != null
                ? (Vector2)_boat.transform.position
                : (Vector2)_cam.transform.position;

            WreckSiteSystem.Site best = null;
            float bestDist = float.MaxValue;

            var sites = WreckSiteSystem.Sites;
            for (int k = 0; k < sites.Count; k++)
            {
                var s = sites[k];
                if (s == null || s.kind != 0 || s.salvaged) continue;   // 只看没打捞的沉船

                Vector3 p = PlacedTile.CellToWorld(s.i, s.j);
                float d = (p.x - from.x) * (p.x - from.x) + (p.y - from.y) * (p.y - from.y);
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        /// <summary>程序生成一个朝上的箭头贴图。</summary>
        static Sprite MakeArrowSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var px = new Color32[size * size];
            int cx = size / 2;
            for (int y = 0; y < size; y++)
            {
                float ny = (float)y / (size - 1);
                for (int x = 0; x < size; x++)
                {
                    bool on;
                    if (ny > 0.42f)
                    {
                        float half = (1f - ny) * size * 0.52f;      // 三角逐渐收窄到顶点
                        on = Mathf.Abs(x - cx) <= half;
                    }
                    else
                    {
                        on = Mathf.Abs(x - cx) <= size * 0.15f;      // 尾部短柄
                    }
                    px[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
