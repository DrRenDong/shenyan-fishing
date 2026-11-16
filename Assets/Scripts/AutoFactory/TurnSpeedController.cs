using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 时间流逝：三个档位自动推进回合（1 回合 3 秒 / 1 回合 1 秒 / 1 秒 3 回合）。
    /// 点同一档再点一次就停下；按钮素材在 Image/UI/工厂按钮/speed_*。
    /// </summary>
    public class TurnSpeedController : MonoBehaviour
    {
        public static TurnSpeedController Instance { get; private set; }

        [Header("外观")]
        [Tooltip("与右下角功能键对齐（TurnHud.buttonHeight = 84）")]
        public float buttonHeight = 84f;
        public float gap = 8f;
        public float margin = 30f;
        [Tooltip("右边缘给菜单按钮让出的宽度（工厂模式菜单在右上角；菜单画布是 1280×720 参考，实际会放大）")]
        public float rightReserve = 180f;
        public Color idleTint = new Color(1f, 1f, 1f, 0.55f);

        struct Gear
        {
            public string sprite;
            public float interval;   // 秒 / 回合
        }

        static readonly Gear[] Gears =
        {
            new Gear { sprite = "speed_turn3s", interval = 3f },
            new Gear { sprite = "speed_turn1s", interval = 1f },
            new Gear { sprite = "speed_3x",     interval = 1f / 3f },
        };

        int _active = -1;
        float _timer;
        Image[] _images;
        GameObject[] _halos;

        public bool Running => _active >= 0;
        public int ActiveGear => _active;

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
            var canvasGo = new GameObject("TurnSpeedCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 4989;      // 和 HUD 同层，仍在各种面板（5000+）之下
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            float h = buttonHeight;
            float w = h * 408f / 410f;
            float total = Gears.Length * w + (Gears.Length - 1) * gap;

            var bar = new GameObject("SpeedBar", typeof(RectTransform));
            var barRect = bar.GetComponent<RectTransform>();
            barRect.SetParent(canvasGo.transform, false);
            barRect.anchorMin = barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 1f);
            barRect.sizeDelta = new Vector2(total, h);
            barRect.anchoredPosition = new Vector2(-(margin + rightReserve), -margin);

            _images = new Image[Gears.Length];

            // 选中框先建：同一层里先建的画在下面，正好垫在按钮后面
            var halos = new GameObject[Gears.Length];
            for (int i = 0; i < Gears.Length; i++)
            {
                var haloGo = new GameObject("Halo" + i, typeof(RectTransform), typeof(Image));
                var haloRect = haloGo.GetComponent<RectTransform>();
                haloRect.SetParent(barRect, false);
                haloRect.anchorMin = haloRect.anchorMax = new Vector2(0f, 1f);
                haloRect.pivot = new Vector2(0.5f, 0.5f);
                haloRect.sizeDelta = new Vector2(w + 12f, h + 12f);
                haloRect.anchoredPosition = new Vector2(i * (w + gap) + w * 0.5f, -h * 0.5f);
                var haloImg = haloGo.GetComponent<Image>();
                haloImg.color = new Color(1f, 0.88f, 0.45f, 0.9f);
                haloImg.raycastTarget = false;
                haloGo.SetActive(false);
                halos[i] = haloGo;
            }
            _halos = halos;

            for (int i = 0; i < Gears.Length; i++)
            {
                int idx = i;
                var go = new GameObject("Speed" + i, typeof(RectTransform), typeof(Image), typeof(Button), typeof(UiPressScale));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(barRect, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(w, h);
                rect.anchoredPosition = new Vector2(i * (w + gap), 0f);

                var img = go.GetComponent<Image>();
                img.sprite = Resources.Load<Sprite>("Image/UI/工厂按钮/" + Gears[i].sprite);
                img.preserveAspect = true;
                img.raycastTarget = true;
                if (img.sprite == null) Debug.LogWarning("[速度] 缺少按钮素材 " + Gears[i].sprite);
                _images[i] = img;

                var btn = go.GetComponent<Button>();
                btn.transition = Selectable.Transition.ColorTint;
                btn.targetGraphic = img;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
                colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
                colors.fadeDuration = 0.06f;
                btn.colors = colors;
                btn.onClick.AddListener(() => ToggleGear(idx));
            }

            var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.SetParent(barRect, false);
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 1f);
            hintRect.sizeDelta = new Vector2(total, 20f);
            hintRect.anchoredPosition = new Vector2(0f, -4f);
            var hint = hintGo.GetComponent<Text>();
            hint.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 15);
            hint.fontSize = 15;
            hint.alignment = TextAnchor.UpperRight;
            hint.color = new Color(0.86f, 0.90f, 0.94f, 0.75f);
            hint.raycastTarget = false;
            hint.text = "点同一档再点一次停止";

            RefreshTint();
        }

        void ToggleGear(int index)
        {
            if (_active == index) { Stop(); return; }
            _active = index;
            _timer = 0f;
            RefreshTint();
            GameAudio.Ensure().PlayUiClick();
        }

        public void Stop()
        {
            if (_active < 0) return;
            _active = -1;
            _timer = 0f;
            RefreshTint();
        }

        void RefreshTint()
        {
            if (_images == null) return;
            for (int i = 0; i < _images.Length; i++)
            {
                if (_images[i] != null)
                    _images[i].color = (_active == i) ? Color.white : idleTint;
                if (_halos != null && i < _halos.Length && _halos[i] != null)
                    _halos[i].SetActive(_active == i);
            }
        }

        void Update()
        {
            if (_active < 0) return;
            if (TurnManager.Instance == null) return;

            float interval = Mathf.Max(0.1f, Gears[_active].interval);
            _timer += Time.unscaledDeltaTime;
            if (_timer < interval) return;

            _timer -= interval;
            if (_timer > interval) _timer = interval;   // 掉帧时别把回合攒成一串
            TurnManager.Instance.EndTurn();
        }
    }
}
