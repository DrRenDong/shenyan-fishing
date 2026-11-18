using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 水下丁达尔光柱：从水面往下射的一束束光，加色叠在画面上。
    ///
    /// 素材是「纯黑底 + 白色光柱」（Assets/Resources/Image/特效/光柱_1..5.png），
    /// 用 AdditiveSprite 材质，黑色部分加 0 亮度＝自动透明。
    ///
    /// 光柱固定在屏幕上（跟着相机走），各自缓慢摆动 + 明暗呼吸，
    /// 画面顶部对齐水面高度，所以只在水中出现、不会飘到天上。
    /// 支持接 TimeOfDaySystem：入夜自动压暗。
    /// </summary>
    [DefaultExecutionOrder(540)]
    public class UnderwaterGodRays : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("素材")]
        public string resourceFolder = "Image/特效";
        public string spritePrefix = "光柱_";

        [Header("摆放")]
        public int beamCount = 7;
        [Tooltip("水面高度（世界 Y）。光柱顶边贴在这里")]
        public float surfaceY = 16f;
        [Tooltip("光柱向下延伸超出画面底部多少")]
        public float bottomMargin = 6f;
        [Tooltip("横向铺开的范围（占屏宽的比例，1 = 正好铺满）")]
        public float horizontalSpread = 1.15f;

        [Header("外观")]
        [Range(0f, 2f)] public float baseAlpha = 0.55f;
        [Tooltip("宽度随机倍率")]
        public float minWidthScale = 0.7f;
        public float maxWidthScale = 1.4f;
        public Color tint = new Color(0.85f, 0.97f, 1f, 1f);

        [Header("动")]
        public float swayAmplitude = 0.55f;
        public float minSwaySpeed = 0.10f;
        public float maxSwaySpeed = 0.26f;
        public float pulseSpeed = 0.35f;
        [Range(0f, 1f)] public float pulseAmount = 0.28f;
        [Tooltip("光柱在世界里缓慢横向漂移的速度（像背景一样滚动，不跟着镜头走）")]
        public float worldDrift = 0.18f;

        [Header("时段")]
        public TimeOfDaySystem timeOfDay;
        [Range(0f, 1f)] public float nightAlphaScale = 0.18f;

        Sprite[] _sprites;
        Beam[] _beams;
        int _builtForCount = -1;

        class Beam
        {
            public SpriteRenderer sr;
            public float slot;       // -0.5 ~ 0.5 的初始相对位置
            public float widthScale;
            public float swaySpeed;
            public float swayPhase;
            public float pulsePhase;
            public float x;          // 世界坐标（自己漂移 + 循环回卷）
            public bool inited;
        }

        void Start()
        {
            if (timeOfDay == null) timeOfDay = FindObjectOfType<TimeOfDaySystem>();
            LoadSprites();
            Build();
        }

        void LoadSprites()
        {
            var all = Resources.LoadAll<Sprite>(resourceFolder);
            var list = new List<Sprite>();
            foreach (var s in all)
                if (s != null && s.name.StartsWith(spritePrefix)) list.Add(s);
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _sprites = list.ToArray();
        }

        void Build()
        {
            if (_sprites == null || _sprites.Length == 0) return;
            if (_builtForCount == beamCount && _beams != null) return;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var ch = transform.GetChild(i);
                if (ch.name.StartsWith("beam_")) Destroy(ch.gameObject);
            }

            _beams = new Beam[Mathf.Max(1, beamCount)];
            for (int i = 0; i < _beams.Length; i++)
            {
                var go = new GameObject("beam_" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprites[Random.Range(0, _sprites.Length)];
                sr.sortingOrder = 3000 + i;
                AdditiveMat.Apply(sr);

                _beams[i] = new Beam
                {
                    sr = sr,
                    slot = _beams.Length == 1 ? 0f : (i / (float)(_beams.Length - 1) - 0.5f),
                    widthScale = Random.Range(minWidthScale, maxWidthScale),
                    swaySpeed = Random.Range(minSwaySpeed, maxSwaySpeed),
                    swayPhase = Random.Range(0f, Mathf.PI * 2f),
                    pulsePhase = Random.Range(0f, Mathf.PI * 2f),
                };
            }
            _builtForCount = beamCount;
        }

        void OnDisable()
        {
            if (_beams == null) return;
            for (int i = 0; i < _beams.Length; i++)
                if (_beams[i] != null && _beams[i].sr != null) _beams[i].sr.enabled = false;
        }

        void LateUpdate()
        {
            if (_beams == null || _beams.Length == 0) return;
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            float viewH = cam.orthographic ? cam.orthographicSize * 2f : 10f;
            float viewW = viewH * Mathf.Max(0.1f, cam.aspect);
            Vector3 camPos = cam.transform.position;

            float bottom = camPos.y - viewH * 0.5f - bottomMargin;
            float columnHeight = Mathf.Max(1f, surfaceY - bottom);

            float night = 1f;
            if (timeOfDay != null && timeOfDay.IsNight) night = nightAlphaScale;

            float t = Time.time;
            // 光柱按世界坐标排布：镜头移动时它们留在原地，滚出画面就从另一侧绕回来，
            // 看起来像一层会循环的背景，而不是贴在屏幕上的滤镜。
            float span = viewW * Mathf.Max(1f, horizontalSpread) * 1.8f;
            float half = span * 0.5f;

            for (int i = 0; i < _beams.Length; i++)
            {
                Beam b = _beams[i];
                if (b.sr == null || b.sr.sprite == null) continue;

                if (!b.inited)
                {
                    b.x = camPos.x + b.slot * span;
                    b.inited = true;
                }

                b.x += worldDrift * Time.deltaTime;
                float rel = b.x - camPos.x;
                if (rel > half) b.x -= span;
                else if (rel < -half) b.x += span;

                float x = b.x + Mathf.Sin(t * b.swaySpeed + b.swayPhase) * swayAmplitude;

                // 纵向：顶边贴水面，向下铺满到画面底部以下
                float spriteH = b.sr.sprite.bounds.size.y;
                float spriteW = b.sr.sprite.bounds.size.x;
                float scaleY = columnHeight / Mathf.Max(0.01f, spriteH);
                float scaleX = scaleY * b.widthScale;

                b.sr.transform.position = new Vector3(x, surfaceY, 0f);
                b.sr.transform.localScale = new Vector3(scaleX, scaleY, 1f);

                float pulse = 1f - pulseAmount * 0.5f + pulseAmount * 0.5f * Mathf.Sin(t * pulseSpeed + b.pulsePhase);
                Color c = tint;
                c.a = baseAlpha * pulse * night;
                b.sr.color = c;
            }
        }
    }
}
