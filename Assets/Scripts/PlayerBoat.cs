using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 玩家渔船：左右移动、按朝向镜像翻转、按吃水比例沉到水面下、水面轻微起伏。
    /// 船的 sortingOrder 要小于海水层，这样吃水线以下的部分会被水色罩住。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerBoat : MonoBehaviour
    {
        [Header("外观")]
        [SerializeField] Sprite boatSprite;
        [SerializeField] string sortingLayer = "Default";
        [SerializeField] int sortingOrder = 2;       // 地形 1 之上、海水 3 之下
        [SerializeField] bool spriteFacesRight = true;

        [Header("水面")]
        [SerializeField] float waterLevelY = 16f;    // 与 SeaWaterLayer 的海平面一致
        [SerializeField, Range(0f, 1f)] float sinkRatio = 0.5f;  // 船高被水淹没的比例

        [Header("移动")]
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float minX = 10f;          // 空气墙：到 x=10 就不能再往左
        [SerializeField] float maxX = 1000000f;     // 右边不设限，一直往右开
        [SerializeField] bool keepInsideCamera = false;

        [Header("浮动")]
        [SerializeField] float bobAmplitude = 0.08f;
        [SerializeField] float bobFrequency = 1.1f;

        [Header("摇桨（没油时）")]
        [SerializeField] Sprite[] rowFrames;                 // 3 帧划桨动作
        [SerializeField, Min(0.1f)] float rowFps = 4f;
        [SerializeField, Range(0.05f, 1f)] float rowSpeedScale = 0.45f;

        SpriteRenderer _renderer;
        Camera _camera;
        QinJunActor _qinJun;
        BoatStatus _status;
        float _baseY;

        bool _battleClamp;
        float _battleMinX = float.NegativeInfinity;
        float _battleMaxX = float.PositiveInfinity;

        public SpriteRenderer Renderer => _renderer;

        /// <summary>大鱼战斗：把船限制在战斗画框里（镜头已经在战斗中锁死）。</summary>
        public void SetBattleClamp(bool on, float minX, float maxX)
        {
            _battleClamp = on;
            _battleMinX = minX;
            _battleMaxX = maxX;
        }

        void Awake()
        {
            _camera = Camera.main;
            _qinJun = GetComponentInChildren<QinJunActor>();
            _status = GetComponent<BoatStatus>();
            moveSpeed = ShopData.MotorSpeed;      // 马达升级覆盖场景配置
            Build();
        }

        void Build()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            if (boatSprite != null) _renderer.sprite = boatSprite;
            _renderer.sortingLayerName = sortingLayer;
            _renderer.sortingOrder = sortingOrder;

            // 船体按吃水比例对齐水面：水面在距船底 sinkRatio 处
            float height = _renderer.sprite != null
                ? _renderer.sprite.rect.height / _renderer.sprite.pixelsPerUnit
                : 0f;
            _baseY = waterLevelY + height * (0.5f - sinkRatio);
            var p = transform.position;
            transform.position = new Vector3(p.x, _baseY, p.z);
        }

        void Update()
        {
            if (_renderer == null) return;

            float h = Input.GetAxisRaw("Horizontal");

            // 移动端：触控按住左右 / 陀螺仪倾斜优先
            var mc = MobileControls.Instance;
            if (mc != null && mc.IsMobile && Mathf.Abs(mc.Horizontal) > 0.01f)
                h = mc.Horizontal;

            if (_status != null && _status.RunOver) h = 0f;   // 本局结束：船停在港口，等结算页
            bool noFuel = _status != null && _status.OutOfFuel;

            // 没油还能低速摇桨，别把游戏卡死
            if (noFuel) h *= rowSpeedScale;

            if (Mathf.Abs(h) > 0.01f)
            {
                var p = transform.position;
                p.x += h * moveSpeed * Time.deltaTime;
                transform.position = p;

                // 朝右航行时用原图，朝左时水平翻转；船上的秦俊要跟着一起镜像
                bool facingRight = h > 0f;
                bool flip = spriteFacesRight ? !facingRight : facingRight;
                if (_renderer.flipX != flip)
                {
                    _renderer.flipX = flip;
                    if (_qinJun != null) _qinJun.SetMirrored(flip);
                }
            }

            // 有油且在开船时马达响（没油摇桨暂时不出声）
            GameAudio.Ensure().SetEngine(!noFuel && Mathf.Abs(h) > 0.01f);

            UpdateRowAnimation(noFuel);

            // 空气墙每帧都夹一次，保证船不可能停在边界外
            var pos = transform.position;
            if (_battleClamp)
            {
                // 大鱼战斗：镜头锁死了，船只能待在战斗画框里
                pos.x = Mathf.Clamp(pos.x, Mathf.Max(minX, _battleMinX), Mathf.Min(maxX, _battleMaxX));
            }
            else
            {
                pos.x = Mathf.Clamp(pos.x, minX, maxX);
                if (keepInsideCamera && _camera != null)
                {
                    float half = _camera.orthographicSize * _camera.aspect;
                    pos.x = Mathf.Clamp(pos.x, _camera.transform.position.x - half + 1f,
                                             _camera.transform.position.x + half - 1f);
                }
            }

            // 轻微起伏：只在竖直方向做正弦漂移
            pos.y = _baseY + Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            transform.position = pos;
        }

        /// <summary>没油时船换成划桨动作帧；恢复了就切回正常船。</summary>
        void UpdateRowAnimation(bool noFuel)
        {
            if (rowFrames == null || rowFrames.Length == 0) return;

            if (noFuel)
            {
                int frame = Mathf.FloorToInt(Time.time * rowFps) % rowFrames.Length;
                _renderer.sprite = rowFrames[frame];
            }
            else if (_renderer.sprite != boatSprite && boatSprite != null)
            {
                _renderer.sprite = boatSprite;
            }
        }

        /// <summary>换一套船贴图（从工厂地图进捕鱼时换成「海神号」）。facesRight = 原图船头是否朝右。</summary>
        public void SetBoatSprite(Sprite sprite, bool facesRight = true)
        {
            if (sprite == null) return;
            boatSprite = sprite;
            spriteFacesRight = facesRight;
            Build();
        }

        /// <summary>换一种海域时重设空气墙（开阔海域没有码头，左边不用卡在 x=10）。</summary>
        public void SetSailLimits(float min, float max)
        {
            minX = min;
            maxX = Mathf.Max(min + 1f, max);
        }

        /// <summary>镜头换水位时同步调用。</summary>
        public void SetWaterLevel(float y)
        {
            waterLevelY = y;
            Build();
        }
    }
}
