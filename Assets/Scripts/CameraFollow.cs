using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 相机跟随：水平缓动跟船，垂直零缓动（上下晃会晕）。
    /// 滚轮推镜头，位移量会被夹住不会累加；
    /// 画面下限按当前所在列的海床高度实时算。有钩子时垂直跟随钩子。
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        [Header("跟随目标")]
        [SerializeField] Transform target;
        [SerializeField] Vector2 offset = new Vector2(0f, 3f);
        [SerializeField, Min(0.01f)] float smoothTime = 0.35f;   // 只作用于水平

        [Header("视野")]
        [SerializeField, Min(1f)] float viewSize = 12f;
        [SerializeField] float skyTopY = 38.75f;
        [SerializeField] float minCameraY = -2f;                 // 拿不到海床高度时的兜底

        [Header("垂直平移（滚轮）")]
        [SerializeField] float scrollStep = 2f;

        [Header("海床")]
        [SerializeField] SandbarTerrain terrain;                 // 用它的顶面当画面下限

        [Header("钩子")]
        [SerializeField] Transform yTarget;

        [Header("大鱼战斗")]
        [SerializeField, Min(1f)] float battleMaxZoom = 1.8f;    // 战斗拉远最多到 viewSize 的几倍

        float _velocityX;
        float _scrollOffset;

        bool _battleOn;
        Vector3 _battlePos;
        float _battleSize;

        public float ViewSize => viewSize;
        public bool BattleView => _battleOn;

        /// <summary>大鱼战斗：拉远到指定位置/大小并锁定（center 只取 x/y，z 保持）。返回实际生效的视口大小。</summary>
        public float SetBattleFocus(bool on, Vector3 center, float size)
        {
            _battleOn = on;
            if (!on) return _battleSize;

            _battlePos = center;
            _battleSize = Mathf.Clamp(size, viewSize, viewSize * battleMaxZoom);
            return _battleSize;
        }

        public float CameraMaxY => skyTopY - viewSize;

        /// <summary>当前允许的最低相机高度：海床顶面，再叠加神眼潜望距离的限制。</summary>
        public float CurrentMinY
        {
            get
            {
                float min = minCameraY;
                if (terrain != null && terrain.TryGetGroundTopY(transform.position.x, out float groundTop))
                    min = groundTop;

                // 神眼：没满级时相机不能潜得比潜望距离更深（满级 DiveLimitY 是负无穷，等于没限制）
                if (AbilityData.EyeLevel < AbilityData.EyeMax)
                    min = Mathf.Max(min, AbilityData.DiveLimitY);

                return min;
            }
        }

        /// <summary>潜望余量：1 = 相机在默认位置（条满），0 = 已经潜到神眼极限。</summary>
        public float DiveRatio
        {
            get
            {
                float minY = CurrentMinY;
                float topY = target != null ? target.position.y + offset.y : transform.position.y;
                if (topY <= minY) return 0f;
                return Mathf.Clamp01((transform.position.y - minY) / (topY - minY));
            }
        }

        /// <summary>移动端拖动 / 外部调用：叠加垂直平移量（LateUpdate 里会被夹到有效范围）。</summary>
        public void ScrollBy(float amount) => _scrollOffset += amount;

        void OnValidate()
        {
            var cam = GetComponent<Camera>();
            if (cam != null && cam.orthographic) cam.orthographicSize = viewSize;
        }

        void LateUpdate()
        {
            var cam = GetComponent<Camera>();

            // 大鱼战斗：镜头拉远并锁死在战斗画框上，不跟随、不响应滚轮
            if (_battleOn)
            {
                if (cam != null && !Mathf.Approximately(cam.orthographicSize, _battleSize))
                    cam.orthographicSize = _battleSize;
                transform.position = new Vector3(_battlePos.x, _battlePos.y, transform.position.z);
                return;
            }

            if (cam != null && !Mathf.Approximately(cam.orthographicSize, viewSize))
                cam.orthographicSize = viewSize;

            if (target == null) return;

            float minY = CurrentMinY;
            float maxY = CameraMaxY;
            if (minY > maxY) minY = maxY;

            float baseY = target.position.y + offset.y;
            float wantY;

            if (yTarget != null)
            {
                // 钩子模式：垂直跟钩子，滚轮不介入
                wantY = yTarget.position.y + offset.y;
            }
            else
            {
                // 滚轮：下滑(y<0) 视角下移。注意先把偏移夹到有效区间，避免越界后继续累加
                float scroll = Input.mouseScrollDelta.y;
                float rawWant = baseY + _scrollOffset + scroll * scrollStep;
                float clampedWant = Mathf.Clamp(rawWant, minY, maxY);
                _scrollOffset = clampedWant - baseY;   // 存回被夹住后的值，继续滚不会积攒
                wantY = clampedWant;
            }

            wantY = Mathf.Clamp(wantY, minY, maxY);

            float x = Mathf.SmoothDamp(transform.position.x, target.position.x + offset.x, ref _velocityX, smoothTime);
            transform.position = new Vector3(x, wantY, transform.position.z);
        }

        public void SetTarget(Transform t) => target = t;

        /// <summary>把垂直跟随切换到钩子；传 null 恢复跟船。</summary>
        public void SetYTarget(Transform t) => yTarget = t;

        public void SetTerrain(SandbarTerrain t) => terrain = t;
    }
}
