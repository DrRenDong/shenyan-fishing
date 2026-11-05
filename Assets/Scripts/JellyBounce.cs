using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 果冻弹性：被触发时先横向拉长、纵向压扁，再带阻尼回弹几下。
    /// 现在用在鱼堆飞进冷链车的那一下。
    /// </summary>
    [DisallowMultipleComponent]
    public class JellyBounce : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] float duration = 0.6f;      // 一次弹动多久
        [SerializeField, Range(0f, 0.6f)] float squash = 0.18f;  // 形变幅度
        [SerializeField, Min(1f)] float cycles = 2.5f;           // 回弹几次

        Vector3 _baseScale;
        bool _playing;
        float _t;

        void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void Play()
        {
            if (!_playing) _baseScale = transform.localScale;
            _playing = true;
            _t = 0f;
        }

        void Update()
        {
            if (!_playing) return;

            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / duration);

            // 砸下去的那一瞬间形变最大，之后阻尼正弦回弹
            float wave = Mathf.Cos(k * Mathf.PI * 2f * cycles) * (1f - k);
            transform.localScale = new Vector3(
                _baseScale.x * (1f + wave * squash),
                _baseScale.y * (1f - wave * squash),
                _baseScale.z);

            if (k >= 1f)
            {
                transform.localScale = _baseScale;
                _playing = false;
            }
        }
    }
}
