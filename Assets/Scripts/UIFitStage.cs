using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 把一块固定设计尺寸的 UI 舞台等比缩放并居中，
    /// 让主界面元素保持与原图 1:1 的像素尺寸，只整体缩放到屏幕。
    /// cover = true 时铺满屏幕（裁边），false 时完整显示（留边）。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIFitStage : MonoBehaviour
    {
        [SerializeField] Vector2 designSize = new Vector2(1536f, 1024f);

        [Tooltip("true = 铺满屏幕、裁掉超出部分；false = 完整显示、四周留边")]
        [SerializeField] bool cover = true;

        RectTransform _rt;
        int _lastW = -1;
        int _lastH = -1;
        float _userScale = 1f;

        /// <summary>设置面板里的「UI 尺寸」档位（1 = 标准）。</summary>
        public void SetUserScale(float k)
        {
            k = Mathf.Clamp(k, 0.5f, 2f);
            if (Mathf.Approximately(k, _userScale)) return;
            _userScale = k;
            Apply();
        }

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (Screen.width != _lastW || Screen.height != _lastH) Apply();
        }

        void Apply()
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            _lastW = Mathf.Max(1, Screen.width);
            _lastH = Mathf.Max(1, Screen.height);

            float sx = _lastW / designSize.x;
            float sy = _lastH / designSize.y;
            // 村落/堂屋用 cover（整张场景铺满）；大地图用 fit（渔村在右下角，裁了会点不到）
            float s = (cover ? Mathf.Max(sx, sy) : Mathf.Min(sx, sy)) * _userScale;

            _rt.sizeDelta = designSize;
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }
}
