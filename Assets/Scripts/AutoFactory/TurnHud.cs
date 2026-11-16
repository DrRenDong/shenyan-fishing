using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 工厂 HUD：左上角只显示信息（回合 / 金币 / 订单），操作按钮全部挪到右下角（图片按钮，右下角最右是「下一回合」）。
    /// </summary>
    public class TurnHud : MonoBehaviour
    {
        [Header("外观")]
        public float width = 560f;          // 信息面板宽
        public float height = 200f;         // 信息面板高（回合/时间/金币/订单/船舱/贵重品）
        public Color backgroundColor = new Color(0.055f, 0.07f, 0.09f, 0.92f);
        public Color accentColor = new Color(0.36f, 0.78f, 0.86f, 1f);

        [Header("按钮尺寸")]
        public float buttonHeight = 84f;
        public float buttonGap = 8f;

        const string ButtonFolder = "Image/UI/工厂按钮";

        Font _font;
        Text _turnText;
        Text _timeText;
        Text _questText;
        PlayerBoatVehicle _boat;

        RectTransform _bar;
        readonly List<RectTransform> _buttonRects = new List<RectTransform>();
        GameObject _moveButton;
        bool _lastMoveVisible;

        void Awake()
        {
            BuildUI();
        }

        public void BuildUI()
        {
            Transform existing = transform.Find("TurnCanvas");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);

            var canvasGo = new GameObject("TurnCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            // 功能键压在详情面板 / 码头面板 / 建造面板等面板之下（面板是 5000+），
            // 但仍要高于场景精灵（远处地块最大约 4022）。
            canvas.sortingOrder = 4990;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            BuildInfoPanel(canvasGo.transform);
            BuildButtonBar(canvasGo.transform);

            Refresh();
        }

        // ---------------- 左上角：信息 ----------------

        void BuildInfoPanel(Transform parent)
        {
            var panelGo = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image));
            var panel = panelGo.GetComponent<RectTransform>();
            panel.SetParent(parent, false);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(width, height);
            panel.anchoredPosition = new Vector2(24f, -24f);
            panelGo.GetComponent<Image>().color = backgroundColor;

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(panel, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accentGo.GetComponent<Image>().color = accentColor;

            _turnText = MakeText(panel, "回合", 24, FontStyle.Bold, new Color(0.96f, 0.97f, 0.98f));
            _turnText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _turnText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _turnText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _turnText.rectTransform.sizeDelta = new Vector2(-32f, 32f);
            _turnText.rectTransform.anchoredPosition = new Vector2(16f, -12f);

            _timeText = MakeText(panel, "时间", 18, FontStyle.Normal, new Color(0.78f, 0.85f, 0.90f));
            _timeText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _timeText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _timeText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _timeText.rectTransform.sizeDelta = new Vector2(-32f, 26f);
            _timeText.rectTransform.anchoredPosition = new Vector2(16f, -44f);

            _questText = MakeText(panel, "", 16, FontStyle.Normal, new Color(0.80f, 0.92f, 0.82f));
            _questText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _questText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _questText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _questText.rectTransform.sizeDelta = new Vector2(-32f, 116f);
            _questText.rectTransform.anchoredPosition = new Vector2(16f, -72f);
            _questText.lineSpacing = 1.05f;
        }

        // ---------------- 右下角：操作按钮 ----------------

        void BuildButtonBar(Transform parent)
        {
            var barGo = new GameObject("ButtonBar", typeof(RectTransform));
            _bar = barGo.GetComponent<RectTransform>();
            _bar.SetParent(parent, false);
            _bar.anchorMin = new Vector2(1f, 0f);
            _bar.anchorMax = new Vector2(1f, 0f);
            _bar.pivot = new Vector2(1f, 0f);
            _bar.sizeDelta = Vector2.zero;
            _bar.anchoredPosition = new Vector2(-32f, 32f);

            _buttonRects.Clear();

            // 从右往左：下一回合 → 退出 → 定位 → 电力 → 工程 → 订单 → 捕鱼 → 航线 → 配方 → 建造 → 移地（可隐藏）
            MakeImageButton("下一回合", "btn_next", 287f / 194f,
                () => { if (TurnManager.Instance != null) TurnManager.Instance.EndTurn(); });

            MakeImageButton("退出", "btn_exit", 182f / 214f, () =>
            {
                SaveSystem.Save();
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            });

            MakeImageButton("定位", "btn_locate", 182f / 214f, () =>
            {
                var ctl = FindObjectOfType<MapCameraController>();
                if (ctl != null) ctl.FocusBoat();
            });

            MakeImageButton("电力", "btn_power", 182f / 214f, () =>
            {
                if (PowerGridView.Instance != null) PowerGridView.Instance.Toggle();
            });

            MakeImageButton("工程", "btn_engineer", 182f / 214f, () =>
            {
                if (EngineeringView.Instance != null) EngineeringView.Instance.Toggle();
            });

            MakeImageButton("订单", "btn_order", 182f / 214f, () =>
            {
                if (BossOrderPanel.Instance != null) BossOrderPanel.Instance.Toggle();
            });

            MakeImageButton("捕鱼", "btn_fish", 182f / 214f, () =>
            {
                if (FishingSession.Instance != null) FishingSession.Instance.Enter();
            });

            MakeImageButton("航线", "btn_route", 182f / 214f, () =>
            {
                var picker = FindObjectOfType<TilePicker>();
                if (picker != null) picker.SetBoatPathMode(true);
            });

            MakeImageButton("配方", "btn_recipe", 182f / 214f, () =>
            {
                if (RecipeBookPanel.Instance != null) RecipeBookPanel.Instance.Toggle();
            });

            MakeImageButton("建造", "btn_build", 182f / 214f, () =>
            {
                var panel = FindObjectOfType<BuildPanel>();
                if (panel == null) return;
                if (panel.IsShown) panel.Hide();
                else panel.Open();
            });

            _moveButton = MakeImageButton("移地", "btn_move", 182f / 214f, () =>
            {
                if (GrassMoveController.Instance != null) GrassMoveController.Instance.Toggle();
            });
            _moveButton.SetActive(AbilityData.CanMoveGrass);
            _lastMoveVisible = AbilityData.CanMoveGrass;

            LayoutButtons();
        }

        GameObject MakeImageButton(string name, string spriteName, float aspect, System.Action onClick)
        {
            float h = buttonHeight;
            float w = h * aspect;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(UiPressScale));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_bar, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(w, h);

            var img = go.GetComponent<Image>();
            img.sprite = Resources.Load<Sprite>($"{ButtonFolder}/{spriteName}");
            img.preserveAspect = true;
            img.raycastTarget = true;
            if (img.sprite == null) Debug.LogWarning($"[HUD] 缺少按钮素材 {ButtonFolder}/{spriteName}");

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());

            _buttonRects.Add(rect);
            return go;
        }

        /// <summary>从右往左排：最右是「下一回合」，被隐藏的按钮（移地未解锁）不占位置。</summary>
        void LayoutButtons()
        {
            float x = 0f;
            for (int i = 0; i < _buttonRects.Count; i++)
            {
                var rect = _buttonRects[i];
                if (rect == null || !rect.gameObject.activeSelf) continue;
                rect.anchoredPosition = new Vector2(-x, 0f);
                x += rect.sizeDelta.x + buttonGap;
            }
        }

        // ---------------- 刷新 ----------------

        void Refresh()
        {
            int turn = TurnManager.Instance != null ? TurnManager.Instance.Turn : 1;
            _turnText.text = $"第 {turn} 日";
            _timeText.text = $"回合 {turn}";

            if (_questText == null) return;

            var sb = new System.Text.StringBuilder();
            sb.Append($"<color=#F0D48A>金币</color> {PlayerProgress.Gold:N0}");

            var boss = BossOrderSystem.Instance;
            if (boss != null)
            {
                if (boss.AllDone) sb.Append("\n<color=#8FE08A>主线：全部完成</color>");
                else if (boss.Current != null) sb.Append($"\n<color=#9ED8FF>主线</color> {boss.Current.title}");
            }

            var rush = BossRushSystem.Instance;
            if (rush != null)
            {
                if (rush.current != null)
                    sb.Append($"\n<color=#FFC46B>限时</color> {rush.current.title}");
                else
                    sb.Append("\n<color=#8A98A6>限时：暂无</color>");
            }

            // 渔船船舱 + 贵重品数量
            if (_boat == null) _boat = FindObjectOfType<PlayerBoatVehicle>();
            if (_boat != null)
                sb.Append($"\n<color=#9ED8FF>船舱</color> {_boat.CargoWeight:0.0} / {_boat.CargoCapacity:0} KG");

            sb.Append($"\n<color=#D8C8A0>贵重品</color> 南海黑珍珠 ×{PlayerProgress.TreasureCount(ItemDatabase.SouthSeaPearl)}" +
                      $"　美乐珠 ×{PlayerProgress.TreasureCount(ItemDatabase.MeloPearl)}" +
                      $"　龙涎香 ×{PlayerProgress.TreasureCount(ItemDatabase.Ambergris)}" +
                      $"　国宝 ×{PlayerProgress.TreasureCount(ItemDatabase.NationalTreasure)}");

            _questText.text = sb.ToString();
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
            return t;
        }

        void Update()
        {
            Refresh();

            // 移地：地形改造 Lv3 之后才出现
            bool showMove = AbilityData.CanMoveGrass;
            if (showMove != _lastMoveVisible)
            {
                _lastMoveVisible = showMove;
                if (_moveButton != null) _moveButton.SetActive(showMove);
                LayoutButtons();
            }
        }
    }
}
