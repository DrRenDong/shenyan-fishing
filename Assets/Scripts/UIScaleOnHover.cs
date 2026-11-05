using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 可点元素提示：待机时轻微「呼吸」（缩放脉动），鼠标/手指指上去再放大并提亮。
    /// 挂在带 Button 的 UI 元素上即可。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIScaleOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("悬停")]
        [SerializeField, Min(1f)] float hoverScale = 1.06f;
        [SerializeField, Min(0.1f)] float speed = 12f;
        [SerializeField] float hoverBrightness = 1.08f;

        [Header("待机呼吸（提示这里能点）")]
        [SerializeField] bool idlePulse = true;
        [SerializeField, Range(0f, 0.08f)] float pulseAmplitude = 0.018f;
        [SerializeField] float pulseSpeed = 2.2f;

        RectTransform _rt;
        Image _img;
        Color _baseColor = Color.white;
        float _target = 1f;
        float _current = 1f;
        bool _hovered;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _img = GetComponent<Image>();
            if (_img != null) _baseColor = _img.color;
            _current = 1f;
        }

        void OnDisable()
        {
            _hovered = false;
            _target = 1f;
            _current = 1f;
            if (_rt != null) _rt.localScale = Vector3.one;
            if (_img != null) _img.color = _baseColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            _target = hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _target = 1f;
        }

        void Update()
        {
            if (_rt == null) return;

            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));

            // 悬停时按 hoverScale 放大，平时轻微呼吸（不悬停才呼吸，避免两股缩放打架）
            float pulse = (idlePulse && !_hovered)
                ? 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude
                : 1f;

            float s = _current * pulse;
            _rt.localScale = new Vector3(s, s, 1f);

            if (_img != null)
            {
                if (_hovered)
                {
                    var c = _baseColor * hoverBrightness;
                    c.a = _baseColor.a;
                    _img.color = c;
                }
                else if (_img.color != _baseColor)
                {
                    _img.color = _baseColor;
                }
            }
        }
    }
}
