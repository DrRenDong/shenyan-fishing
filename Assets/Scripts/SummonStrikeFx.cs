using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 召唤物（海龟 / 鲸鱼）的自毁兜底：
    /// 它们的飞行是鱼钩上的协程，如果战斗提前结束、鱼钩被销毁，协程会跟着停，
    /// 召唤物就会永远停在半路。挂上这个组件后，它会自己接着飞到目标点再消失。
    /// </summary>
    public class SummonStrikeFx : MonoBehaviour
    {
        public float life = 1.5f;
        public float speed = 30f;
        public Vector3 target;

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        }
    }
}
