using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 收获反馈：右上角滚动弹出「金枪鱼 5.58kg」这类消息泡（最多 5 条，多了把旧的顶掉），
    /// 顶部中央显示本竿连击数，数字越高颜色越艳，10 条以上变成流动彩虹。
    /// 运行时用代码生成 Canvas + 系统字体，不需要字体资源。
    /// </summary>
    [DisallowMultipleComponent]
    public class CatchFeedUI : MonoBehaviour
    {
        [Header("消息泡")]
        [SerializeField] Vector2 feedAnchor = new Vector2(-28f, -76f);   // 右上角（连击下方）
        [SerializeField] float lineHeight = 30f;
        [SerializeField, Min(1)] int maxMessages = 5;
        [SerializeField, Min(0.5f)] float messageLifetime = 4.5f;
        [SerializeField] int messageFontSize = 20;

        [Header("连击")]
        [SerializeField] Vector2 comboAnchor = new Vector2(-28f, -18f);  // 右上角最上
        [SerializeField] int comboBaseFontSize = 30;
        [SerializeField] int comboFontStep = 4;      // 每层连击加多少号
        [SerializeField] int comboMaxFontSize = 76;

        [Header("等级")]
        [SerializeField] int comboMax = 8;          // 现在一竿最多挂 8 条

        Canvas _canvas;
        Font _font;
        Text _comboText;
        RectTransform _comboRt;
        int _combo;
        float _comboPunch;

        class Entry
        {
            public Text text;
            public RectTransform rt;
            public float bornAt;
            public float targetY;
            public float currentY;
            public bool alive = true;
        }

        readonly List<Entry> _entries = new List<Entry>();

        void Start()
        {
            Build();
        }

        // ---------------- 构建 ----------------

        void Build()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("CatchFeedCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Arial" }, messageFontSize);

            // 连击数（右上角，右对齐）
            var comboGo = new GameObject("Combo", typeof(RectTransform), typeof(Text));
            comboGo.transform.SetParent(canvasGo.transform, false);
            _comboRt = comboGo.GetComponent<RectTransform>();
            _comboRt.anchorMin = _comboRt.anchorMax = new Vector2(1f, 1f);
            _comboRt.pivot = new Vector2(1f, 1f);
            _comboRt.anchoredPosition = comboAnchor;
            _comboRt.sizeDelta = new Vector2(400f, 80f);
            _comboText = comboGo.GetComponent<Text>();
            _comboText.font = _font;
            _comboText.fontSize = comboBaseFontSize;
            _comboText.alignment = TextAnchor.MiddleRight;
            _comboText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _comboText.text = "";

            // 描边让数字在天空/海水背景上都读得清
            var outline = comboGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);
        }

        // ---------------- 对外接口 ----------------

        /// <summary>每条渔获弹一个消息泡，同时连击 +1。</summary>
        public void Push(string fishName, float weight)
        {
            Build();
            _combo++;
            _comboPunch = 1.45f;
            GameAudio.Ensure().PlayCombo(_combo);

            var go = new GameObject("Msg", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(320f, lineHeight);

            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = messageFontSize;
            t.alignment = TextAnchor.MiddleRight;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = fishName + "  " + weight.ToString("0.00") + "kg";
            t.color = new Color(1f, 1f, 1f, 1f);

            var msgOutline = go.AddComponent<Outline>();
            msgOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            msgOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var e = new Entry { text = t, rt = rt, bornAt = Time.time, currentY = feedAnchor.y - 40f };
            _entries.Insert(0, e);

            // 超过上限：最上面那条直接销毁
            while (_entries.Count > maxMessages)
            {
                var old = _entries[_entries.Count - 1];
                _entries.RemoveAt(_entries.Count - 1);
                if (old.text != null) Destroy(old.text.gameObject);
            }

            LayoutEntries();
            UpdateComboText();
        }

        /// <summary>新一竿开始时清空连击。</summary>
        public void ResetCombo()
        {
            _combo = 0;
            _comboPunch = 0f;
            UpdateComboText();
        }

        // ---------------- 内部 ----------------

        void LayoutEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].targetY = feedAnchor.y - i * lineHeight;
        }

        void UpdateComboText()
        {
            if (_comboText == null) return;

            _comboText.text = _combo > 0 ? "连击×" + _combo : "";
            _comboText.color = ComboColor(_combo);

            // 连击越高字号越大（再叠加一次弹跳）
            int size = comboBaseFontSize + (_combo - 1) * comboFontStep;
            size = Mathf.Clamp(size, comboBaseFontSize, comboMaxFontSize);
            size = Mathf.RoundToInt(size * (1f + _comboPunch * 0.22f));
            _comboText.fontSize = size;
        }

        Color ComboColor(int n)
        {
            if (n >= 10)
            {
                float h = Mathf.Repeat(Time.time * 0.55f, 1f);
                return Color.HSVToRGB(h, 1f, 1f);            // 彩虹流动
            }
            if (n >= 7) return new Color(1f, 0.42f, 0.15f);  // 橙红
            if (n >= 4) return new Color(1f, 0.85f, 0.28f);  // 金黄
            if (n >= 2) return new Color(0.85f, 0.95f, 1f);  // 淡青
            return Color.white;
        }

        void Update()
        {
            // 连击数字回弹
            if (_comboPunch > 0f)
            {
                _comboPunch = Mathf.Max(0f, _comboPunch - Time.deltaTime * 3.2f);
                UpdateComboText();
            }
            else if (_combo >= 10 && _comboText != null)
            {
                _comboText.color = ComboColor(_combo);       // 彩虹持续流动
            }

            // 消息泡：上浮 + 淡出
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.text == null) { _entries.RemoveAt(i); continue; }

                e.currentY = Mathf.Lerp(e.currentY, e.targetY, 1f - Mathf.Exp(-14f * Time.deltaTime));
                var p = e.rt.anchoredPosition;
                e.rt.anchoredPosition = new Vector2(feedAnchor.x, e.currentY);

                float age = Time.time - e.bornAt;
                float fade = Mathf.Clamp01(1f - (age - messageLifetime) / 0.8f);
                var c = e.text.color;
                c.a = fade;
                e.text.color = c;

                if (age > messageLifetime + 0.9f)
                {
                    Destroy(e.text.gameObject);
                    _entries.RemoveAt(i);
                }
            }
        }

        public int Combo => _combo;
        public int ComboMax => comboMax;
    }
}
