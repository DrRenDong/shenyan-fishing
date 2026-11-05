using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 头顶飘字：一句话从人物头顶冒出来，边升边淡出。
    /// 用世界空间 Canvas（代码生成，不依赖预制体），跟着挂载的物体一起动。
    /// </summary>
    [DisallowMultipleComponent]
    public class FloatingHint : MonoBehaviour
    {
        [SerializeField] Vector3 offset = new Vector3(0f, 1.75f, 0f);   // 相对船中心：船顶在 +1
        [SerializeField] float canvasScale = 0.011f;
        [SerializeField] float riseDistance = 0.8f;
        [SerializeField] float lifetime = 1.7f;
        [SerializeField] int fontSize = 34;
        [SerializeField] int sortingOrder = 30;

        Canvas _canvas;
        CanvasGroup _group;
        Text _text;
        float _t = -1f;
        Vector3 _basePos;

        public void Show(string message)
        {
            Build();
            if (_canvas == null) return;

            _text.text = message;
            _t = 0f;
            _basePos = offset;
            _canvas.gameObject.SetActive(true);
            _canvas.transform.localPosition = _basePos;
            _group.alpha = 1f;
        }

        void Build()
        {
            if (_canvas != null) return;

            var go = new GameObject("HintCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            go.transform.localScale = Vector3.one * canvasScale;

            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = sortingOrder;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(760f, 96f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            _text = textGo.GetComponent<Text>();
            _text.font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Arial" }, fontSize);
            _text.fontSize = fontSize;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.color = Color.white;
            _text.raycastTarget = false;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            _group = go.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            go.SetActive(false);
        }

        void Update()
        {
            if (_t < 0f || _canvas == null) return;

            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / lifetime);

            _canvas.transform.localPosition = _basePos + Vector3.up * (riseDistance * k);

            // 前 12% 快速淡入，后 40% 淡出
            float a = k < 0.12f ? k / 0.12f : 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
            _group.alpha = a;

            if (k >= 1f)
            {
                _t = -1f;
                _canvas.gameObject.SetActive(false);
            }
        }
    }
}
