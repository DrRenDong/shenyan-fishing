using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 渔具商店内容：四行装备升级（各 10 级）+ 鱼雷 / 饵料两行道具。
    /// 行不做卡片背景，紧凑排布；整行可点 = 购买。
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        [Header("布局")]
        [SerializeField] float firstRowY = -4f;
        [SerializeField] float rowGap = 57f;
        [SerializeField] Vector2 rowSize = new Vector2(880f, 54f);
        [SerializeField] Text hintText;          // 旧版的“进货中”提示，重建时隐藏

        [Header("海神号")]
        public int shipPrice = 20000;
        [SerializeField] ShipComicUI shipComic;

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
                    new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        }

        void Rebuild()
        {
            EnsureScroll();

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var c = _content.GetChild(i);
                if (c.name.StartsWith("ShopRow")) Destroy(c.gameObject);
            }

            if (hintText != null) hintText.gameObject.SetActive(false);

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                int level = ShopData.LevelOf(idx);
                bool maxed = level >= ShopData.MaxLevel;
                string text = $"{ShopData.TitleOf(idx)}　Lv.{level}/{ShopData.MaxLevel}　　{ShopData.Describe(idx)}";
                string price = maxed ? "已满级" : ShopData.UpgradeCost(idx).ToString("N0") + " 金";
                MakeRow(i, text, price, !maxed, () => Buy(idx));
            }

            // 鱼雷
            MakeRow(4, $"鱼雷　×{ShopData.Torpedoes}　　炸一片鱼直接上船（卖价减半）",
                    ShopData.TorpedoPrice.ToString("N0") + " 金", true, BuyTorpedo);
            // 饵料
            MakeRow(5, $"饵料　×{ShopData.Baits}　　打窝聚鱼（一竿消耗一个）",
                    ShopData.BaitPrice.ToString("N0") + " 金", true, BuyBait);

            // 海神号：王老板联系大厂造的远洋捕捞船，一次性购买
            bool owned = SaveSystem.ShipPurchased;
            MakeRow(6,
                    owned
                        ? "海神号　已停泊在渔村码头（回渔村点海神号进工厂）"
                        : "海神号　王老板联系大厂造的远洋捕捞船，买下就能开工厂",
                    owned ? "已拥有" : shipPrice.ToString("N0") + " 金",
                    !owned && PlayerProgress.Gold >= shipPrice,
                    BuyShip);

            // 7 行内容刚好铺满纸面（纸面在面板里是 y +73 ~ -337）
            _content.sizeDelta = new Vector2(rowSize.x,
                Mathf.Max(0f, -firstRowY) + 6f * rowGap + rowSize.y + 8f);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>可滚动区域 + 右侧滚动条（行数多了也不会压出面板）。</summary>
        void EnsureScroll()
        {
            if (_content != null) return;

            var vpGo = new GameObject("ShopViewport", typeof(RectTransform), typeof(RectMask2D));
            vpGo.transform.SetParent(transform, false);
            var vp = vpGo.GetComponent<RectTransform>();
            vp.anchorMin = vp.anchorMax = new Vector2(0.5f, 0.5f);
            vp.pivot = new Vector2(0.5f, 0.5f);
            // 视口对齐商店背景的纸面区域（面板 1200×800 时纸面 ≈ x -481..464 / y +73..-337），
            // 否则列表会顶到上面的遮阳篷、下面留一大块空白。
            vp.sizeDelta = new Vector2(920f, 408f);
            vp.anchoredPosition = new Vector2(0f, -132f);

            var contentGo = new GameObject("ShopContent", typeof(RectTransform));
            contentGo.transform.SetParent(vpGo.transform, false);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0.5f, 1f);
            _content.anchorMax = new Vector2(0.5f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(880f, 600f);

            _scroll = vpGo.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 42f;
            _scroll.viewport = vp;
            _scroll.content = _content;

            // 右侧滚动条（放在 Viewport 外面，免得被 RectMask2D 裁掉）
            var barGo = new GameObject("ShopScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var bar = barGo.GetComponent<RectTransform>();
            bar.SetParent(transform, false);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.sizeDelta = new Vector2(12f, 408f);
            bar.anchoredPosition = new Vector2(444f, -132f);
            barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handle = handleGo.GetComponent<RectTransform>();
            handle.SetParent(bar, false);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.sizeDelta = Vector2.zero;
            handleGo.GetComponent<Image>().color = new Color(0.55f, 0.40f, 0.22f, 0.85f);

            var sb = barGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handle;
            sb.targetGraphic = handleGo.GetComponent<Image>();
            _scroll.verticalScrollbar = sb;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        static ColorBlock PressedColors()
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1f, 1f, 1f, 1f);
            c.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            c.selectedColor = Color.white;
            c.fadeDuration = 0.06f;
            return c;
        }

        void MakeRow(int index, string label, string price, bool canBuy, System.Action onClick)
        {
            float y = firstRowY - index * rowGap;

            var row = new GameObject("ShopRow" + index, typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(_content, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = rowSize;

            var img = row.GetComponent<Image>();
            img.color = canBuy ? new Color(1f, 0.97f, 0.90f, 0.55f)
                               : new Color(1f, 0.97f, 0.90f, 0.28f);

            var btn = row.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;   // 按下变暗，有反馈
            btn.targetGraphic = img;
            btn.colors = PressedColors();
            row.AddComponent<Shenyan.UiPressScale>();          // 按下轻微缩小
            btn.interactable = canBuy;
            btn.onClick.AddListener(() => onClick());

            MakeText(row.transform, "Label", label, 24, new Color(0.24f, 0.15f, 0.09f),
                     TextAnchor.MiddleLeft, new Vector2(-rowSize.x * 0.5f + 24f, 0f),
                     new Vector2(rowSize.x - 300f, rowSize.y), new Vector2(0f, 0.5f));

            MakeText(row.transform, "Price", price, 22,
                     canBuy ? new Color(0.55f, 0.26f, 0.10f) : new Color(0.45f, 0.42f, 0.38f),
                     TextAnchor.MiddleRight, new Vector2(rowSize.x * 0.5f - 24f, 0f),
                     new Vector2(260f, rowSize.y), new Vector2(1f, 0.5f));
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
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
        }

        void Buy(int index)
        {
            GameAudio.Ensure().PlayUiClick();
            if (ShopData.TryBuyUpgrade(index)) Rebuild();
            else Debug.Log("[商店] 金币不够或已满级");
        }

        void BuyTorpedo()
        {
            GameAudio.Ensure().PlayUiClick();
            if (ShopData.TryBuyTorpedo()) Rebuild();
            else Debug.Log("[商店] 金币不够买鱼雷");
        }

        void BuyBait()
        {
            GameAudio.Ensure().PlayUiClick();
            if (ShopData.TryBuyBait()) Rebuild();
            else Debug.Log("[商店] 金币不够买饵料");
        }

        /// <summary>买海神号：扣钱 → 存档 → 渔村换背景 → 播漫画。</summary>
        void BuyShip()
        {
            GameAudio.Ensure().PlayUiClick();

            if (SaveSystem.ShipPurchased) return;

            if (PlayerProgress.Gold < shipPrice)
            {
                Debug.Log($"[商店] 金币不够买海神号（需要 {shipPrice}）");
                Rebuild();
                return;
            }

            PlayerProgress.AddGold(-shipPrice);
            SaveSystem.ShipPurchased = true;
            SaveSystem.Save();
            Debug.Log("[商店] 买到海神号！");

            var main = FindObjectOfType<MainMenuUI>();
            if (main != null) main.ApplyShipState();

            if (shipComic == null) shipComic = FindObjectOfType<ShipComicUI>();
            if (shipComic != null) shipComic.Play();

            Rebuild();
        }
    }
}
