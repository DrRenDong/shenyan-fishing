using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 王老板的面板：上面是主线订单（给区块与科技），下面是限时订单（给 3 倍现金 + 区块）。
    /// </summary>
    public class BossOrderPanel : MonoBehaviour
    {
        public static BossOrderPanel Instance { get; private set; }

        [Header("外观")]
        public float panelWidth = 820f;
        public float panelHeight = 820f;
        public Color backgroundColor = new Color(0.055f, 0.07f, 0.09f, 1f);
        public Color accentColor = new Color(0.86f, 0.68f, 0.34f, 1f);
        public Color buttonColor = new Color(0.24f, 0.46f, 0.34f, 1f);
        public Color rushColor = new Color(0.46f, 0.30f, 0.22f, 1f);
        public KeyCode toggleKey = KeyCode.O;

        Font _font;
        CanvasGroup _group;

        Text _title;
        Text _brief;
        Text _need;
        Text _reward;
        Button _deliverBtn;
        Text _deliverLabel;

        Text _rushTitle;
        Text _rushNeed;
        Text _rushInfo;
        Button _rushDeliverBtn;
        Text _rushDeliverLabel;
        Button _refreshBtn;
        Text _refreshLabel;

        float _timer;

        void Awake()
        {
            Instance = this;
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
            BuildUI();
            SetShown(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
            SlidePanel();
            if (!IsShown) return;

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.3f;
            Refresh();
        }

        RectTransform _panel;
        bool _targetShown;

        void SlidePanel()
        {
            if (_panel == null) return;
            float target = _targetShown ? 24f : -_panel.sizeDelta.y - 40f;
            Vector2 p = _panel.anchoredPosition;
            p.y = Mathf.Lerp(p.y, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            _panel.anchoredPosition = p;
        }

        public bool IsShown => _targetShown;

        public void Toggle() => SetShown(!IsShown);

        public void SetShown(bool on)
        {
            if (_group == null) return;
            _group.gameObject.SetActive(true);
            _targetShown = on;
            if (on) Refresh();
        }

        public void BuildUI()
        {
            Transform existing = transform.Find("BossOrderCanvas");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);

            var canvasGo = new GameObject("BossOrderCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5080;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _group = canvasGo.AddComponent<CanvasGroup>();

            // 不压黑遮罩：面板从屏幕下方滑入

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var panel = panelGo.GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(panelWidth, panelHeight);
            panel.anchoredPosition = new Vector2(0f, -panelHeight - 40f);
            _panel = panel;
            panelGo.GetComponent<Image>().color = backgroundColor;

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(panel, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accentGo.GetComponent<Image>().color = accentColor;

            _title = MakeText(panel, "王老板的特殊订单", 30, FontStyle.Bold, new Color(0.96f, 0.97f, 0.98f),
                TextAnchor.UpperLeft);
            SetTop(_title, -20f, 40f);

            _brief = MakeText(panel, "", 20, FontStyle.Normal, new Color(0.78f, 0.84f, 0.88f), TextAnchor.UpperLeft);
            _brief.lineSpacing = 1.35f;
            SetTop(_brief, -66f, 86f);

            _need = MakeText(panel, "", 24, FontStyle.Bold, new Color(0.92f, 0.96f, 1f), TextAnchor.UpperLeft);
            SetTop(_need, -166f, 70f);

            _reward = MakeText(panel, "", 21, FontStyle.Normal, new Color(0.86f, 0.78f, 0.52f), TextAnchor.UpperLeft);
            SetTop(_reward, -244f, 56f);

            var divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            var div = divGo.GetComponent<RectTransform>();
            div.SetParent(panel, false);
            div.anchorMin = new Vector2(0f, 1f);
            div.anchorMax = new Vector2(1f, 1f);
            div.pivot = new Vector2(0.5f, 1f);
            div.sizeDelta = new Vector2(-56f, 2f);
            div.anchoredPosition = new Vector2(0f, -312f);
            divGo.GetComponent<Image>().color = new Color(0.86f, 0.68f, 0.34f, 0.35f);

            _rushTitle = MakeText(panel, "限时订单", 24, FontStyle.Bold, new Color(0.92f, 0.82f, 0.58f), TextAnchor.UpperLeft);
            SetTop(_rushTitle, -328f, 32f);

            _rushNeed = MakeText(panel, "", 22, FontStyle.Normal, new Color(0.90f, 0.94f, 0.97f), TextAnchor.UpperLeft);
            SetTop(_rushNeed, -368f, 100f);

            _rushInfo = MakeText(panel, "", 20, FontStyle.Normal, new Color(0.80f, 0.75f, 0.60f), TextAnchor.UpperLeft);
            SetTop(_rushInfo, -480f, 64f);

            // 关闭按钮
            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.SetParent(panel, false);
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(110f, 38f);
            closeRect.anchoredPosition = new Vector2(-22f, -14f);
            closeGo.GetComponent<Image>().color = new Color(0.24f, 0.28f, 0.32f, 1f);
            closeGo.GetComponent<Button>().onClick.AddListener(() => SetShown(false));
            closeGo.AddComponent<UIScaleOnHover>().Configure(1.06f, false);
            closeGo.AddComponent<UiPressScale>();

            var closeText = MakeText(panel, "关闭", 20, FontStyle.Normal, new Color(0.95f, 0.97f, 1f), TextAnchor.MiddleCenter);
            closeText.rectTransform.SetParent(closeRect, false);
            closeText.rectTransform.anchorMin = Vector2.zero;
            closeText.rectTransform.anchorMax = Vector2.one;
            closeText.rectTransform.offsetMin = Vector2.zero;
            closeText.rectTransform.offsetMax = Vector2.zero;

            // 三个按钮
            _deliverBtn = MakeButton(panel, "交付", -220f, buttonColor, out _deliverLabel);
            _deliverBtn.gameObject.AddComponent<UIScaleOnHover>().Configure(1.05f, false);
            _deliverBtn.gameObject.AddComponent<UiPressScale>();
            _deliverBtn.onClick.AddListener(OnDeliver);

            _rushDeliverBtn = MakeButton(panel, "交付限时", 0f, rushColor, out _rushDeliverLabel);
            _rushDeliverBtn.gameObject.AddComponent<UIScaleOnHover>().Configure(1.05f, false);
            _rushDeliverBtn.gameObject.AddComponent<UiPressScale>();
            _rushDeliverBtn.onClick.AddListener(OnRushDeliver);

            _refreshBtn = MakeButton(panel, "刷新", 220f, new Color(0.22f, 0.30f, 0.40f, 1f), out _refreshLabel);
            _refreshBtn.gameObject.AddComponent<UIScaleOnHover>().Configure(1.05f, false);
            _refreshBtn.gameObject.AddComponent<UiPressScale>();
            _refreshBtn.onClick.AddListener(OnRushRefresh);
        }

        void SetTop(Text text, float y, float height)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-64f, height);
            rect.anchoredPosition = new Vector2(28f, y);
        }

        Button MakeButton(RectTransform panel, string label, float x, Color color, out Text labelText)
        {
            var btnGo = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = btnGo.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(200f, 54f);
            rect.anchoredPosition = new Vector2(x, 28f);
            btnGo.GetComponent<Image>().color = color;

            var btn = btnGo.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            btn.colors = colors;

            labelText = MakeText(rect, label, 22, FontStyle.Bold, new Color(0.96f, 0.98f, 1f), TextAnchor.MiddleCenter);
            labelText.rectTransform.anchorMin = Vector2.zero;
            labelText.rectTransform.anchorMax = Vector2.one;
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;
            return btn;
        }

        // ---------------- 刷新显示 ----------------

        public void Refresh()
        {
            RefreshMain();
            RefreshRush();
        }

        void RefreshMain()
        {
            var system = BossOrderSystem.Instance;
            if (system == null)
            {
                _title.text = "王老板的特殊订单";
                _brief.text = "（订单系统未初始化）";
                _need.text = "";
                _reward.text = "";
                if (_deliverBtn != null) _deliverBtn.interactable = false;
                return;
            }

            if (system.AllDone)
            {
                _title.text = "王老板的特殊订单";
                _brief.text = "所有的单子都交付完了。王老板拍着你的肩膀说：以后有货，先送到我这儿。";
                _need.text = "";
                _reward.text = "土地局已经认可了你的经营能力。";
                if (_deliverBtn != null) _deliverBtn.interactable = false;
                if (_deliverLabel != null) _deliverLabel.text = "已完成";
                return;
            }

            BossOrder order = system.Current;
            float have = system.Stock(order.itemId);
            bool ready = have >= order.amount - 1e-3f;

            _title.text = "王老板的特殊订单";
            _brief.text = $"<color=#F0D48A>{order.title}</color>\n{order.brief}";

            string itemName = ItemDatabase.NameOf(order.itemId);
            string unit = ItemDatabase.Get(order.itemId).UnitText;
            _need.text = $"需求：<color=#F0D48A>{itemName} {order.amount:0.#}{unit}</color>" +
                         $"\n库存：<color={(ready ? "#8FE08A" : "#FF9A8A")}>{have:0.#}{unit}</color>";

            string reward = $"报酬：区块 <color=#8FE08A>×{order.blocks}</color>";
            if (!string.IsNullOrEmpty(order.unlockName))
                reward += $"　解锁工艺：<color=#7FD1E8>{order.unlockName}</color>";
            // 有的订单一次解锁两座建筑（接收塔 + 棱镜塔），报酬要把它们都写出来
            if (order.extraUnlocks != null)
                foreach (var extra in order.extraUnlocks)
                    if (!string.IsNullOrEmpty(extra))
                        reward += $"<color=#7FD1E8>、{extra}</color>";
            reward += "\n<color=#8A98A6>（王老板要看的是你把鱼做出花样的本事）</color>";
            _reward.text = reward;

            if (_deliverBtn != null) _deliverBtn.interactable = ready;
            if (_deliverLabel != null) _deliverLabel.text = ready ? "交付" : "货还不够";
        }

        void RefreshRush()
        {
            var rush = BossRushSystem.Instance;
            if (rush == null)
            {
                _rushTitle.text = "限时订单";
                _rushNeed.text = "（限时订单系统未初始化）";
                _rushInfo.text = "";
                if (_rushDeliverBtn != null) _rushDeliverBtn.interactable = false;
                if (_refreshBtn != null) _refreshBtn.interactable = false;
                return;
            }

            rush.EnsureOrder();
            var order = rush.current;
            _rushTitle.text = "限时订单 <color=#8A98A6>（每 5 回合可刷新一次）</color>";

            if (order == null)
            {
                _rushNeed.text = "";
                _rushInfo.text = "";
                if (_rushDeliverBtn != null) _rushDeliverBtn.interactable = false;
                if (_refreshBtn != null) _refreshBtn.interactable = false;
                return;
            }

            float have = rush.Stock(order.itemId);
            bool ready = rush.CanDeliver;
            string unit = ItemDatabase.Get(order.itemId).UnitText;

            _rushNeed.text = $"<color=#F0D48A>{order.title}</color>\n{order.brief}\n" +
                             $"需求：<color=#F0D48A>{ItemDatabase.NameOf(order.itemId)} {order.amount:0.#}{unit}</color>" +
                             $"　库存：<color={(ready ? "#8FE08A" : "#FF9A8A")}>{have:0.#}{unit}</color>";

            int left = rush.TurnsUntilRefresh;
            _rushInfo.text = $"报酬：<color=#8FE08A>{rush.RewardGold:N0} 金</color>（正常售价的 3 倍）＋ <color=#8FE08A>区块×1</color>" +
                             (left > 0 ? $"　　<color=#8A98A6>还有 {left} 回合可以刷新</color>" : "　　<color=#8FE08A>现在可以刷新</color>");

            if (_rushDeliverBtn != null) _rushDeliverBtn.interactable = ready;
            if (_rushDeliverLabel != null) _rushDeliverLabel.text = ready ? "交付限时" : "货还不够";
            if (_refreshBtn != null) _refreshBtn.interactable = rush.CanRefresh;
            if (_refreshLabel != null) _refreshLabel.text = rush.CanRefresh ? "刷新" : $"刷新（{left}）";
        }

        // ---------------- 交互 ----------------

        void OnDeliver()
        {
            var system = BossOrderSystem.Instance;
            if (system == null || !system.CanDeliver()) return;
            if (system.Deliver())
            {
                GameAudio.Ensure().PlayOrderSfx();
                Refresh();
                SetShown(false);      // 交付完立刻收起面板
            }
        }

        void OnRushDeliver()
        {
            var rush = BossRushSystem.Instance;
            if (rush == null || !rush.CanDeliver) return;
            if (rush.Deliver())
            {
                GameAudio.Ensure().PlayOrderSfx();
                Refresh();
                SetShown(false);
            }
        }

        void OnRushRefresh()
        {
            var rush = BossRushSystem.Instance;
            if (rush == null) return;
            rush.RefreshOrder();
            Refresh();
        }

        Text MakeText(RectTransform parent, string text, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.text = text;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }
    }
}
