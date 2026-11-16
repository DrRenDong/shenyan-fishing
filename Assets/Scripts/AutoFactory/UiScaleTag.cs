using UnityEngine;

namespace Shenyan
{
    /// <summary>记录某个 CanvasScaler 的原始参考分辨率（UiScale 内部用，运行时挂在同一个 GameObject 上）。</summary>
    [DisallowMultipleComponent]
    public class UiScaleTag : MonoBehaviour
    {
        public Vector2 baseRef;
        public Vector2 applied;
    }
}
