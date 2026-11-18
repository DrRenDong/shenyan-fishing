using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 水面波光：在相机附近的水面上不断冒出小亮点，淡入淡出后换个地方再冒。
    /// 海面因此会有轻微闪烁，不至于是一张死图。
    ///
    /// 精灵来自 Assets/Resources/Image/特效/波光_*.png（水花素材表切出来的 21 个）。
    /// </summary>
    [DefaultExecutionOrder(510)]
    public class WaterGlints : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("素材")]
        public string resourceFolder = "Image/特效";
        public string spritePrefix = "波光_";

        [Header("数量与节奏")]
        public int slotCount = 12;
        public float minLife = 1.1f;
        public float maxLife = 2.4f;
        public float fadeTime = 0.35f;

        [Header("外观")]
        [Tooltip("世界单位下的基础尺寸（会再乘随机 0.8~1.4）")]
        public float baseScale = 1.0f;
        public Color tint = new Color(1f, 1f, 1f, 1f);
        public int sortingOrderBias = 0;

        Sprite[] _sprites;
        Slot[] _slots;
        IsoTileMap _map;

        class Slot
        {
            public SpriteRenderer sr;
            public float timer;
            public float life;
            public float wait;
        }

        void Start()
        {
            LoadSprites();
            _map = FindObjectOfType<IsoTileMap>();
            BuildSlots();
        }

        void LoadSprites()
        {
            var all = Resources.LoadAll<Sprite>(resourceFolder);
            var list = new List<Sprite>();
            foreach (var s in all)
                if (s != null && s.name.StartsWith(spritePrefix)) list.Add(s);
            _sprites = list.ToArray();
        }

        void BuildSlots()
        {
            _slots = new Slot[Mathf.Max(1, slotCount)];
            for (int i = 0; i < _slots.Length; i++)
            {
                var go = new GameObject("glint_" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.enabled = false;
                AdditiveMat.Apply(sr);          // 光效用加色混合：透明处不会带出任何底色
                _slots[i] = new Slot { sr = sr, timer = 0f, life = 0f, wait = Random.Range(0f, 1.5f) };
            }
        }

        void Update()
        {
            if (_slots == null || _sprites == null || _sprites.Length == 0) return;
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (s.sr == null) continue;

                if (s.life <= 0f)
                {
                    s.wait -= Time.deltaTime;
                    if (s.wait > 0f) continue;
                    if (!Spawn(s, cam)) { s.wait = 0.5f; continue; }
                }

                s.timer += Time.deltaTime;
                float a = 1f;
                if (s.timer < fadeTime) a = s.timer / fadeTime;
                else if (s.timer > s.life - fadeTime) a = Mathf.Max(0f, (s.life - s.timer) / fadeTime);

                Color c = tint;
                c.a = tint.a * Mathf.Clamp01(a);
                s.sr.color = c;

                if (s.timer >= s.life)
                {
                    s.sr.enabled = false;
                    s.life = 0f;
                    s.wait = Random.Range(0.2f, 1.4f);
                }
            }
        }

        bool Spawn(Slot s, Camera cam)
        {
            if (_map == null) return false;

            float h = cam.orthographic ? cam.orthographicSize : 5f;
            float w = h * Mathf.Max(0.1f, cam.aspect);
            Vector3 camPos = cam.transform.position;

            for (int t = 0; t < 14; t++)
            {
                Vector3 p = camPos + new Vector3(Random.Range(-w * 0.85f, w * 0.85f),
                                                Random.Range(-h * 0.85f, h * 0.85f), 0f);
                Vector2Int cell = PlacedTile.WorldToCell(p);

                bool water = _map.IsWaterAt(cell.x, cell.y);
                bool openSea = string.IsNullOrEmpty(_map.TileAt(cell.x, cell.y));
                if (!water && !openSea) continue;

                Vector3 basePos = PlacedTile.CellToWorld(cell.x, cell.y);
                s.sr.transform.position = new Vector3(
                    basePos.x + Random.Range(-0.25f, 0.25f),
                    basePos.y + 0.66f + Random.Range(-0.18f, 0.18f),
                    -0.004f);

                s.sr.sprite = _sprites[Random.Range(0, _sprites.Length)];
                float sc = baseScale * Random.Range(0.8f, 1.4f);
                s.sr.transform.localScale = new Vector3(sc, sc, 1f);
                s.sr.sortingOrder = (cell.x + cell.y) * 4 + sortingOrderBias;
                s.sr.flipX = Random.value > 0.5f;
                s.sr.enabled = true;

                s.timer = 0f;
                s.life = Random.Range(minLife, maxLife);
                return true;
            }
            return false;
        }
    }
}
