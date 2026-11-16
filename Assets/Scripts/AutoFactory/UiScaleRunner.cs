using UnityEngine;

namespace Shenyan
{
    /// <summary>定时把 UI 尺寸档位同步到所有 Canvas（包括场景切换后 / 运行时新建的面板）。</summary>
    [DisallowMultipleComponent]
    public class UiScaleRunner : MonoBehaviour
    {
        float _timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("UiScaleRunner");
            go.AddComponent<UiScaleRunner>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            UiScale.Apply();      // 进场景就先套一次，别等第一个 tick
        }

        void LateUpdate()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < 0.4f) return;
            _timer = 0f;
            UiScale.Apply();
        }
    }
}
