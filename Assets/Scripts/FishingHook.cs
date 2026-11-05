using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Shenyan
{
    /// <summary>
    /// 鱼钩：有质量、受重力与水阻，鱼线是以竿尖为圆心、半径=线长的不可拉伸约束。
    /// 勾鱼流程：
    ///   下沉(Sinking) → 碰到小鱼减速(Slowing，2 秒内速度归零) → 加速回收(Reeling)
    ///   下沉阶段碰到大鱼直接无视，继续下沉；
    ///   回收阶段碰到大鱼会丢掉所有小鱼，只留大鱼。
    /// </summary>
    [DisallowMultipleComponent]
    public class FishingHook : MonoBehaviour
    {
        enum HookState { Sinking, Slowing, Reeling, Battle, Idle, Done }

        [Header("外观")]
        [SerializeField] Sprite hookSprite;
        [SerializeField] string sortingLayer = "Default";
        [SerializeField] int sortingOrder = 3;

        [Header("物理")]
        [SerializeField] float gravity = 16f;
        [SerializeField] float waterDrag = 1.1f;
        [SerializeField] float maxSpeed = 14f;
        [SerializeField] float lineFeedSpeed = 8f;      // 放线速度，同时作为下沉速度上限
        [SerializeField] float maxLineLength = 60f;
        [SerializeField] float initialLineLength = 0.8f;
        [SerializeField] Vector2 hookColliderSize = new Vector2(0.35f, 0.35f);

        [Header("勾鱼")]
        [SerializeField] float slowDuration = 2f;      // 减速到 0 用多久
        [SerializeField] float reelMaxSpeed = 13f;     // 收线速度（匀速，起步就是全速）
        [SerializeField] float surfaceY = 15.6f;       // 水面高度（回到这儿算收杆），也是串鱼的天花板
        [SerializeField] int maxSmallFish = 8;         // 最多串几条小鱼
        [SerializeField] float fishChainGap = 0.5f;    // 小鱼串的间距
        [SerializeField] float catchRadiusScale = 1.15f;

        [Header("负重")]
        [SerializeField] float heavyWeight = 200f;      // 到这个总重时回收方向基本竖直
        [SerializeField] float verticalBias = 0.7f;     // 重量让回收方向偏向垂直的最大幅度

        [Header("大鱼战斗（勾到大鱼时进入）")]
        [SerializeField] float battleZoneWidth = 6f;        // 鱼在船左右多少格内算「压在船下」
        [SerializeField] float battleFishDrain = 0.22f;     // 压制时每秒掉多少鱼耐力
        [SerializeField] float battleFishRegen = 0.06f;     // 脱离压制时每秒回多少
        [SerializeField] float battlePlayerDrain = 0.14f;   // 脱离压制时每秒掉多少人体力
        [SerializeField] float battlePlayerRegen = 0.05f;   // 压制时每秒回多少
        [SerializeField] float battleDragSpeed = 12f;        // 大鱼拽着线横向跑的速度
        [SerializeField] float battleSpeedJitter = 0.35f;    // 每次换向时的速度浮动（±35%）
        [SerializeField] Vector2 battleTurnInterval = new Vector2(0.35f, 0.9f);
        [SerializeField] float battleFishMargin = 2.5f;     // 鱼离画框边缘至少这么远
        [SerializeField] float battleCameraMargin = 3f;     // 相机取景在船/鱼外留多少
        [SerializeField] float battleZoomOut = 1.15f;       // 战斗时比平时拉远多少（能看到整个战场）

        [Header("召唤撞击（战斗中）")]
        [SerializeField] float summonSpeed = 30f;           // 海龟/鲸鱼冲过来的速度
        [SerializeField] float summonStartMargin = 4f;      // 从画面外多远开始冲
        [SerializeField] float turtleStunDuration = 2f;     // 海龟撞晕多久

        [Header("鱼线")]
        [SerializeField] float lineWidth = 0.07f;
        [SerializeField] int lineSortingOrder = 3;
        [SerializeField] Color lineColor = new Color(0.95f, 0.95f, 0.9f, 0.85f);

        Vector3 _velocity;
        Vector3 _slowStartVelocity;    // 勾到鱼那一刻的速度，用来做线性减速
        float _reelSpeed;              // 回收阶段当前速度
        Vector3 _anchor;
        float _lineLength;
        bool _landed;
        HookState _state = HookState.Sinking;
        float _slowTimer;

        QinJunActor _angler;
        CameraFollow _cameraFollow;
        SandbarTerrain _terrain;
        LineRenderer _line;
        BoxCollider2D _collider;

        readonly List<FishEntity> _caught = new List<FishEntity>();
        FishEntity _bigFish;
        int _intimidatedThisCast;      // 这一竿下沉时已经被「威慑」吓退了几条小鱼

        // 大鱼战斗
        float _fishStamina = 1f;
        float _playerStamina = 1f;
        float _battleDir = 1f;
        float _battleSpeed;
        float _battleTurnTimer;
        float _stunTimer;              // > 0 = 大鱼被海龟撞晕，原地不动
        Coroutine _summonRoutine;
        BoatStatus _boatStatus;
        float _battleMinX;
        float _battleMaxX;
        bool _battleView;
        BattleHud _battleHud;
        PlayerBoat _boat;

        public bool Stopped => _landed;
        public int CaughtCount => _caught.Count + (_bigFish != null ? 1 : 0);
        public string StateName => _state.ToString();
        public bool IsReeling => _state == HookState.Reeling || _state == HookState.Done;
        public bool InBattle => _state == HookState.Battle;
        public float FishStamina => _fishStamina;
        public float PlayerStamina => _playerStamina;

        /// <summary>玩家按 E 收线：立刻进入匀速回收（不销毁钩子，途中照样能勾鱼）。</summary>
        public void StartReeling()
        {
            if (_state == HookState.Reeling || _state == HookState.Done || _state == HookState.Battle) return;
            _state = HookState.Reeling;
            _reelSpeed = reelMaxSpeed;
            _slowTimer = 0f;
        }

        /// <summary>到水面时触发一次（自动回收结算用）。</summary>
        public System.Action<FishingHook> OnFinished;

        bool _settled;
        public bool Settled => _settled;
        public void MarkSettled() => _settled = true;

        /// <summary>这一杆所有鱼的总价。</summary>
        public int TotalValue
        {
            get
            {
                int v = _bigFish != null ? _bigFish.value : 0;
                foreach (var f in _caught) if (f != null) v += f.value;
                return v;
            }
        }

        public string CatchReport()
        {
            var parts = new List<string>();
            if (_bigFish != null) parts.Add(_bigFish.fishName + "(" + _bigFish.value + ")");
            foreach (var f in _caught)
                if (f != null) parts.Add(f.fishName + "(" + f.value + ")");
            return parts.Count == 0 ? "空钩" : string.Join(" + ", parts);
        }

        /// <summary>这一杆所有鱼的总重量（kg）。</summary>
        public float TotalWeight
        {
            get
            {
                float w = 0f;
                if (_bigFish != null) w += FishDatabase.Get(_bigFish.fishName).weight;
                foreach (var f in _caught)
                    if (f != null) w += FishDatabase.Get(f.fishName).weight;
                return w;
            }
        }

        /// <summary>这一杆里有没有宝箱鱼（用来开汽油桶）。</summary>
        public bool HasChestFish
        {
            get
            {
                if (_bigFish != null && _bigFish.fishName == "宝箱鱼") return true;
                foreach (var f in _caught)
                    if (f != null && f.fishName == "宝箱鱼") return true;
                return false;
            }
        }

        public struct CatchItem
        {
            public string name;
            public int value;
            public float weight;
        }

        /// <summary>这一杆的所有渔获（大鱼在前，然后是小鱼串）。</summary>
        public List<CatchItem> GetCatches()
        {
            var list = new List<CatchItem>();
            if (_bigFish != null)
                list.Add(new CatchItem
                {
                    name = _bigFish.fishName,
                    value = _bigFish.value,
                    weight = FishDatabase.Get(_bigFish.fishName).weight
                });
            foreach (var f in _caught)
            {
                if (f == null) continue;
                list.Add(new CatchItem
                {
                    name = f.fishName,
                    value = f.value,
                    weight = FishDatabase.Get(f.fishName).weight
                });
            }
            return list;
        }

        void Awake()
        {
            maxSmallFish = ShopData.MaxSmallFish;   // 鱼钩升级覆盖场景配置

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (hookSprite != null) sr.sprite = hookSprite;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
            }

            var body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;

            _collider = GetComponent<BoxCollider2D>();
            if (_collider == null) _collider = gameObject.AddComponent<BoxCollider2D>();
            _collider.size = hookColliderSize;

            _line = GetComponent<LineRenderer>();
            if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.startWidth = lineWidth;
            _line.endWidth = lineWidth;
            _line.numCapVertices = 0;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = lineColor;
            _line.endColor = lineColor;
            _line.sortingLayerName = sortingLayer;
            _line.sortingOrder = lineSortingOrder;
        }

        public void Drop(Vector3 startPos, Transform boat, QinJunActor angler,
                         SandbarTerrain terrain, CameraFollow camFollow)
        {
            _angler = angler;
            _cameraFollow = camFollow;
            _terrain = terrain;
            _anchor = angler != null ? angler.RodTipPosition : startPos;
            transform.position = startPos;

            _velocity = new Vector3(Random.Range(-0.6f, 0.6f), 1.5f, 0f);
            // 线长至少要够得着入水点：竿尖比 hookDropY 高出 3 格左右，
            // 用固定 0.8 的话第一帧就会被线约束拽到竿尖旁边，看起来是"钩子飞到半空"
            _lineLength = Mathf.Max(initialLineLength, Vector3.Distance(_anchor, startPos) + 0.05f);
            _landed = false;
            _state = HookState.Sinking;
            _slowTimer = 0f;
            _caught.Clear();
            _bigFish = null;
            _intimidatedThisCast = 0;

            UpdateLine();
            if (_cameraFollow != null) _cameraFollow.SetYTarget(transform);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_angler != null) _anchor = _angler.RodTipPosition;

            CheckFishCatch();

            switch (_state)
            {
                case HookState.Sinking: TickSinking(dt); break;
                case HookState.Slowing: TickSlowing(dt); break;
                case HookState.Reeling: TickReeling(dt); break;
                case HookState.Battle: TickBattle(dt); break;
                case HookState.Idle: _velocity = Vector3.zero; break;
                case HookState.Done: TickDone(dt); break;
            }

            ApplyLineConstraint();
            MoveAndCollide(dt);
            ArrangeCaught();

            Vector3 lineDir = _anchor - transform.position;
            if (lineDir.sqrMagnitude > 0.0004f)
            {
                float angle = Mathf.Atan2(lineDir.y, lineDir.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, 0f, angle),
                                                      1f - Mathf.Exp(-12f * dt));
            }

            UpdateLine();
        }

        // ---------------- 状态 ----------------

        void TickSinking(float dt)
        {
            if (!CheckGround()) _landed = false;
            if (!_landed) _lineLength = Mathf.Min(_lineLength + lineFeedSpeed * dt, maxLineLength);
            _velocity.y -= gravity * dt;
            ApplyDrag(dt);

            // 下沉速度压到放线速度以内：线和钩子同步下放，线就不会一帧绷一帧松，
            // 否则位置在「约束钳制」和「自由下落」之间来回跳，看起来就是一顿一顿
            if (_velocity.y < -lineFeedSpeed) _velocity.y = -lineFeedSpeed;

            // 触底不再自动回收（主人 2026-11-05 要求）：钩子就趴在海床上等着，
            // 鱼游过来照样能勾；玩家想收回时自己按 E
        }

        void TickSlowing(float dt)
        {
            // 减速阶段必须一直放线，否则线一绷紧钩子就被拽停在原地
            _lineLength = Mathf.Min(_lineLength + lineFeedSpeed * dt, maxLineLength);

            _slowTimer += dt;
            float t = Mathf.Clamp01(_slowTimer / slowDuration);

            // 按时间线性减速：不能用「每帧乘以 (1-t)²」，那样在高帧率下会瞬间归零
            _velocity = Vector3.Lerp(_slowStartVelocity, Vector3.zero, t);

            if (_slowTimer >= slowDuration)
            {
                _velocity = Vector3.zero;
                _state = HookState.Reeling;
            }
        }

        void TickReeling(float dt)
        {
            // 匀速收线（主人 2026-11-05 要求）：起步就是全速，不再加速、也不按重量减速；
            // 只有方向会随重量偏向正上方（越重越"吊"起来）
            float heavy = Mathf.Clamp01(TotalWeight / Mathf.Max(1f, heavyWeight));

            Vector3 toAnchor = _anchor - transform.position;
            if (toAnchor.sqrMagnitude > 0.0001f)
            {
                toAnchor.Normalize();
                Vector3 dir = Vector3.Lerp(toAnchor, Vector3.up, heavy * verticalBias).normalized;
                _reelSpeed = reelMaxSpeed;
                _velocity = dir * _reelSpeed;
            }

            // 线只许越收越短，不会因为钩子被拉近而变长
            _lineLength = Mathf.Max(1.2f, _lineLength - _reelSpeed * dt);

            if (transform.position.y >= surfaceY)
            {
                _velocity = Vector3.zero;
                _state = HookState.Done;
                // 到顶自动回收：通知船上来结算
                if (OnFinished != null)
                {
                    var cb = OnFinished;
                    OnFinished = null;
                    cb(this);
                }
            }
        }

        void TickDone(float dt)
        {
            _velocity = Vector3.zero;
        }

        // ---------------- 大鱼战斗 ----------------

        void BeginBattle()
        {
            _state = HookState.Battle;
            _fishStamina = 1f;
            _playerStamina = 1f;
            _stunTimer = 0f;
            _summonRoutine = null;
            _velocity = Vector3.zero;
            _battleDir = Random.value < 0.5f ? -1f : 1f;
            _battleTurnTimer = Random.Range(battleTurnInterval.x, battleTurnInterval.y);
            _battleSpeed = battleDragSpeed;

            // 线放足，否则线约束会跟"鱼拽着跑"打架
            _lineLength = maxLineLength;

            var camFollow = _cameraFollow != null ? _cameraFollow : FindObjectOfType<CameraFollow>();
            var cam = camFollow != null ? camFollow.GetComponent<Camera>() : null;
            float aspect = cam != null ? Mathf.Max(0.2f, cam.aspect) : (16f / 9f);

            Vector3 boatPos = _angler != null ? _angler.transform.position : transform.position;
            Vector3 fishPos = transform.position;

            float minX = Mathf.Min(boatPos.x, fishPos.x) - battleCameraMargin;
            float maxX = Mathf.Max(boatPos.x, fishPos.x) + battleCameraMargin;
            float minY = Mathf.Min(boatPos.y, fishPos.y) - battleCameraMargin;
            float maxY = Mathf.Max(boatPos.y, fishPos.y) + battleCameraMargin;
            float cx = (minX + maxX) * 0.5f;
            float cy = (minY + maxY) * 0.5f;
            float needSize = Mathf.Max((maxX - minX) * 0.5f / aspect, (maxY - minY) * 0.5f);
            // 战斗视野比平时拉远一点，整个战场（船 + 鱼的活动范围）都在画面里
            if (camFollow != null) needSize = Mathf.Max(needSize, camFollow.ViewSize * battleZoomOut);

            float appliedSize = needSize;
            if (camFollow != null)
                appliedSize = camFollow.SetBattleFocus(true, new Vector3(cx, cy, 0f), needSize);

            float halfH = Mathf.Max(1f, appliedSize);
            float halfW = halfH * aspect;
            _battleMinX = cx - halfW + battleFishMargin;
            _battleMaxX = cx + halfW - battleFishMargin;

            // 鱼先夹进画框
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, _battleMinX, _battleMaxX);
            transform.position = p;

            // 船也只能在画框里活动
            if (_boat == null && _angler != null) _boat = _angler.GetComponentInParent<PlayerBoat>();
            if (_boat != null) _boat.SetBattleClamp(true, cx - halfW + 2f, cx + halfW - 2f);
            if (_boatStatus == null && _boat != null) _boatStatus = _boat.GetComponent<BoatStatus>();

            if (_battleHud == null) _battleHud = FindObjectOfType<BattleHud>();
            if (_battleHud != null)
            {
                _battleHud.Show();
                _battleHud.SetStamina(1f, 1f);
            }

            _battleView = true;
            Debug.Log("[战斗] 勾到大鱼，进入战斗：把它压在船下方耗它的耐力");
        }

        void TickBattle(float dt)
        {
            // 被海龟撞晕：原地不动，这 2 秒算"被压制"
            bool stunned = _stunTimer > 0f;
            if (stunned) _stunTimer -= dt;

            Vector3 p = transform.position;
            if (!stunned)
            {
                // 大鱼拽着线随机左右跑，但不会跑出画框
                _battleTurnTimer -= dt;
                if (_battleTurnTimer <= 0f)
                {
                    _battleDir = Random.value < 0.5f ? -1f : 1f;
                    _battleTurnTimer = Random.Range(battleTurnInterval.x, battleTurnInterval.y);
                    // 每段冲刺速度略有浮动，挣扎起来更凶
                    _battleSpeed = battleDragSpeed * Random.Range(1f - battleSpeedJitter, 1f + battleSpeedJitter);
                }

                p.x += _battleDir * _battleSpeed * dt;
                if (p.x <= _battleMinX) { p.x = _battleMinX; _battleDir = 1f; }
                if (p.x >= _battleMaxX) { p.x = _battleMaxX; _battleDir = -1f; }
                transform.position = p;

                // 贴图朝向跟着挣扎方向走（原图默认朝右，向左跑时水平翻转）
                if (_bigFish != null)
                {
                    var fishRenderer = _bigFish.GetComponent<SpriteRenderer>();
                    if (fishRenderer != null) fishRenderer.flipX = _battleDir < 0f;
                }
            }
            _velocity = Vector3.zero;

            // 鱼是不是压在船下方的那一段（晕眩时算压制）
            float boatX = _angler != null ? _angler.transform.position.x : p.x;
            bool under = stunned || Mathf.Abs(p.x - boatX) <= battleZoneWidth;

            if (under)
            {
                _fishStamina -= battleFishDrain * dt;
                _playerStamina += battlePlayerRegen * dt;
            }
            else
            {
                _fishStamina += battleFishRegen * dt;
                _playerStamina -= battlePlayerDrain * dt;
            }
            _fishStamina = Mathf.Clamp01(_fishStamina);
            _playerStamina = Mathf.Clamp01(_playerStamina);

            if (_battleHud != null)
            {
                _battleHud.SetStamina(_fishStamina, _playerStamina);
                _battleHud.SetSummon(SummonHint());
            }

            if (_fishStamina <= 0f) EndBattle(true);
            else if (_playerStamina <= 0f) EndBattle(false);
        }

        // ---------------- 召唤撞击 ----------------

        string SummonHint()
        {
            if (_boatStatus == null) return "";
            string s = "Q 海龟 ×" + _boatStatus.TurtleSummons;
            if (_boatStatus.WhaleUnlocked) s += "    R 鲸鱼 ×" + _boatStatus.WhaleSummons;
            return s;
        }

        /// <summary>战斗中按 Q：从画面外召唤海龟撞过来，大鱼晕眩 2 秒。</summary>
        public void SummonTurtle()
        {
            if (_state != HookState.Battle || _summonRoutine != null) return;
            if (_boatStatus == null || !_boatStatus.TryUseTurtleSummon())
            {
                Debug.Log("[召唤] 海龟次数用完了，回码头恢复");
                return;
            }
            _summonRoutine = StartCoroutine(StrikeRoutine("Image/鱼_像素/海龟", 1.4f, true));
        }

        /// <summary>战斗中按 R（天赋解锁后）：召唤鲸鱼撞过来，直接让目标力竭。</summary>
        public void SummonWhale()
        {
            if (_state != HookState.Battle || _summonRoutine != null) return;
            if (_boatStatus == null || !_boatStatus.TryUseWhaleSummon())
            {
                Debug.Log("[召唤] 鲸鱼还没解锁（或次数用完了）");
                return;
            }
            _summonRoutine = StartCoroutine(StrikeRoutine("Image/鱼_像素/小鲸鱼", 2.2f, false));
        }

        System.Collections.IEnumerator StrikeRoutine(string spritePath, float scale, bool stun)
        {
            var sprite = Resources.Load<Sprite>(spritePath);
            var cam = Camera.main;
            float halfW = cam != null ? cam.orthographicSize * Mathf.Max(0.2f, cam.aspect) : 20f;
            float cx = cam != null ? cam.transform.position.x : transform.position.x;

            bool fromLeft = Random.value < 0.5f;
            var go = new GameObject(stun ? "TurtleStrike" : "WhaleStrike");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder + 6;      // 飞过来时盖住鱼
            sr.flipX = !fromLeft;                     // 鱼图默认朝右，从左边来就朝右
            go.transform.localScale = Vector3.one * scale;
            go.transform.position = new Vector3(cx + (fromLeft ? -(halfW + summonStartMargin) : (halfW + summonStartMargin)),
                                                transform.position.y, 0f);

            while (go != null && Vector3.Distance(go.transform.position, transform.position) > 0.8f)
            {
                go.transform.position = Vector3.MoveTowards(go.transform.position, transform.position,
                                                            summonSpeed * Time.deltaTime);
                yield return null;
            }
            if (go != null) Destroy(go);

            if (_state == HookState.Battle)
            {
                if (stun)
                {
                    _stunTimer = AbilityData.TurtleStunDuration;
                    Debug.Log("[召唤] 海龟命中，大鱼晕眩 " + AbilityData.TurtleStunDuration + " 秒");
                }
                else
                {
                    // 鲸鱼：按异能等级削掉耐力（2 级直接归零）
                    float dmg = AbilityData.WhaleDamageRatio;
                    _fishStamina = Mathf.Max(0f, _fishStamina - dmg);
                    Debug.Log("[召唤] 鲸鱼命中，削掉 " + (dmg * 100f).ToString("0") +
                              "% 耐力（剩 " + (_fishStamina * 100f).ToString("0") + "%）");
                }
            }
            _summonRoutine = null;
        }

        void EndBattle(bool win)
        {
            RestoreBattleView();

            if (win)
            {
                // 鱼力竭：自动收线把它拉回来（不用再按 E）
                _state = HookState.Reeling;
                _reelSpeed = reelMaxSpeed;
                _slowTimer = 0f;
                _velocity = Vector3.zero;
                Debug.Log("[战斗] 大鱼力竭，自动收线");
            }
            else
            {
                Debug.Log("[战斗] 体力耗尽，鱼线断了");
                CutLine();
            }
        }

        void RestoreBattleView()
        {
            if (!_battleView) return;
            _battleView = false;

            var camFollow = _cameraFollow != null ? _cameraFollow : FindObjectOfType<CameraFollow>();
            if (camFollow != null) camFollow.SetBattleFocus(false, Vector3.zero, 0f);

            if (_boat == null && _angler != null) _boat = _angler.GetComponentInParent<PlayerBoat>();
            if (_boat != null) _boat.SetBattleClamp(false, 0f, 0f);

            if (_battleHud != null) _battleHud.Hide();
        }

        /// <summary>
        /// 剪断鱼线（战斗中按 E）：挂着的鱼全部放回水里正常巡游，钩子销毁。
        /// </summary>
        public void CutLine()
        {
            foreach (var f in _caught)
            {
                if (f == null) continue;
                NudgeIntoWater(f.transform);
                f.hooked = false;
                f.consumeRequested = false;
                f.releaseRequested = true;
            }
            if (_bigFish != null)
            {
                NudgeIntoWater(_bigFish.transform);
                _bigFish.hooked = false;
                _bigFish.consumeRequested = false;
                _bigFish.releaseRequested = true;
            }
            _caught.Clear();
            _bigFish = null;

            RestoreBattleView();
            Destroy(gameObject);
        }

        /// <summary>把鱼挪回水里，免得被鱼群系统判成"出水"直接收进池子。</summary>
        void NudgeIntoWater(Transform t)
        {
            if (t == null) return;
            Vector3 p = t.position;
            if (_terrain != null && _terrain.TryGetGroundTopY(p.x, out float floorY))
                p.y = Mathf.Max(p.y, floorY + 0.6f);
            p.y = Mathf.Min(p.y, surfaceY - 0.6f);
            t.position = p;
            Physics2D.SyncTransforms();
        }

        void ApplyDrag(float dt)
        {
            _velocity *= Mathf.Clamp01(1f - waterDrag * dt);
            if (_velocity.magnitude > maxSpeed) _velocity = _velocity.normalized * maxSpeed;
        }

        // ---------------- 勾鱼 ----------------

        void CheckFishCatch()
        {
            // 战斗期间不判定其它鱼（主人要求：期间不会再和其它鱼碰撞）
            if (_state == HookState.Battle) return;

            var hits = Physics2D.OverlapBoxAll(transform.position,
                                               hookColliderSize * catchRadiusScale, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var fish = h.GetComponent<FishEntity>();
                if (fish == null || fish.hooked) continue;

                if (_state == HookState.Reeling)
                {
                    if (fish.IsBig)
                    {
                        // 战斗结束后的收线回程：已经挂着的那条大鱼不能被新的大鱼挤掉
                        // （否则旧鱼被销毁、还会莫名其妙再打一场；主人 2026-11-05 反馈）
                        if (_bigFish != null) continue;
                        CatchBig(fish);
                    }
                    else CatchSmall(fish);
                }
                else if (_state == HookState.Sinking || _state == HookState.Slowing)
                {
                    // 空钩（或刚开始下沉）碰到大鱼：无视，继续下沉
                    if (fish.IsBig) continue;

                    // 威慑：这一竿下沉时先吓退 N 条小鱼（每条只被吓一次）
                    if (!fish.intimidated && _intimidatedThisCast < AbilityData.IntimidateSkip)
                    {
                        fish.intimidated = true;
                        _intimidatedThisCast++;
                        continue;
                    }

                    CatchSmall(fish);
                }
            }
        }

        void CatchSmall(FishEntity fish)
        {
            if (_bigFish != null) return;                 // 已经挂着大鱼了，小的不要
            if (_caught.Count >= maxSmallFish) return;

            fish.gameObject.SetActive(true);              // 万一这一帧正好被鱼群系统隐藏了
            fish.hooked = true;
            fish.releaseRequested = false;
            _caught.Add(fish);

            if (_state == HookState.Sinking || _state == HookState.Slowing)
            {
                if (_state == HookState.Sinking)
                {
                    // 记下勾住瞬间的下沉速度，之后 2 秒里线性降到 0
                    _slowStartVelocity = _velocity;
                    _slowTimer = 0f;
                }
                _state = HookState.Slowing;
            }
        }

        void CatchBig(FishEntity fish)
        {
            // 丢掉所有小鱼，只挂大鱼。主人 2026-11-05 要求：这些小鱼直接销毁，
            // 不再放回水里（否则被挤掉的鱼会继续在钩子旁边游，看着像没勾住）
            foreach (var small in _caught)
            {
                if (small == null) continue;
                small.hooked = false;
                small.releaseRequested = false;
                small.consumeRequested = true;
            }
            _caught.Clear();

            if (_bigFish != null)
            {
                _bigFish.hooked = false;
                _bigFish.releaseRequested = false;
                _bigFish.consumeRequested = true;
            }
            _bigFish = fish;
            fish.gameObject.SetActive(true);
            fish.releaseRequested = false;
            fish.hooked = true;

            // 勾到大鱼 → 进入战斗：不回收，被它拽着跑，等玩家把它的耐力耗光
            BeginBattle();
        }

        void ArrangeCaught()
        {
            Vector3 p = transform.position;

            // 串鱼的天花板是水面、地板是海床：钩子放到贴着船底时，
            // 往上串的鱼会直接"飞"到半空（主人 2026-11-04 反馈），所以放不下的就往钩子下面串
            float top = surfaceY - 0.35f;
            float bottom = p.y - 8f;
            if (_terrain != null && _terrain.TryGetGroundTopY(p.x, out float floor))
                bottom = Mathf.Max(bottom, floor + 0.4f);
            if (bottom > top) bottom = top - 0.5f;

            if (_bigFish != null && _bigFish.transform != null)
            {
                _bigFish.transform.position = new Vector3(p.x, Mathf.Clamp(p.y + 0.4f, bottom, top), 0f);
                return;
            }

            for (int i = 0; i < _caught.Count; i++)
            {
                var f = _caught[i];
                if (f == null) continue;
                float offset = fishChainGap * (i + 1);
                float y = p.y + offset;
                if (y > top) y = p.y - offset;      // 上面放不下 → 改挂到钩子下面
                f.transform.position = new Vector3(p.x, Mathf.Clamp(y, bottom, top), 0f);
            }
        }

        // ---------------- 物理 ----------------

        void ApplyLineConstraint()
        {
            Vector3 rel = transform.position - _anchor;
            float dist = rel.magnitude;
            if (dist > _lineLength && dist > 0.0001f)
            {
                Vector3 n = rel / dist;
                transform.position = _anchor + n * _lineLength;
                float radial = Vector3.Dot(_velocity, n);
                if (radial > 0f) _velocity -= n * radial;
            }
        }

        void MoveAndCollide(float dt)
        {
            Vector3 next = transform.position + _velocity * dt;
            Vector3 step = next - transform.position;
            float stepLen = step.magnitude;

            if (stepLen > 0.0001f)
            {
                var hits = Physics2D.RaycastAll(transform.position, step / stepLen, stepLen + 0.05f);
                foreach (var h in hits)
                {
                    if (h.collider == null || h.collider == _collider) continue;
                    if (h.collider.GetComponent<TilemapCollider2D>() == null &&
                        h.collider.GetComponent<CompositeCollider2D>() == null) continue;

                    // 任何状态都不许钻进海床。以前减速/回收阶段直接跳过地面判定，
                    // 结果勾到鱼后那 2 秒会带着下沉速度一路扎进地里（主人反馈"钩子穿过海床"）
                    next = (Vector3)h.point - step / stepLen * 0.05f;
                    if (_state == HookState.Sinking)
                    {
                        _velocity = Vector3.zero;
                        _landed = true;
                    }
                    else if (_velocity.y < 0f)
                    {
                        _velocity.y = 0f;      // 减速阶段只止住下沉，状态机照走
                    }
                    break;
                }
            }

            // 兜底：raycast 从"已经在地里"的位置出发可能打不到碰撞面，
            // 直接查这一列的海床顶面，把钩子夹在地面之上
            if (_terrain != null && _terrain.TryGetGroundTopY(next.x, out float topY))
            {
                float minY = topY + hookColliderSize.y * 0.5f;
                if (next.y < minY)
                {
                    next.y = minY;
                    if (_velocity.y < 0f) _velocity.y = 0f;
                    _landed = true;
                }
            }

            transform.position = next;
        }

        bool CheckGround()
        {
            foreach (var h in Physics2D.RaycastAll(transform.position, Vector2.down, 0.45f))
            {
                if (h.collider == null || h.collider == _collider) continue;
                if (h.collider.GetComponent<TilemapCollider2D>() != null ||
                    h.collider.GetComponent<CompositeCollider2D>() != null)
                    return true;
            }
            return false;
        }

        void UpdateLine()
        {
            if (_line == null) return;
            _line.SetPosition(0, _anchor);
            _line.SetPosition(1, transform.position);
        }

        public void Reel()
        {
            // 这一竿的鱼已经装船了：置 consumeRequested，由 FishSpawner 收进池子。
            // 不能只 SetActive(false)——鱼群系统会把它当空位重新激活；
            // 也不能用 releaseRequested——那会把鱼"放回水里"，看起来就是钓上来还在原地游。
            foreach (var f in _caught)
            {
                if (f == null) continue;
                f.hooked = false;
                f.releaseRequested = false;
                f.consumeRequested = true;
            }
            if (_bigFish != null)
            {
                _bigFish.hooked = false;
                _bigFish.releaseRequested = false;
                _bigFish.consumeRequested = true;
            }
            _caught.Clear();
            _bigFish = null;

            Destroy(gameObject);
        }

        void OnDestroy()
        {
            RestoreBattleView();
            if (_cameraFollow != null) _cameraFollow.SetYTarget(null);
        }
    }
}
