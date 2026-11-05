using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 移动端触控：屏幕三等分——左右按住左右移动、中间单击下杆/收杆；
    /// 右下角两个按钮放鱼雷 / 洒饵；任意位置上下拖动平移相机。
    /// 支持陀螺仪左右倾斜（触控优先）。
    /// 只在移动平台生效，桌面端完全不影响键盘鼠标。
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileControls : MonoBehaviour
    {
        public static MobileControls Instance { get; private set; }

        [Header("拖动灵敏度")]
        [SerializeField] float dragScrollScale = 0.035f;

        [Header("陀螺仪")]
        [SerializeField] bool gyroEnabled = false;   // 默认关闭，去设置里开
        [SerializeField] float gyroGain = 2.5f;
        [SerializeField] float gyroDeadZone = 0.12f;

        public float Horizontal { get; private set; }
        public bool IsMobile => Application.isMobilePlatform;

        /// <summary>设置面板里的陀螺仪开关（默认关闭，改动会被记住）。</summary>
        const string GyroPrefKey = "gyro_enabled";

        public bool GyroEnabled
        {
            get => gyroEnabled;
            set
            {
                gyroEnabled = value;
                PlayerPrefs.SetInt(GyroPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (value && SystemInfo.supportsGyroscope) Input.gyro.enabled = true;
            }
        }

        Canvas _canvas;
        RectTransform _torpedoBtn, _baitBtn;
        Text _torpedoLabel, _baitLabel;
        Font _font;

        float _gyroSmooth;
        float _touchH;
        bool _leftHeld, _rightHeld;
        int _castFingerId = -1;
        Vector2 _castStart;
        bool _castMoved;

        void Awake()
        {
            Instance = this;

            // 陀螺仪：没存过就是关，存过就按上次的选择（改设置后下一局也保持）
            gyroEnabled = PlayerPrefs.GetInt(GyroPrefKey, 0) == 1;
        }

        void Start()
        {
            if (!IsMobile) return;

            if (gyroEnabled && SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = true;
            }
            BuildUI();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("MobileControlsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);

            _torpedoBtn = MakeButton(canvasGo.transform, "鱼雷", new Vector2(-150f, 110f), () =>
            {
                var qin = FindObjectOfType<QinJunActor>();
                if (qin != null) qin.MobileTorpedo();
            });
            _torpedoLabel = _torpedoBtn.Find("Label").GetComponent<Text>();

            _baitBtn = MakeButton(canvasGo.transform, "饵料", new Vector2(-150f, 240f), () =>
            {
                var qin = FindObjectOfType<QinJunActor>();
                if (qin != null) qin.MobileBait();
            });
            _baitLabel = _baitBtn.Find("Label").GetComponent<Text>();
        }

        RectTransform MakeButton(Transform parent, string label, Vector2 pos, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(112f, 112f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.15f, 0.11f, 0.08f, 0.55f);

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var t = textGo.GetComponent<Text>();
            t.font = _font;
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.96f, 0.86f);
            t.raycastTarget = false;
            t.text = label;

            return rt;
        }

        void Update()
        {
            if (!IsMobile) return;

            _leftHeld = _rightHeld = false;
            _touchH = 0f;

            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.touches[i];
                if (IsOnButton(t.position)) continue;   // 按钮自己处理

                float w = Screen.width;
                // 五等分：左右各 1/5 移动，中间 3/5 是下杆/收杆
                bool leftZone = t.position.x < w * 0.2f;
                bool rightZone = t.position.x > w * 0.8f;

                switch (t.phase)
                {
                    case TouchPhase.Began:
                        if (leftZone) _leftHeld = true;
                        else if (rightZone) _rightHeld = true;
                        else
                        {
                            _castFingerId = t.fingerId;
                            _castStart = t.position;
                            _castMoved = false;
                        }
                        break;

                    case TouchPhase.Stationary:
                    case TouchPhase.Moved:
                        if (leftZone) _leftHeld = true;
                        else if (rightZone) _rightHeld = true;

                        // 任意位置的上下拖动 = 平移相机
                        if (Mathf.Abs(t.deltaPosition.y) > 0.01f)
                        {
                            var cam = FindObjectOfType<CameraFollow>();
                            if (cam != null) cam.ScrollBy(t.deltaPosition.y * dragScrollScale);
                        }

                        if (t.fingerId == _castFingerId &&
                            Vector2.Distance(t.position, _castStart) > 24f)
                            _castMoved = true;
                        break;

                    case TouchPhase.Ended:
                        if (t.fingerId == _castFingerId)
                        {
                            // 中间区域轻点 = 下杆 / 收杆
                            if (!_castMoved)
                            {
                                var qin = FindObjectOfType<QinJunActor>();
                                if (qin != null) qin.MobileCast();
                            }
                            _castFingerId = -1;
                        }
                        break;

                    case TouchPhase.Canceled:
                        if (t.fingerId == _castFingerId) _castFingerId = -1;
                        break;
                }
            }

            if (_leftHeld) _touchH = -1f;
            if (_rightHeld) _touchH = 1f;

            UpdateGyro();
            Horizontal = _touchH != 0f ? _touchH : _gyroSmooth;

            UpdateButtonLabels();
        }

        /// <summary>战斗中把「鱼雷 / 饵料」换成「海龟 / 鲸鱼」。</summary>
        void UpdateButtonLabels()
        {
            var qin = FindObjectOfType<QinJunActor>();
            bool battle = qin != null && qin.InBattleHint;

            if (_torpedoLabel != null)
            {
                string s = battle ? "海龟" : "鱼雷";
                if (_torpedoLabel.text != s) _torpedoLabel.text = s;
            }
            if (_baitLabel != null)
            {
                string s = battle ? "鲸鱼" : "饵料";
                if (_baitLabel.text != s) _baitLabel.text = s;
            }
        }

        void UpdateGyro()
        {
            if (!gyroEnabled || !SystemInfo.supportsGyroscope) { _gyroSmooth = 0f; return; }

            // 横屏时 acceleration.x 就是左右倾斜
            float target = Mathf.Clamp(Input.acceleration.x * gyroGain, -1f, 1f);
            if (Mathf.Abs(target) < gyroDeadZone) target = 0f;
            _gyroSmooth = Mathf.MoveTowards(_gyroSmooth, target, Time.deltaTime * 4f);
        }

        bool IsOnButton(Vector2 screenPos)
        {
            if (_torpedoBtn != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_torpedoBtn, screenPos, null)) return true;
            if (_baitBtn != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_baitBtn, screenPos, null)) return true;
            return false;
        }
    }
}
