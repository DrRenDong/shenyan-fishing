using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 左上角两条数值条：载重 / 油量。运行时用代码生成，不依赖预制体。
    /// </summary>
    [DisallowMultipleComponent]
    public class BoatHud : MonoBehaviour
    {
        [SerializeField] BoatStatus status;
        [SerializeField] TimeOfDaySystem timeSystem;
        [SerializeField] Vector2 barSize = new Vector2(220f, 18f);
        [SerializeField] Vector2 origin = new Vector2(24f, -24f);
        [SerializeField] float gap = 28f;
        [SerializeField] float timeRowGap = 34f;

        Image _loadFill, _fuelFill;
        Image _timeProgress;
        readonly Image[] _timeDots = new Image[3];
        static Sprite _white;

        void Start()
        {
            if (status == null) status = FindObjectOfType<BoatStatus>();
            if (timeSystem == null) timeSystem = FindObjectOfType<TimeOfDaySystem>();
            Build();
        }

        void Build()
        {
            if (_loadFill != null) return;

            var canvasGo = new GameObject("BoatHudCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            _loadFill = MakeBar(canvasGo.transform, origin, new Color(0.95f, 0.72f, 0.25f));   // 载重：橙
            _fuelFill = MakeBar(canvasGo.transform, origin + new Vector2(0f, -gap), new Color(0.35f, 0.85f, 0.55f)); // 油量：绿

            BuildTimeRow(canvasGo.transform, origin + new Vector2(0f, -gap * 2f - timeRowGap));
        }

        /// <summary>时段指示：三个小方块（白天/黄昏/夜晚）+ 当前时段进度条。</summary>
        void BuildTimeRow(Transform parent, Vector2 pos)
        {
            Color[] colors =
            {
                new Color(1f, 0.87f, 0.35f),   // 白天：黄
                new Color(0.95f, 0.55f, 0.28f),// 黄昏：橙红
                new Color(0.35f, 0.45f, 0.85f) // 夜晚：蓝
            };

            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("TimeDot" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = pos + new Vector2(i * 22f, 0f);
                rt.sizeDelta = new Vector2(16f, 16f);
                var img = go.GetComponent<Image>();
                img.sprite = White;
                img.color = colors[i];
                _timeDots[i] = img;
            }

            // 进度条
            var bgGo = new GameObject("TimeBg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(parent, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0f, 1f);
            bgRt.pivot = new Vector2(0f, 1f);
            bgRt.anchoredPosition = pos + new Vector2(0f, -22f);
            bgRt.sizeDelta = new Vector2(barSize.x, 8f);
            var bg = bgGo.GetComponent<Image>();
            bg.sprite = White;
            bg.color = new Color(0f, 0f, 0f, 0.5f);

            var fillGo = new GameObject("TimeFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bgGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(1f, 1f);
            fillRt.offsetMax = new Vector2(-1f, -1f);
            _timeProgress = fillGo.GetComponent<Image>();
            _timeProgress.sprite = White;
            _timeProgress.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
            _timeProgress.type = Image.Type.Filled;
            _timeProgress.fillMethod = Image.FillMethod.Horizontal;
            _timeProgress.fillOrigin = (int)Image.OriginHorizontal.Left;
            _timeProgress.fillAmount = 0f;
        }

        Image MakeBar(Transform parent, Vector2 pos, Color fillColor)
        {
            var bgGo = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(parent, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0f, 1f);
            bgRt.pivot = new Vector2(0f, 1f);
            bgRt.anchoredPosition = pos;
            bgRt.sizeDelta = barSize;
            var bg = bgGo.GetComponent<Image>();
            bg.sprite = White;
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            var fillGo = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bgGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = White;
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            return fill;
        }

        static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                }
                return _white;
            }
        }

        void Update()
        {
            if (status == null) status = FindObjectOfType<BoatStatus>();
            if (status != null)
            {
                if (_loadFill != null)
                    _loadFill.fillAmount = Mathf.Clamp01(status.Load / Mathf.Max(1f, status.MaxLoad));
                if (_fuelFill != null)
                    _fuelFill.fillAmount = status.Dockless
                        ? 1f   // 开阔海域（工厂入口捕鱼）：没有码头、不耗油，油量条常满
                        : Mathf.Clamp01(status.Fuel / Mathf.Max(1f, status.MaxFuel));
            }

            UpdateTimeRow();
        }

        void UpdateTimeRow()
        {
            if (timeSystem == null) timeSystem = FindObjectOfType<TimeOfDaySystem>();
            if (timeSystem == null) return;

            int idx = (int)timeSystem.Current;
            for (int i = 0; i < _timeDots.Length; i++)
            {
                if (_timeDots[i] == null) continue;
                var c = _timeDots[i].color;
                c.a = i == idx ? 1f : 0.25f;
                _timeDots[i].color = c;
            }

            if (_timeProgress != null)
                _timeProgress.fillAmount = timeSystem.PeriodProgress;
        }
    }
}
