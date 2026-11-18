using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 水下焦散：海床上流动的网状光纹，两层朝不同方向缓慢漂移，形成交错的闪动。
    ///
    /// 素材是「纯黑底 + 青蓝光网」（Assets/Resources/Image/特效/焦散.png），
    /// 用 AdditiveSprite 材质加色叠加，所以只会在水里"加光"，不会遮住鱼和地形。
    /// 上边界对齐水面（surfaceY），不会照到天上。
    /// </summary>
    [DefaultExecutionOrder(538)]
    public class SeabedCaustics : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("素材")]
        public string resourceFolder = "Image/特效";
        public string spriteName = "焦散";

        [Header("摆放")]
        public float surfaceY = 16f;
        [Tooltip("向下延伸超出画面底部多少")]
        public float bottomMargin = 6f;
        [Tooltip("横向铺开倍数（1 = 正好铺满屏宽，留出漂移余量）")]
        public float widthScale = 1.6f;

        [Header("外观")]
        [Range(0f, 1f)] public float alpha = 0.30f;
        public Color tint = new Color(0.72f, 0.95f, 1f, 1f);
        public int baseSortingOrder = 2990;

        [Header("漂移")]
        public float driftX = 1.1f;
        public float driftY = 0.35f;
        public float driftSpeedA = 0.055f;
        public float driftSpeedB = 0.083f;
        public float pulseSpeed = 0.30f;
        [Range(0f, 1f)] public float pulseAmount = 0.25f;

        [Header("时段")]
        public TimeOfDaySystem timeOfDay;
        [Range(0f, 1f)] public float nightAlphaScale = 0.15f;

        Sprite _sprite;
        SpriteRenderer[] _layers = new SpriteRenderer[2];

        void Start()
        {
            if (timeOfDay == null) timeOfDay = FindObjectOfType<TimeOfDaySystem>();

            _sprite = Resources.Load<Sprite>($"{resourceFolder}/{spriteName}");
            if (_sprite == null)
            {
                var all = Resources.LoadAll<Sprite>(resourceFolder);
                foreach (var s in all)
                    if (s != null && s.name == spriteName) { _sprite = s; break; }
            }
            if (_sprite == null) return;

            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("caustic_" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprite;
                sr.sortingOrder = baseSortingOrder + i;
                AdditiveMat.Apply(sr);
                _layers[i] = sr;
            }
        }

        void OnDisable()
        {
            // 组件被关掉时，已经建出来的子物体不会自动消失，这里手动收起来
            if (_layers == null) return;
            for (int i = 0; i < _layers.Length; i++)
                if (_layers[i] != null) _layers[i].enabled = false;
        }

        void LateUpdate()
        {
            if (_sprite == null || _layers[0] == null) return;
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            float viewH = cam.orthographic ? cam.orthographicSize * 2f : 10f;
            float viewW = viewH * Mathf.Max(0.1f, cam.aspect);
            Vector3 camPos = cam.transform.position;

            float bottom = camPos.y - viewH * 0.5f - bottomMargin;
            float top = surfaceY;
            float height = Mathf.Max(1f, top - bottom);
            float width = viewW * Mathf.Max(1f, widthScale);

            float night = 1f;
            if (timeOfDay != null && timeOfDay.IsNight) night = nightAlphaScale;

            float t = Time.time;
            float spriteW = Mathf.Max(0.01f, _sprite.bounds.size.x);
            float spriteH = Mathf.Max(0.01f, _sprite.bounds.size.y);

            for (int i = 0; i < 2; i++)
            {
                SpriteRenderer sr = _layers[i];
                if (sr == null) continue;

                float speed = i == 0 ? driftSpeedA : driftSpeedB;
                float sign = i == 0 ? 1f : -1f;

                float x = camPos.x + Mathf.Sin(t * speed * Mathf.PI * 2f + i * 1.7f) * driftX * sign;
                // 纵向只允许往下漂（(cos-1)/2 的值域是 [-1,0]），
                // 否则层顶会被推到水面以上，海面上方会露出焦散。
                float dy = (Mathf.Cos(t * speed * Mathf.PI * 2f + i * 2.3f) - 1f) * 0.5f * driftY;
                float y = (top + bottom) * 0.5f + dy;

                sr.transform.position = new Vector3(x, y, 0f);
                sr.transform.localScale = new Vector3(width / spriteW, height / spriteH, 1f);

                float pulse = 1f - pulseAmount * 0.5f + pulseAmount * 0.5f * Mathf.Sin(t * pulseSpeed + i * 2.1f);
                Color c = tint;
                c.a = alpha * pulse * night;
                sr.color = c;
            }
        }
    }
}
