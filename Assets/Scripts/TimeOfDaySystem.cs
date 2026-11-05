using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 时段系统：白天 / 黄昏 / 夜晚 三个时段循环推进，
    /// 同步切换栈桥贴图、背景图和水色，并广播给刷鱼等系统。
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeOfDaySystem : MonoBehaviour
    {
        [Header("时段")]
        [SerializeField] TimeOfDay current = TimeOfDay.Day;
        [SerializeField] bool autoAdvance = true;
        [SerializeField, Min(5f)] float periodSeconds = 90f;   // 每个时段持续多久

        [Header("栈桥贴图")]
        [SerializeField] SpriteRenderer dockRenderer;
        [SerializeField] Sprite dockDay;
        [SerializeField] Sprite dockDusk;
        [SerializeField] Sprite dockNight;

        [Header("水色 / 背景")]
        [SerializeField] SeaWaterLayer water;

        public TimeOfDay Current => current;
        public bool IsNight => current == TimeOfDay.Night;
        public float PeriodProgress => periodSeconds <= 0f ? 0f : Mathf.Clamp01(_timer / periodSeconds);
        public float RemainingSeconds => Mathf.Max(0f, periodSeconds - _timer);

        public System.Action<TimeOfDay> OnChanged;

        float _timer;

        void Awake()
        {
            if (water == null) water = FindObjectOfType<SeaWaterLayer>();
            if (dockRenderer == null)
            {
                var dock = GameObject.Find("DockPier");
                if (dock != null) dockRenderer = dock.GetComponent<SpriteRenderer>();
            }
        }

        void Start()
        {
            Apply(current);
        }

        void Update()
        {
            if (!autoAdvance) return;
            if (current == TimeOfDay.Night) return;   // 入夜就停住：本局不会天亮，得开回港口结算
            _timer += Time.deltaTime;
            if (_timer >= periodSeconds)
            {
                _timer -= periodSeconds;
                Advance();
            }
        }

        public void Advance()
        {
            switch (current)
            {
                case TimeOfDay.Day: SetTime(TimeOfDay.Dusk); break;
                case TimeOfDay.Dusk: SetTime(TimeOfDay.Night); break;
                default: return;                  // 已经是夜晚：不再转回白天
            }
        }

        public void SetTime(TimeOfDay t)
        {
            current = t;
            _timer = 0f;
            Apply(t);
            Debug.Log("[时段] " + t);
            OnChanged?.Invoke(t);
        }

        void Apply(TimeOfDay t)
        {
            // 栈桥
            if (dockRenderer != null)
            {
                var s = t switch
                {
                    TimeOfDay.Dusk => dockDusk,
                    TimeOfDay.Night => dockNight,
                    _ => dockDay
                };
                if (s != null) dockRenderer.sprite = s;
            }

            // 水色 + 背景
            if (water == null) water = FindObjectOfType<SeaWaterLayer>();
            if (water != null) water.Apply(t);
        }
    }
}
