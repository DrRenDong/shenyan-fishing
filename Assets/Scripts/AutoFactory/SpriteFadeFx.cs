using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 通用的「一次性精灵特效」：可选的逐帧动画 + 逐渐淡出 + 匀速漂移，寿命到了自毁。
    /// 船尾浪和工厂炊烟都用它。
    /// </summary>
    public class SpriteFadeFx : MonoBehaviour
    {
        public SpriteRenderer sr;
        public Sprite[] frames;
        public float fps = 8f;
        public float life = 2.2f;
        public float startAlpha = 0.75f;
        [Tooltip("世界单位/秒")]
        public Vector3 drift = Vector3.zero;
        [Tooltip("缩放是否随着寿命一起变大（烟雾用）")]
        public float growTo = 1f;

        float _t;
        Vector3 _baseScale;

        void Awake()
        {
            _baseScale = transform.localScale;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Mathf.Max(0.01f, life));

            if (sr != null)
            {
                if (frames != null && frames.Length > 0)
                {
                    int idx = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(_t * fps));
                    sr.sprite = frames[idx];
                }

                Color c = sr.color;
                c.a = startAlpha * (1f - k);
                sr.color = c;
            }

            if (drift != Vector3.zero)
                transform.position += drift * Time.deltaTime;

            if (growTo > 1f)
                transform.localScale = _baseScale * Mathf.Lerp(1f, growTo, k);

            if (_t >= life) Destroy(gameObject);
        }
    }
}
