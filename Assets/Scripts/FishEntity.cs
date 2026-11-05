using UnityEngine;

namespace Shenyan
{
    /// <summary>挂在每条鱼上的轻量数据，供鱼钩判定"勾到了什么"。</summary>
    [DisallowMultipleComponent]
    public class FishEntity : MonoBehaviour
    {
        public string fishName;
        public int value;
        public int level;
        public bool bottom;             // 沉底类
        public bool hooked;             // 已经挂在钩子上
        public bool releaseRequested;   // 中途放回水里（大鱼挤掉小鱼），等鱼群系统原地安置
        public bool consumeRequested;   // 已经收上船/卖掉，等鱼群系统收进池子（必须消失）
        public bool intimidated;        // 已被「威慑」吓退过一次（之后再遇到就正常会咬钩）

        public void Setup(FishDatabase.Info info, string name)
        {
            fishName = name;
            value = info.value;
            level = info.level;
            bottom = info.behavior == FishBehavior.Bottom;
            hooked = false;
            releaseRequested = false;
            consumeRequested = false;
            intimidated = false;
        }

        public bool IsBig => level >= 4;
    }
}
