using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 配方速查面板（Tab 或左上角按钮打开）：
    /// 按品类列出所有成品，以及它们要经过的工厂与工序，方便玩家规划生产链。
    /// </summary>
    public class RecipeBookPanel : MonoBehaviour
    {
        public static RecipeBookPanel Instance { get; private set; }

        [Header("外观")]
        public float panelWidth = 980f;
        public float panelHeight = 780f;
        public Color backgroundColor = new Color(0.055f, 0.07f, 0.09f, 1f);
        public Color accentColor = new Color(0.36f, 0.78f, 0.86f, 1f);
        public KeyCode toggleKey = KeyCode.Tab;

        Font _font;
        CanvasGroup _group;
        bool _shown;

        static readonly string[] GroupNames =
        {
            "初加工 / 半成品",
            "鲜品 / 冷冻",
            "成型食品",
            "干制 / 烟熏 / 包装",
            "罐头 / 调味",
        };

        static readonly FishStage[][] GroupStages =
        {
            new[] { FishStage.Fillet, FishStage.Breading, FishStage.Fried, FishStage.Minced },
            new[] { FishStage.Frozen },
            new[] { FishStage.Ball, FishStage.Cake, FishStage.Sausage },
            new[] { FishStage.Dried, FishStage.Smoked, FishStage.Bagged },
            new[] { FishStage.Canned, FishStage.TomatoCan, FishStage.Salted, FishStage.Sauce },
        };

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
            if (Input.GetKeyDown(toggleKey)) SetShown(!_shown);
            SlidePanel();
            if (_shown && Input.GetKeyDown(KeyCode.Escape)) SetShown(false);
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

        public bool IsShown => _shown;

        public void Toggle() => SetShown(!_shown);

        public void SetShown(bool on)
        {
            _shown = on;
            if (_group != null) _group.gameObject.SetActive(true);
            _targetShown = on;
        }

        public void BuildUI()
        {
            Transform existing = transform.Find("RecipeBookCanvas");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);

            var canvasGo = new GameObject("RecipeBookCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5050;      // 高于建造面板与 HUD
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
            panel.anchoredPosition = new Vector2(0f, -panelHeight - 40f);   // 先在屏幕外
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

            var title = MakeText(panel, "配方速查（Tab 关闭）", 30, FontStyle.Bold, new Color(0.96f, 0.97f, 0.98f));
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-64f, 44f);
            title.rectTransform.anchoredPosition = new Vector2(32f, -22f);

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

            var closeText = MakeText(panel, "关闭", 20, FontStyle.Normal, new Color(0.95f, 0.97f, 1f));
            closeText.rectTransform.SetParent(closeRect, false);
            closeText.rectTransform.anchorMin = Vector2.zero;
            closeText.rectTransform.anchorMax = Vector2.one;
            closeText.rectTransform.offsetMin = Vector2.zero;
            closeText.rectTransform.offsetMax = Vector2.zero;
            closeText.alignment = TextAnchor.MiddleCenter;

            // 正文：ScrollRect
            var areaGo = new GameObject("BodyArea", typeof(RectTransform), typeof(ScrollRect));
            var area = areaGo.GetComponent<RectTransform>();
            area.SetParent(panel, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(32f, 28f);
            area.offsetMax = new Vector2(-32f, -78f);

            var scroll = areaGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.SetParent(area, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-14f, 0f);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            var bodyRect = bodyGo.GetComponent<RectTransform>();
            bodyRect.SetParent(viewport, false);
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.sizeDelta = Vector2.zero;
            bodyRect.anchoredPosition = Vector2.zero;

            var body = bodyGo.GetComponent<Text>();
            body.font = _font;
            body.fontSize = 22;
            body.color = new Color(0.82f, 0.87f, 0.90f);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.supportRichText = true;
            body.lineSpacing = 1.35f;
            body.text = BuildContent();

            var fitter = bodyGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = bodyRect;

            var barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var bar = barGo.GetComponent<RectTransform>();
            bar.SetParent(area, false);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(10f, 0f);
            bar.anchoredPosition = Vector2.zero;
            barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.07f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handle = handleGo.GetComponent<RectTransform>();
            handle.SetParent(bar, false);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.sizeDelta = Vector2.zero;
            var handleImage = handleGo.GetComponent<Image>();
            handleImage.color = new Color(0.36f, 0.78f, 0.86f, 0.55f);

            var sb = barGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handle;
            sb.targetGraphic = handleImage;
            sb.size = 0.25f;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        /// <summary>把每个成品要经过的工厂与工序拼成一份清单。</summary>
        public static string BuildContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<color=#5AA9E6>通用规则</color>：海里捞上来的是<b>生鱼</b>，先送预处理厂剖好，之后每一步在对应工厂完成；");
            sb.AppendLine("每经过一道工序价格按倍率累乘，品质在最终出售时才算。同一个中间品可以走不同路线，注意原料分配。");
            sb.AppendLine();

            for (int g = 0; g < GroupNames.Length; g++)
            {
                sb.AppendLine($"<color=#E6C55A>【{GroupNames[g]}】</color>");
                FishStage[] stages = GroupStages[g];
                for (int k = 0; k < stages.Length; k++)
                {
                    Recipe recipe = RecipeDatabase.FindByStage(stages[k]);
                    if (recipe == null) continue;

                    string name = string.IsNullOrEmpty(recipe.DisplayName) ? recipe.Id : recipe.DisplayName;
                    sb.Append($"· <color=#6FD08C>{name}</color>：");
                    sb.Append(FormatChain(RecipeDatabase.ChainOf(stages[k])));
                    if (recipe.AuxItems != null && recipe.AuxItems.Length > 0)
                        sb.Append($"　<color=#E6C55A>辅料</color>：{FormatAux(recipe)}");
                    if (recipe.IsBatch)
                        sb.Append($"　<color=#E6C55A>批次：每 {recipe.BatchTurns} 回合发一批</color>");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            sb.AppendLine("<color=#E6C55A>【副产物利用】</color>");
            sb.AppendLine("· <color=#6FD08C>鱼油</color>：边角料 → 萃取精炼厂（鱼油）");
            sb.AppendLine("· <color=#6FD08C>鱼粉</color>：边角料 → 萃取精炼厂（鱼粉）");
            sb.AppendLine("· <color=#6FD08C>发酵液</color>：边角料 → 发酵熟成厂（发酵液）");
            return sb.ToString();
        }

        static string FormatChain(List<FishStage> chain)
        {
            var parts = new List<string> { "生鱼" };
            for (int k = 0; k < chain.Count; k++)
            {
                Recipe recipe = RecipeDatabase.FindByStage(chain[k]);
                FactoryType? type = recipe != null ? RecipeDatabase.TypeOfRecipe(recipe) : null;
                string factory = type.HasValue ? RecipeDatabase.TypeName(type.Value) : "工厂";
                parts.Add($"{factory}·{ItemDatabase.StageWord(chain[k])}");
            }
            return string.Join(" → ", parts);
        }

        static string FormatAux(Recipe recipe)
        {
            var parts = new List<string>();
            for (int k = 0; k < recipe.AuxItems.Length; k++)
            {
                ItemDef def = ItemDatabase.Get(recipe.AuxItems[k]);
                parts.Add($"{def.DisplayName} {recipe.AuxPerUnit[k]:0.##}{def.UnitText}");
            }
            return string.Join("、", parts);
        }

        Text MakeText(RectTransform parent, string text, int size, FontStyle style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.text = text;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
