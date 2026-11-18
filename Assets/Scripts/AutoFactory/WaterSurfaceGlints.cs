using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 水面波光：沿着水面线（surfaceY）不断冒出细小的闪光，加色叠加后像阳光在水面上跳。
    /// 复用特效目录里的 波光_*.png，只在画面范围内随机出现，淡入淡出后换地方。
    /// </summary>
    [DefaultExecutionOrder(544)]
    public class WaterSurfaceGlints : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("素材")]
        public string resourceFolder = "Image/特效";
        public string spritePrefix = "波光_";

        [Header("摆放")]
        public float surfaceY = 16f;
        [Tooltip("沿水面线的上下抖动")]
        public float verticalJitter = 0.18f;
        [Tooltip("横向铺开范围（占屏宽比例）")]
        public float horizontalSpread = 1.0f;

        [Header("数量与节奏")]
        public int slotCount = 14;
        public float minLife = 0.9f;
        public float maxLife = 2.2f;
        public float fadeTime = 0.3f;
        public float minGap = 0.15f;
        public float maxGap = 0.9f;

        [Header("外观")]
        [Range(0f, 2f)] public float alpha = 0.85f;
        public float minScale = 0.55f;
        public float maxScale = 1.15f;
        public Color tint = new Color(0.9f, 0.98f, 1f, 1f);
        public int sortingOrder = 2600;

        [Header("时段")]
        public TimeOfDaySystem timeOfDay;
        [Range(0f, 1f)] public float nightAlphaScale = 0.25f;

        Sprite[] _sprites;
        Slot[] _slots;

        class Slot
        {
            public SpriteRenderer sr;
            public float timer;
            public float life;
            public float wait;
            public float x;        // 世界坐标（固定在水面上，镜头移动时绕回来）
            public bool inited;
        }

        void Start()
        {
            if (timeOfDay == null) timeOfDay = FindObjectOfType<TimeOfDaySystem>();

            var all = Resources.LoadAll<Sprite>(resourceFolder);
            var list = new List<Sprite>();
            foreach (var s in all)
                if (s != null && s.name.StartsWith(spritePrefix)) list.Add(s);
            _sprites = list.ToArray();
            if (_sprites.Length == 0) return;

            _slots = new Slot[Mathf.Max(1, slotCount)];
            for (int i = 0; i < _slots.Length; i++)
            {
                var go = new GameObject("glint_" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.enabled = false;
                AdditiveMat.Apply(sr);
                _slots[i] = new Slot { sr = sr, wait = Random.Range(0f, 1.2f) };
            }
        }

        void OnDisable()
        {
            // 关掉组件时把正在闪的那些点也收起来，否则会定格在画面上
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].sr != null) _slots[i].sr.enabled = false;
        }

        void Update()
        {
            if (_slots == null) return;
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            float viewH = cam.orthographic ? cam.orthographicSize * 2f : 10f;
            float viewW = viewH * Mathf.Max(0.1f, cam.aspect);

            float night = 1f;
            if (timeOfDay != null && timeOfDay.IsNight) night = nightAlphaScale;

            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (s.sr == null) continue;

                if (s.life <= 0f)
                {
                    s.wait -= Time.deltaTime;
                    if (s.wait > 0f) continue;

                    s.sr.sprite = _sprites[Random.Range(0, _sprites.Length)];

                    // 波光固定在水面的世界坐标上：镜头移动时从另一侧绕回来，
                    // 不跟着镜头"贴着走"。
                    float span = viewW * Mathf.Max(1f, horizontalSpread);
                    float half = span * 0.5f;
                    if (!s.inited)
                    {
                        s.x = cam.transform.position.x + Random.Range(-0.5f, 0.5f) * span;
                        s.inited = true;
                    }
                    float rel = s.x - cam.transform.position.x;
                    if (rel > half) s.x -= span;
                    else if (rel < -half) s.x += span;

                    float x = s.x + Random.Range(-0.12f, 0.12f);
                    float y = surfaceY + Random.Range(-verticalJitter, verticalJitter);
                    s.sr.transform.position = new Vector3(x, y, 0f);

                    float sc = Random.Range(minScale, maxScale);
                    s.sr.transform.localScale = new Vector3(sc, sc, 1f);
                    s.sr.flipX = Random.value > 0.5f;
                    s.sr.sortingOrder = sortingOrder + i;
                    s.sr.enabled = true;

                    s.timer = 0f;
                    s.life = Random.Range(minLife, maxLife);
                    continue;
                }

                s.timer += Time.deltaTime;
                float a = 1f;
                if (s.timer < fadeTime) a = s.timer / fadeTime;
                else if (s.timer > s.life - fadeTime) a = Mathf.Max(0f, (s.life - s.timer) / fadeTime);

                Color c = tint;
                c.a = alpha * Mathf.Clamp01(a) * night;
                s.sr.color = c;

                if (s.timer >= s.life)
                {
                    s.sr.enabled = false;
                    s.life = 0f;
                    s.wait = Random.Range(minGap, maxGap);
                }
            }
        }
    }
}
