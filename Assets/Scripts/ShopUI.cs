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
        [SerializeField] float firstRowY = 190f;
        [SerializeField] float rowGap = 66f;
        [SerializeField] Vector2 rowSize = new Vector2(900f, 56f);
        [SerializeField] Text hintText;          // 旧版的“进货中”提示，重建时隐藏

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
                    new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        }

        void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
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
            row.transform.SetParent(transform, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
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
    }
}
