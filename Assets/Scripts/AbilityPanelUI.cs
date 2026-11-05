using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 异能面板内容：神眼 / 动物交流 / 威慑三张卡片，点卡片花异能点升级。
    /// 动物交流有两个分支按钮（海龟 / 鲸鱼），两者只能携带其一：
    /// 切换分支时把另一个分支已投入的点数退回来并重置。
    /// </summary>
    public class AbilityPanelUI : MonoBehaviour
    {
        [Header("布局")]
        [SerializeField] Vector2 cardSize = new Vector2(920f, 132f);
        [SerializeField] float firstCardY = 20f;
        [SerializeField] float cardGap = 140f;

        Font _font;

        void OnEnable()
        {
            EnsureFont();
            Rebuild();
        }

        void EnsureFont()
        {
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei", "SimHei", "Arial" }, 26);
        }

        void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
                if (c.name.StartsWith("Ability")) Destroy(c.gameObject);
            }

            MakeCard(0, "神眼", $"Lv.{AbilityData.EyeLevel} / {AbilityData.EyeMax}",
                     "提升潜望距离，看得更深。" + AbilityData.Describe("eye"),
                     AbilityData.EyeLevel < AbilityData.EyeMax, UpgradeEye);

            string animalDesc = $"海龟 Lv.{Mathf.Max(0, AbilityData.TurtleLevel)}：{AbilityData.Describe("turtle")}\n" +
                                (AbilityData.WhaleUnlocked
                                    ? $"鲸鱼 Lv.{AbilityData.WhaleLevel}：{AbilityData.Describe("whale")}"
                                    : "鲸鱼：未解锁（与海龟二选一，切换退点）");
            string animalSub = $"海龟 {AbilityData.TurtleLevel}/{AbilityData.TurtleMax} · 鲸鱼 " +
                               (AbilityData.WhaleUnlocked ? AbilityData.WhaleLevel + "/" + AbilityData.WhaleMax : "—");
            var animalCard = MakeCard(1, "动物交流", animalSub, animalDesc, false, null);
            // 按钮放到卡片最右侧；选中那条加勾号，避免绿/灰被读成「可用/禁用」
            MakeBranchButton(animalCard, "升海龟", 340f, 6f, UpgradeTurtle,
                             AbilityData.TurtleLevel < AbilityData.TurtleMax,
                             !AbilityData.WhaleUnlocked);
            MakeBranchButton(animalCard, "升鲸鱼", 340f, -40f, UpgradeWhale,
                             AbilityData.WhaleLevel < AbilityData.WhaleMax,
                             AbilityData.WhaleUnlocked);

            MakeCard(2, "威慑", $"Lv.{AbilityData.IntimidateLevel} / {AbilityData.IntimidateMax}",
                     "钓鱼下沉阶段，鱼不敢咬钩。" + AbilityData.Describe("intimidate"),
                     AbilityData.IntimidateLevel < AbilityData.IntimidateMax, UpgradeIntimidate);

            MakePointsLine();
        }

        void MakePointsLine()
        {
            // 半透明底条：这一行原本骑在遮阳篷条纹上看不清
            var bg = new GameObject("AbilityPointsBg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(transform, false);
            var brt = bg.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, 108f);
            brt.sizeDelta = new Vector2(320f, 40f);
            var bimg = bg.GetComponent<Image>();
            bimg.color = new Color(0.16f, 0.11f, 0.08f, 0.72f);
            bimg.raycastTarget = false;

            var go = new GameObject("AbilityPoints", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 108f);
            rt.sizeDelta = new Vector2(320f, 40f);

            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = 26;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.98f, 0.93f, 0.80f);
            t.raycastTarget = false;
            t.text = "待分配异能点　" + PlayerProgress.SkillPoints;
        }

        // ---------------- 卡片 ----------------

        static ColorBlock PressedColors()
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = Color.white;
            c.highlightedColor = Color.white;
            c.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            c.selectedColor = Color.white;
            c.fadeDuration = 0.06f;
            return c;
        }

        GameObject MakeCard(int index, string title, string subtitle, string desc,
                            bool clickable, System.Action onClick)
        {
            float y = firstCardY - index * cardGap;

            var card = new GameObject("AbilityCard" + index, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(transform, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = cardSize;

            var bg = card.GetComponent<Image>();
            // 可升级时底色更亮一点，给玩家"能点"的暗示
            bg.color = clickable ? new Color(1f, 0.97f, 0.90f, 0.85f)
                                 : new Color(1f, 0.97f, 0.90f, 0.55f);
            bg.raycastTarget = clickable;

            if (clickable && onClick != null)
            {
                var btn = card.AddComponent<Button>();
                btn.transition = Selectable.Transition.ColorTint;
                btn.targetGraphic = bg;
                btn.colors = PressedColors();
                btn.onClick.AddListener(() => onClick());
                card.AddComponent<Shenyan.UiPressScale>();
            }

            MakeText(card.transform, "Title", title, 32, new Color(0.22f, 0.13f, 0.08f),
                     TextAnchor.MiddleLeft, new Vector2(-cardSize.x * 0.5f + 44f, cardSize.y * 0.5f - 34f),
                     new Vector2(420f, 44f), new Vector2(0f, 0.5f));

            MakeText(card.transform, "Sub", subtitle, 22, new Color(0.42f, 0.30f, 0.20f),
                     TextAnchor.MiddleRight, new Vector2(cardSize.x * 0.5f - 30f, cardSize.y * 0.5f - 34f),
                     new Vector2(560f, 34f), new Vector2(1f, 0.5f));

            MakeText(card.transform, "Desc", desc, 22, new Color(0.30f, 0.22f, 0.16f),
                     TextAnchor.UpperLeft, new Vector2(-cardSize.x * 0.5f + 44f, cardSize.y * 0.5f - 76f),
                     new Vector2(cardSize.x - 260f, 60f), new Vector2(0f, 1f));

            return card;
        }

        /// <summary>动物交流卡片右侧的分支按钮。active = 当前走的这条分支（高亮）。</summary>
        void MakeBranchButton(GameObject card, string label, float x, float y,
                              System.Action onClick, bool enabled, bool active)
        {
            var go = new GameObject("Ability" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(card.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(150f, 44f);

            var img = go.GetComponent<Image>();
            if (!enabled) img.color = new Color(0.45f, 0.42f, 0.38f, 0.55f);
            else if (active) img.color = new Color(0.26f, 0.40f, 0.24f, 0.92f);   // 当前分支：绿
            else img.color = new Color(0.24f, 0.15f, 0.10f, 0.9f);

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            btn.colors = PressedColors();
            btn.interactable = enabled;
            go.AddComponent<Shenyan.UiPressScale>();
            btn.onClick.AddListener(() => onClick());

            MakeText(go.transform, "Label", active ? "✓ " + label : label, 20, new Color(0.98f, 0.93f, 0.80f),
                     TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150f, 44f), new Vector2(0.5f, 0.5f));
        }

        void MakeText(Transform parent, string name, string content, int size, Color color,
                      TextAnchor align, Vector2 pos, Vector2 size2, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size2;

            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
        }

        // ---------------- 升级 ----------------

        void UpgradeEye()
        {
            GameAudio.Ensure().PlayUiClick();
            if (!SpendPoint()) return;
            AbilityData.EyeLevel++;
            SaveSystem.Save();
            Rebuild();
        }

        void UpgradeIntimidate()
        {
            GameAudio.Ensure().PlayUiClick();
            if (!SpendPoint()) return;
            AbilityData.IntimidateLevel++;
            SaveSystem.Save();
            Rebuild();
        }

        void UpgradeTurtle()
        {
            GameAudio.Ensure().PlayUiClick();
            if (AbilityData.TurtleLevel >= AbilityData.TurtleMax) return;
            if (PlayerProgress.SkillPoints <= 0) return;

            // 互斥：已经选了鲸鱼就先退点并清空
            if (AbilityData.WhaleUnlocked)
            {
                int refund = AbilityData.WhaleLevel + 1;
                PlayerProgress.SkillPoints += refund;
                AbilityData.WhaleLevel = -1;
                Debug.Log("[异能] 改走海龟分支，鲸鱼已退还 " + refund + " 点");
            }

            PlayerProgress.SkillPoints--;
            AbilityData.TurtleLevel++;
            SaveSystem.Save();
            Rebuild();
        }

        void UpgradeWhale()
        {
            GameAudio.Ensure().PlayUiClick();
            if (AbilityData.WhaleLevel >= AbilityData.WhaleMax) return;
            if (PlayerProgress.SkillPoints <= 0) return;

            if (AbilityData.WhaleUnlocked)
            {
                PlayerProgress.SkillPoints--;
                AbilityData.WhaleLevel++;
            }
            else
            {
                // 首次解锁鲸鱼：退还海龟已投入的点数，海龟回到默认 0 级
                if (AbilityData.TurtleLevel > 0)
                {
                    PlayerProgress.SkillPoints += AbilityData.TurtleLevel;
                    Debug.Log("[异能] 改走鲸鱼分支，海龟已退还 " + AbilityData.TurtleLevel + " 点");
                    AbilityData.TurtleLevel = 0;
                }
                PlayerProgress.SkillPoints--;
                AbilityData.WhaleLevel = 0;
            }
            SaveSystem.Save();
            Rebuild();
        }

        bool SpendPoint()
        {
            if (PlayerProgress.SkillPoints <= 0)
            {
                Debug.Log("[异能] 没有可分配的异能点");
                return false;
            }
            PlayerProgress.SkillPoints--;
            return true;
        }
    }
}
