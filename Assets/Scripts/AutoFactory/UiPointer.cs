using UnityEngine;
using UnityEngine.EventSystems;

namespace Shenyan
{
    /// <summary>
    /// 统一的「指针是不是压在 UI 上」判断。
    /// EventSystem.IsPointerOverGameObject() 的无参版本只认鼠标：手机上滑动列表时它经常返回 false，
    /// 结果手指划 ScrollRect 会同时拖动地图、点 HUD 按钮也会穿透到下面的格子。
    /// 触摸屏必须按 fingerId 逐个查。
    /// </summary>
    public static class UiPointer
    {
        /// <summary>当前有没有任意鼠标/手指压在 UI 上。</summary>
        public static bool OverAnyUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
                }
            }

            // 鼠标（桌面端、编辑器，以及手机上的模拟鼠标）兜底
            return es.IsPointerOverGameObject();
        }

        /// <summary>指定手指是否压在 UI 上。</summary>
        public static bool OverUI(int fingerId)
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject(fingerId);
        }

        /// <summary>
        /// 按住不放的那一下是否从 UI 上开始。用于「按下时记一次、抬起时再用」的场景：
        /// 手指抬起后 touchCount 已经归零，抬起那一帧再查就查不到了。
        /// </summary>
        public static bool PressStartedOnUI(out int fingerId)
        {
            fingerId = -1;
            var es = EventSystem.current;
            if (es == null) return false;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (t.phase != TouchPhase.Began && t.phase != TouchPhase.Moved && t.phase != TouchPhase.Stationary)
                        continue;
                    fingerId = t.fingerId;
                    if (es.IsPointerOverGameObject(t.fingerId)) return true;
                }
                return false;
            }

            return es.IsPointerOverGameObject();
        }
    }
}
