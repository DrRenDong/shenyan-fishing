using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 本局结算面板：回到港口（BoatStatus.RunOver）时弹出。
    /// 面板靠右，左边站着王老板的胜利立绘；点击任意处 / E / 空格 开下一局。
    /// 运行时用代码生成 Canvas，不依赖预制体，和 BoatHud / CatchFeedUI 一致。
    /// </summary>
    [DisallowMultipleComponent]
    public class RunSummaryUI : MonoBehaviour
    {
        [Header("资源")]
        [SerializeField] Sprite panelSprite;
        [SerializeField] Sprite portraitSprite;

        [Header("布局（参考分辨率 1280×720）")]
        [SerializeField] Vector2 referenceResolution = new Vector2(1280f, 720f);
        [SerializeField] Vector2 panelSize = new Vector2(700f, 467f);              // 3:2
        [SerializeField] Vector2 panelOffset = new Vector2(272f, 0f);               // 2026-11-05 主人手动调整：再往右靠
        [SerializeField] Vector2 portraitSize = new Vector2(904.48f, 1356.72f);     // 大立绘：只露到屏幕底、下半身裁掉
        [SerializeField] Vector2 portraitOffset = new Vector2(-340f, -360f);

        [Header("文字")]
        [SerializeField] int titleFontSize = 40;
        [SerializeField] int bodyFontSize = 30;
        [SerializeField] int hintFontSize = 22;

        [Header("行为")]
        [SerializeField] bool hideHudOnShow = true;   // 结算时把左上角载重/油量条和右上角消息收起来

        [Header("参考")]
        [SerializeField] BoatStatus status;

        Canvas _canvas;
        CanvasGroup _group;
        RectTransform _root;
        Text _title, _catch, _money, _time, _hint;
        RectTransform _mapBtnRt;
        Font _font;
        bool _shown;
        float _fade;
        bool _clickPrev, _keyPrev;

        void Awake()
        {
            if (status == null) status = FindObjectOfType<BoatStatus>();
            if (panelSprite == null) panelSprite = Resources.Load<Sprite>("Image/UI/结算面板");
            if (portraitSprite == null) portraitSprite = Resources.Load<Sprite>("Image/UI/王老板_胜利");
            Build();
        }

        void Build()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("RunSummaryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            var rootGo = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup));
            rootGo.transform.SetParent(canvasGo.transform, false);
            _root = rootGo.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _group = rootGo.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Arial" }, bodyFontSize);

            MakeImage("Panel", panelSprite, panelOffset, panelSize);
            MakePortrait();
            BuildTexts();
            BuildMapButton();

            canvasGo.SetActive(false);   // 等本局结束再显示
        }

        void MakeImage(string name, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (sprite == null) img.color = new Color(0.1f, 0.15f, 0.25f, 0.9f);
        }

        void MakePortrait()
        {
            // 位置和大小直接在 Inspector 里调（主人 2026-11-05 摆好的数值），
            // 不再从 panelSize 推算，这样调整时所见即所得
            MakeImage("Portrait", portraitSprite, portraitOffset, portraitSize);
        }

        void BuildTexts()
        {
            // 实测面板图：绶带在面板中心上方 122~170，米白纸面 y ∈ [-153, 120]、宽约面板的 75%
            _title = MakeText("Title", "", titleFontSize, new Color(0.34f, 0.19f, 0.08f), 148f, 56f, 460f);
            _title.fontStyle = FontStyle.Bold;

            var body = new Color(0.22f, 0.2f, 0.26f);
            _catch = MakeText("Catch", "", bodyFontSize, body, 45f, 46f, 480f);
            _money = MakeText("Money", "", bodyFontSize, body, -13f, 46f, 480f);
            _time = MakeText("Time", "", bodyFontSize, body, -71f, 46f, 480f);

            _hint = MakeText("Hint", "", hintFontSize, new Color(0.33f, 0.29f, 0.23f, 0.95f), -122f, 34f, 480f);
        }

        /// <summary>面板下方的「返回渔港地图」按钮：点了就离开本局，回主界面。</summary>
        void BuildMapButton()
        {
            var go = new GameObject("MapButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_root, false);
            _mapBtnRt = go.GetComponent<RectTransform>();
            _mapBtnRt.anchorMin = _mapBtnRt.anchorMax = new Vector2(0.5f, 0.5f);
            _mapBtnRt.pivot = new Vector2(0.5f, 0.5f);
            _mapBtnRt.anchoredPosition = new Vector2(panelOffset.x, -252f);
            _mapBtnRt.sizeDelta = new Vector2(320f, 54f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.24f, 0.15f, 0.10f, 0.95f);

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.onClick.AddListener(BackToMap);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lRt = labelGo.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero;
            lRt.offsetMax = Vector2.zero;

            var t = labelGo.GetComponent<Text>();
            t.font = _font;
            t.fontSize = 26;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.98f, 0.93f, 0.80f, 1f);
            t.raycastTarget = false;
            t.text = "返回渔港地图";
        }

        public void BackToMap()
        {
            Debug.Log("[结算] 返回渔港地图");
            SceneManager.LoadScene("MainMenu");
        }

        Text MakeText(string name, string text, int size, Color color, float y, float h, float w)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(panelOffset.x, y);
            rt.sizeDelta = new Vector2(w, h);

            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.color = color;
            t.text = text;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        void Show()
        {
            _shown = true;
            if (_canvas == null) return;
            _canvas.gameObject.SetActive(true);
            _fade = 0f;
            _group.alpha = 0f;
            // 关键：CanvasGroup 默认 blocksRaycasts=false，不打开的话按钮永远收不到点击
            // （「重开一局」走 Input.GetMouseButton 所以看起来正常，会误导排查）
            _group.blocksRaycasts = true;
            _root.localScale = Vector3.one * 0.94f;

            int total = Mathf.Max(0, Mathf.RoundToInt(status != null ? status.RunSeconds : 0f));
            _title.text = "本局结算";
            _catch.text = "渔获　" + (status != null ? status.RunCatch : 0) + " 条";
            _money.text = "收入　" + (status != null ? status.RunMoney.ToString("N0") : "0") + " 金";
            _time.text = "用时　" + (total / 60) + " 分 " + (total % 60) + " 秒";
            _hint.text = "点击任意处 / 按 E 开始下一局";

            // 本局金币入账（经验已在装船时按连击逐条结算，这里不再重复加）
            if (status != null)
            {
                PlayerProgress.AddGold(status.RunMoney);
                SaveSystem.Save();   // 一局结束自动存档
            }

            if (hideHudOnShow)
            {
                var hudCanvas = GameObject.Find("BoatHudCanvas");
                if (hudCanvas != null) hudCanvas.SetActive(false);
                var feedCanvas = GameObject.Find("CatchFeedCanvas");
                if (feedCanvas != null) feedCanvas.SetActive(false);
            }
        }

        void Update()
        {
            if (!_shown)
            {
                if (status != null && status.RunOver) Show();
                return;
            }

            // 淡入 + 轻微放大
            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime * 4f);
                _group.alpha = _fade;
                _root.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, _fade);
            }

            // 交互：用 GetMouseButton/GetKey + 上一帧做边沿判断（GetKeyDown 没焦点收不到）
            bool click = Input.GetMouseButton(0);
            bool key = Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return);

            // 鼠标压在「返回渔港地图」按钮上时，不要顺手触发“开始下一局”
            bool overButton = _mapBtnRt != null &&
                              RectTransformUtility.RectangleContainsScreenPoint(_mapBtnRt, Input.mousePosition, null);

            if (_fade > 0.35f && !overButton && status != null && ((click && !_clickPrev) || (key && !_keyPrev)))
                status.RestartRun();
            _clickPrev = click;
            _keyPrev = key;
        }
    }
}
