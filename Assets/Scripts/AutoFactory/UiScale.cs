using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 全局 UI 尺寸档位（设置面板里调）。手机上 1920×1080 参考分辨率的 UI 字太小，
    /// 这里统一放大/缩小所有 Canvas：调整 CanvasScaler.referenceResolution（越小 UI 越大），
    /// 主菜单那种手工缩放的 UIFitStage 舞台则整体乘一个系数。
    /// 选择存 PlayerPrefs，重启后保持。
    /// </summary>
    public static class UiScale
    {
        public const string PrefKey = "ui_scale_index";

        static readonly float[] Steps = { 0.85f, 1f, 1.15f, 1.3f };
        static readonly string[] Names = { "小", "标准", "大", "特大" };

        static int _index = -1;

        public static int Count => Steps.Length;

        public static int Index
        {
            get
            {
                if (_index < 0)
                    _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 1), 0, Steps.Length - 1);
                return _index;
            }
        }

        public static float Value => Steps[Index];
        public static string Label => Names[Index];

        /// <summary>这档尺寸只作用在工厂模式；主菜单、捕鱼界面保持原样。</summary>
        public static bool AppliesHere =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "FactoryDemo";

        public static void SetIndex(int i)
        {
            _index = Mathf.Clamp(i, 0, Steps.Length - 1);
            PlayerPrefs.SetInt(PrefKey, _index);
            PlayerPrefs.Save();
            Apply();
        }

        public static void Next() => SetIndex((Index + 1) % Steps.Length);
        public static void Prev() => SetIndex((Index - 1 + Steps.Length) % Steps.Length);

        /// <summary>把所有 Canvas / 舞台按当前档位重算一遍（新开的 Canvas 由 UiScaleRunner 定时补上）。</summary>
        public static void Apply()
        {
            // 只缩放工厂模式；其它场景用 1（等于恢复原始尺寸），退出工厂就自动还原
            float k = AppliesHere ? Value : 1f;

            foreach (var scaler in Object.FindObjectsOfType<CanvasScaler>(true))
            {
                if (scaler == null) continue;
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;

                // 原始参考分辨率记在组件所在的 GameObject 上，随对象一起销毁，不会像 instanceID 那样错位
                var tag = scaler.GetComponent<UiScaleTag>();
                if (tag == null)
                {
                    tag = scaler.gameObject.AddComponent<UiScaleTag>();
                    tag.baseRef = scaler.referenceResolution;
                }
                if (tag.baseRef.x <= 0f || tag.baseRef.y <= 0f)
                    tag.baseRef = scaler.referenceResolution;

                Vector2 target = tag.baseRef / k;
                if (tag.applied != target)
                {
                    scaler.referenceResolution = target;
                    tag.applied = target;
                }
            }

            foreach (var stage in Object.FindObjectsOfType<UIFitStage>(true))
                stage.SetUserScale(k);
        }
    }
}
