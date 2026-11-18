using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 船尾浪：渔船走过之后，在原来那一格留下一团渐渐散开的浪花。
    /// 精灵来自 Assets/Resources/Image/特效/船尾浪_*.png（水花素材表切出来的 3 个大小）。
    /// </summary>
    public static class BoatWakeFx
    {
        const string Folder = "Image/特效";
        const string Prefix = "船尾浪_";

        static Sprite[] _sprites;

        static Sprite[] Sprites
        {
            get
            {
                if (_sprites == null || _sprites.Length == 0)
                {
                    var all = Resources.LoadAll<Sprite>(Folder);
                    var list = new System.Collections.Generic.List<Sprite>();
                    foreach (var s in all)
                        if (s != null && s.name.StartsWith(Prefix)) list.Add(s);
                    _sprites = list.ToArray();
                }
                return _sprites;
            }
        }

        /// <summary>在 world 处留下一道浪花痕迹（sortingOrder 用船所在的层级）。</summary>
        public static void Spawn(Vector3 world, int sortingOrder, float scale = 0.9f)
        {
            Sprite[] sp = Sprites;
            if (sp == null || sp.Length == 0) return;

            var go = new GameObject("wake");
            go.transform.position = new Vector3(world.x, world.y, -0.006f);
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp[Random.Range(0, sp.Length)];
            sr.sortingOrder = sortingOrder;
            sr.flipX = Random.value > 0.5f;
            sr.flipY = Random.value > 0.5f;
            sr.color = new Color(1f, 1f, 1f, 0.8f);

            var fx = go.AddComponent<SpriteFadeFx>();
            fx.sr = sr;
            fx.life = 2.2f;
            fx.startAlpha = 0.8f;
            fx.drift = Vector3.zero;
            fx.growTo = 1.25f;
        }
    }
}
