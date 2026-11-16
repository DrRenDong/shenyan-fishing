using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 异能面板内容：神眼 / 动物交流 / 威慑 / 神眼·远眺 / 动物交流·优质 / 地形改造。
    /// 前三条花异能点升级；后三条消耗沉船宝物（美乐珠 / 龙涎香 / 南海黑珍珠）。
    /// 内容放在可滚动区域里，卡片多了也不会超出面板。
    /// </summary>
    public class AbilityPanelUI : MonoBehaviour
    {
        [Header("布局")]
        [SerializeField] Vector2 cardSize = new Vector2(920f, 54f);
        [SerializeField] float cardGap = 59.6f;

        // 第一个条目在 Y=-159、最后一个（第 6 张）在 Y=-457：整体收紧成紧凑列表
        const float FirstCardOffset = 159f;

        Font _font;
        RectTransform _content;
        ScrollRect _scroll;

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

        /// <summary>卡片太多，放一个纵向滚动区（只建一次）。</summary>
        void EnsureScroll()
        {
            if (_content != null) return;

            var vpGo = new GameObject("TalentViewport", typeof(RectTransform), typeof(RectMask2D));
            vpGo.transform.SetParent(transform, false);
            var vp = vpGo.GetComponent<RectTransform>();
            // 底部再放开一点：紧凑列表最后一张在 Y=-457，加上卡高刚好落在视口里
            vp.anchorMin = new Vector2(0.03f, 0.08f);
            vp.anchorMax = new Vector2(0.965f, 0.72f);
            vp.offsetMin = Vector2.zero;
            vp.offsetMax = Vector2.zero;

            var contentGo = new GameObject("TalentContent", typeof(RectTransform));
            contentGo.transform.SetParent(vpGo.transform, false);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0.5f, 1f);
            _content.anchorMax = new Vector2(0.5f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 800f);

            _scroll = vpGo.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 42f;
            _scroll.viewport = vp;
            _scroll.content = _content;

            // 右侧滚动条（挂在 Viewport 外面，免得被 RectMask2D 裁掉）
            var barGo = new GameObject("TalentScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var bar = barGo.GetComponent<RectTransform>();
            bar.SetParent(transform, false);
            bar.anchorMin = new Vector2(0.965f, 0.08f);
            bar.anchorMax = new Vector2(0.965f, 0.72f);
            bar.offsetMin = new Vector2(4f, 0f);
            bar.offsetMax = new Vector2(18f, 0f);
            barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handle = handleGo.GetComponent<RectTransform>();
            handle.SetParent(bar, false);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.sizeDelta = Vector2.zero;
            handleGo.GetComponent<Image>().color = new Color(0.36f, 0.78f, 0.86f, 0.72f);

            var sb = barGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handle;
            sb.targetGraphic = handleGo.GetComponent<Image>();
            _scroll.verticalScrollbar = sb;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        void Rebuild()
        {
            EnsureScroll();

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var c = _content.GetChild(i);
                if (!c.name.StartsWith("Ability")) continue;
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
                if (!c.name.StartsWith("AbilityPoints")) continue;
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            int index = 0;

            MakeCard(index++, "神眼", $"Lv.{AbilityData.EyeLevel} / {AbilityData.EyeMax}",
                     "提升潜望距离，看得更深。" + AbilityData.Describe("eye"),
                     AbilityData.EyeLevel < AbilityData.EyeMax, UpgradeEye);

            string animalDesc = AbilityData.Describe("turtle") + " · 鲸鱼：" +
                                (AbilityData.WhaleUnlocked
                                    ? AbilityData.Describe("whale")
                                    : "未解锁（与海龟二选一，切换退点）");
            string animalSub = $"海龟 {AbilityData.TurtleLevel}/{AbilityData.TurtleMax} · 鲸鱼 " +
                               (AbilityData.WhaleUnlocked ? AbilityData.WhaleLevel + "/" + AbilityData.WhaleMax : "—");
            var animalCard = MakeCard(index++, "动物交流", animalSub, animalDesc, false, null, 240f);
            MakeBranchButton(animalCard, "升海龟", 295f, -12f, UpgradeTurtle,
                             AbilityData.TurtleLevel < AbilityData.TurtleMax,
                             !AbilityData.WhaleUnlocked);
            MakeBranchButton(animalCard, "升鲸鱼", 405f, -12f, UpgradeWhale,
                             AbilityData.WhaleLevel < AbilityData.WhaleMax,
                             AbilityData.WhaleUnlocked);

            MakeCard(index++, "威慑", $"Lv.{AbilityData.IntimidateLevel} / {AbilityData.IntimidateMax}",
                     "钓鱼下沉阶段，鱼不敢咬钩。" + AbilityData.Describe("intimidate"),
                     AbilityData.IntimidateLevel < AbilityData.IntimidateMax, UpgradeIntimidate);

            // ---- 沉船宝物解锁的三条 ----

            int meloHave = PlayerProgress.TreasureCount(ItemDatabase.MeloPearl);
            int meloNeed = AbilityData.EyeFarLevel < AbilityData.EyeFarMax
                ? AbilityData.EyeFarCosts[AbilityData.EyeFarLevel] : 0;
            MakeCard(index++, "神眼 · 远眺", $"Lv.{AbilityData.EyeFarLevel} / {AbilityData.EyeFarMax}",
                     $"视野半径 +{AbilityData.BoatViewBonus} 格 · " +
                     (AbilityData.EyeFarLevel >= AbilityData.EyeFarMax
                        ? "已满级"
                        : $"升级需美乐珠 ×{meloNeed}（持有 {meloHave}）"),
                     AbilityData.EyeFarLevel < AbilityData.EyeFarMax && meloHave >= meloNeed,
                     UpgradeEyeFar);

            int amberHave = PlayerProgress.TreasureCount(ItemDatabase.Ambergris);
            int amberNeed = AbilityData.QualityLevel < AbilityData.QualityMax
                ? AbilityData.QualityCosts[AbilityData.QualityLevel] : 0;
            MakeCard(index++, "动物交流 · 优质", $"Lv.{AbilityData.QualityLevel} / {AbilityData.QualityMax}",
                     $"捕鱼品质 {AbilityData.CatchQuality * 100f:0}% · " +
                     (AbilityData.QualityLevel >= AbilityData.QualityMax
                        ? "已满级"
                        : $"升级需龙涎香 ×{amberNeed}（持有 {amberHave}）"),
                     AbilityData.QualityLevel < AbilityData.QualityMax && amberHave >= amberNeed,
                     UpgradeQuality);

            int pearlHave = PlayerProgress.TreasureCount(ItemDatabase.SouthSeaPearl);
            int pearlNeed = AbilityData.TerrainLevel < AbilityData.TerrainMax
                ? AbilityData.TerrainCosts[AbilityData.TerrainLevel] : 0;
            string terrainEffect;
            if (AbilityData.TerrainLevel <= 0) terrainEffect = "Lv1 可拆附属物";
            else if (AbilityData.TerrainLevel == 1) terrainEffect = "Lv2 拆建筑全额退款";
            else if (AbilityData.TerrainLevel == 2) terrainEffect = "Lv3 可移动草地";
            else terrainEffect = "已能移草地、全额退款";
            MakeCard(index++, "地形改造", $"Lv.{AbilityData.TerrainLevel} / {AbilityData.TerrainMax}",
                     $"{terrainEffect} · " +
                     (AbilityData.TerrainLevel >= AbilityData.TerrainMax
                        ? "已满级"
                        : $"升级需南海黑珍珠 ×{pearlNeed}（持有 {pearlHave}）"),
                     AbilityData.TerrainLevel < AbilityData.TerrainMax && pearlHave >= pearlNeed,
                     UpgradeTerrain);

            _content.sizeDelta = new Vector2(0f, index * cardGap + FirstCardOffset);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;

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
            brt.anchoredPosition = new Vector2(0f, 330f);
            brt.sizeDelta = new Vector2(320f, 40f);
            var bimg = bg.GetComponent<Image>();
            bimg.color = new Color(0.16f, 0.11f, 0.08f, 0.72f);
            bimg.raycastTarget = false;

            var go = new GameObject("AbilityPoints", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 330f);
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
                            bool clickable, System.Action onClick, float descTrim = 0f)
        {
            var card = new GameObject("AbilityCard" + index, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_content, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(FirstCardOffset + index * cardGap));
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

            // 紧凑两行：上排 标题 + 等级，下排 说明（单行，超出就截断）
            MakeText(card.transform, "Title", title, 22, new Color(0.22f, 0.13f, 0.08f),
                     TextAnchor.MiddleLeft, new Vector2(-cardSize.x * 0.5f + 40f, 12f),
                     new Vector2(420f, 26f), new Vector2(0f, 0.5f));

            MakeText(card.transform, "Sub", subtitle, 16, new Color(0.42f, 0.30f, 0.20f),
                     TextAnchor.MiddleRight, new Vector2(cardSize.x * 0.5f - 26f, 12f),
                     new Vector2(420f, 22f), new Vector2(1f, 0.5f));

            MakeText(card.transform, "Desc", desc, 15, new Color(0.30f, 0.22f, 0.16f),
                     TextAnchor.MiddleLeft, new Vector2(-cardSize.x * 0.5f + 40f, -13f),
                     new Vector2(cardSize.x - 84f - descTrim, 21f), new Vector2(0f, 0.5f), true);

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
            rt.sizeDelta = new Vector2(100f, 24f);

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

            MakeText(go.transform, "Label", active ? "✓ " + label : label, 14, new Color(0.98f, 0.93f, 0.80f),
                     TextAnchor.MiddleCenter, Vector2.zero, new Vector2(100f, 24f), new Vector2(0.5f, 0.5f));
        }

        void MakeText(Transform parent, string name, string content, int size, Color color,
                      TextAnchor align, Vector2 pos, Vector2 size2, Vector2 pivot, bool truncate = false)
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
            t.verticalOverflow = truncate ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
        }

        // ---------------- 升级（异能点） ----------------

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

        // ---------------- 升级（沉船宝物） ----------------

        void UpgradeEyeFar()
        {
            GameAudio.Ensure().PlayUiClick();
            if (AbilityData.EyeFarLevel >= AbilityData.EyeFarMax) return;
            int need = AbilityData.EyeFarCosts[AbilityData.EyeFarLevel];
            if (!PlayerProgress.TrySpendTreasure(ItemDatabase.MeloPearl, need))
            {
                Debug.Log($"[异能] 美乐珠不足（需要 {need}）");
                return;
            }
            AbilityData.EyeFarLevel++;
            SaveSystem.Save();
            Rebuild();
        }

        void UpgradeQuality()
        {
            GameAudio.Ensure().PlayUiClick();
            if (AbilityData.QualityLevel >= AbilityData.QualityMax) return;
            int need = AbilityData.QualityCosts[AbilityData.QualityLevel];
            if (!PlayerProgress.TrySpendTreasure(ItemDatabase.Ambergris, need))
            {
                Debug.Log($"[异能] 龙涎香不足（需要 {need}）");
                return;
            }
            AbilityData.QualityLevel++;
            SaveSystem.Save();
            Rebuild();
        }

        void UpgradeTerrain()
        {
            GameAudio.Ensure().PlayUiClick();
            if (AbilityData.TerrainLevel >= AbilityData.TerrainMax) return;
            int need = AbilityData.TerrainCosts[AbilityData.TerrainLevel];
            if (!PlayerProgress.TrySpendTreasure(ItemDatabase.SouthSeaPearl, need))
            {
                Debug.Log($"[异能] 南海黑珍珠不足（需要 {need}）");
                return;
            }
            AbilityData.TerrainLevel++;
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
