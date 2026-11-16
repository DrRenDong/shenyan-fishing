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
        public float fleeTimer;         // >0 时正在往远处窜（被威慑吓跑）
        public Vector3 fleeFrom;        // 从钩子所在位置逃开

        /// <summary>被威慑吓到：接下来一小段时间往离开钩子的方向窜，免得"只跳过一帧"又被勾上。</summary>
        public void StartFlee(Vector3 from, float duration = 0.85f)
        {
            fleeTimer = duration;
            fleeFrom = from;
        }

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
            fleeTimer = 0f;
        }

        public bool IsBig => level >= 4;
    }
}
