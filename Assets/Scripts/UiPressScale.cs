using UnityEngine;
using UnityEngine.EventSystems;

namespace Shenyan
{
    /// <summary>
    /// 按钮按下反馈：按下时轻微缩小，松开弹回。
    /// 配合 Button 的 ColorTint 一起用，点起来更有手感。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Range(0.8f, 1f)] float pressScale = 0.95f;
        [SerializeField, Min(1f)] float speed = 20f;

        RectTransform _rt;
        float _target = 1f;
        float _current = 1f;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
        }

        void OnDisable()
        {
            _target = 1f;
            _current = 1f;
            if (_rt != null) _rt.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData) => _target = pressScale;
        public void OnPointerUp(PointerEventData eventData) => _target = 1f;

        void Update()
        {
            if (_rt == null) return;
            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            _rt.localScale = new Vector3(_current, _current, 1f);
        }
    }
}
