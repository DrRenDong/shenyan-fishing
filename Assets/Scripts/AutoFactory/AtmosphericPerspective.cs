using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 大气透视：离镜头中心越远的地形/海面，颜色越往「远处冷蓝」靠，
    /// 近处保持原色。无限地图靠这一层把远处的方块压进背景里，画面才有纵深。
    ///
    /// 距离阈值按正交尺寸等比缩放（nearFactor / farFactor × 视野高度），
    /// 所以拉近拉远时观感一致。
    ///
    /// 只作用于地形（IsoTileMap 的 Ground）与流式海面（SeaStreamer），
    /// 不去碰聚集点/沉船宝藏/施工半透明这些自己会改颜色的对象。
    /// </summary>
    public class AtmosphericPerspective : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("距离（按屏幕中心到边缘的比例）")]
        [Tooltip("屏幕中心到边缘的归一化距离，小于这个值保持原色")]
        public float nearFactor = 0.30f;
        [Tooltip("大于这个值完全变到远景色（1 = 屏幕角）")]
        public float farFactor = 1.00f;

        [Header("颜色")]
        public Color farColor = new Color(0.16f, 0.26f, 0.34f, 1f);
        [Range(0f, 1f)] public float maxBlend = 0.50f;

        [Header("刷新")]
        [Tooltip("颜色刷新间隔（秒），相机没动时不重算")]
        public float updateInterval = 0.1f;
        [Tooltip("相机移动超过这个距离才算「动了」")]
        public float moveEpsilon = 0.02f;
        public bool includeSea = true;

        [Header("水面微光")]
        [Tooltip("让水面有一层很轻的明暗呼吸，画面不至于死板。只压暗不增亮，避免触发泛光")]
        public bool waterShimmer = true;
        [Tooltip("呼吸幅度（0.06 = 最暗时压到 94%）")]
        [Range(0f, 0.3f)] public float shimmerAmplitude = 0.06f;
        [Tooltip("一次完整的明暗周期（秒）")]
        public float shimmerPeriod = 7f;
        [Tooltip("水面颜色刷新间隔（秒），呼吸需要比大气透视刷得更勤")]
        public float shimmerInterval = 0.05f;

        readonly List<SpriteRenderer> _targets = new List<SpriteRenderer>();
        readonly List<bool> _isWater = new List<bool>();
        Transform _groundRoot;
        Transform _seaRoot;
        int _lastGroundCount = -1;
        int _lastSeaCount = -1;

        float _timer;
        float _shimmerTimer;
        Vector3 _lastCamPos = new Vector3(float.MaxValue, 0f, 0f);
        float _lastSize = -1f;

        /// <summary>当前参与着色的精灵数量（验证用）。</summary>
        public int TargetCount => _targets.Count;

        Camera Cam
        {
            get
            {
                if (targetCamera == null) targetCamera = Camera.main;
                return targetCamera;
            }
        }

        void Start()
        {
            Rebuild();
            Apply(force: true);
        }

        void LateUpdate()
        {
            Camera cam = Cam;
            if (cam == null) return;

            // 水面呼吸要一直刷新，所以单独一条计时
            if (waterShimmer)
            {
                _shimmerTimer += Time.unscaledDeltaTime;
                if (_shimmerTimer >= shimmerInterval)
                {
                    _shimmerTimer = 0f;
                    Apply(force: true);
                }
                return;
            }

            _timer += Time.unscaledDeltaTime;
            if (_timer < updateInterval) return;
            _timer = 0f;

            RebuildIfChanged();

            // 相机没动就不用重算
            bool moved = (cam.transform.position - _lastCamPos).sqrMagnitude > moveEpsilon * moveEpsilon;
            if (!moved && Mathf.Approximately(cam.orthographicSize, _lastSize)) return;

            Apply(force: false);
        }

        public void Refresh()
        {
            Rebuild();
            Apply(force: true);
        }

        void RebuildIfChanged()
        {
            int groundCount = _groundRoot != null ? _groundRoot.childCount : 0;
            int seaCount = _seaRoot != null ? _seaRoot.childCount : 0;

            if (_groundRoot == null || _seaRoot == null
                || groundCount != _lastGroundCount || seaCount != _lastSeaCount)
                Rebuild();
        }

        void Rebuild()
        {
            _targets.Clear();
            _isWater.Clear();

            var map = FindObjectOfType<IsoTileMap>();
            _groundRoot = map != null ? map.transform : null;

            if (includeSea)
            {
                var streamer = FindObjectOfType<SeaStreamer>();
                _seaRoot = streamer != null ? streamer.transform : null;
            }
            else _seaRoot = null;

            if (_groundRoot != null)
            {
                Collect(_groundRoot);
                _lastGroundCount = _groundRoot.childCount;
            }
            else _lastGroundCount = -1;

            if (_seaRoot != null)
            {
                Collect(_seaRoot);
                _lastSeaCount = _seaRoot.childCount;
            }
            else _lastSeaCount = -1;
        }

        /// <summary>
        /// 收集要着色的精灵。脚底阴影（shadow）不能进列表——
        /// 它本来就要保持纯黑，被这里刷白就看不见了。
        /// </summary>
        void Collect(Transform root)
        {
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                if (sr.gameObject.name == "shadow") continue;
                _targets.Add(sr);
                _isWater.Add(IsWaterName(sr.gameObject.name));
            }
        }

        /// <summary>瓦片名是不是水面（深水/浅水/水面A/水面B，以及流式海面的 sea_i_j）。</summary>
        static bool IsWaterName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("sea_")) return true;
            return name.Contains("深水") || name.Contains("浅水") || name.Contains("水面");
        }

        void Apply(bool force)
        {
            Camera cam = Cam;
            if (cam == null) return;

            if (force) RebuildIfChanged();

            float near = Mathf.Clamp01(nearFactor);
            float far = Mathf.Max(near + 0.01f, Mathf.Clamp01(farFactor));
            float phase = Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.5f, shimmerPeriod);

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                SpriteRenderer sr = _targets[i];
                if (sr == null) { _targets.RemoveAt(i); if (i < _isWater.Count) _isWater.RemoveAt(i); continue; }

                // 用视口坐标算「离画面中心多远」：0 = 正中心，1 = 屏幕角。
                // 这样不用关心地图尺寸和缩放级别，画面边缘永远是渐淡的。
                Vector3 vp = cam.WorldToViewportPoint(sr.bounds.center);
                float dx = vp.x - 0.5f;
                float dy = vp.y - 0.5f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 0.7071f);

                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - near) / (far - near))) * maxBlend;
                Color c = Color.Lerp(Color.white, farColor, t);

                // 水面：叠一层很轻的明暗呼吸。按世界坐标错开相位，波纹看起来是滚动的。
                // 只往暗里压，不往亮里推，免得触发 Bloom 闪烁。
                if (waterShimmer && i < _isWater.Count && _isWater[i])
                {
                    Vector3 p = sr.transform.position;
                    float ph = phase + (p.x * 0.9f + p.y * 1.7f);
                    float k = 1f - shimmerAmplitude * (0.5f + 0.5f * Mathf.Sin(ph));
                    c.r *= k; c.g *= k; c.b *= k;
                }

                sr.color = c;
            }

            _lastCamPos = cam.transform.position;
            _lastSize = cam.orthographicSize;
        }
    }
}
