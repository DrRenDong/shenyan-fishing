using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 让界面完全不受主相机后处理影响。
    ///
    /// 工厂场景里的面板/按钮都是 ScreenSpaceCamera 画布，会跟着主相机一起进
    /// Bloom / Vignette / ColorGrading —— 实测信息面板亮度会掉 60%、边缘锐度掉 20%+，
    /// 详情面板、货运码头面板、回合数面板、右下功能键全都变暗发闷。
    ///
    /// 这里把它们统一改成 ScreenSpaceOverlay：覆盖层在相机渲染完之后才绘制，
    /// 后处理和景深都碰不到；因为是按屏幕像素排布，UI 的位置和尺寸不会变。
    ///
    /// 每 0.25 秒扫一次，这样后面动态创建的面板（商店行、码头订单、提示条）
    /// 也会自动被纳入，不需要改各个面板的创建代码。
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class UiRenderIsolation : MonoBehaviour
    {
        [Tooltip("多久扫一次画布（秒）。动态创建的面板最迟这么久之后就会切到覆盖层")]
        public float scanInterval = 0.25f;

        readonly List<Canvas> _canvases = new List<Canvas>();
        float _timer;

        /// <summary>累计切换过的画布数量（调试用）。</summary>
        public int ConvertedCount { get; private set; }

        void Start()
        {
            Convert();
        }

        void LateUpdate()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < scanInterval) return;
            _timer = 0f;
            Convert();
        }

        public void Convert()
        {
            _canvases.Clear();
            _canvases.AddRange(Object.FindObjectsOfType<Canvas>(true));

            foreach (Canvas c in _canvases)
            {
                if (c == null) continue;
                if (c.renderMode != RenderMode.ScreenSpaceCamera) continue;

                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.worldCamera = null;
                ConvertedCount++;
            }
        }
    }
}
