using System.Collections;
using UnityEngine;

namespace Shenyan
{
    public enum QinJunPose
    {
        Sit,      // 坐：压在船上面
        Pull,     // 拉拽
        Raise,    // 推举
        Crouch,   // 蹲
        HoldRod,  // 握竿
        Cast1,    // 甩竿 1
        Cast2,    // 甩竿 2
        Reel      // 横拉
    }

    /// <summary>
    /// 秦俊（船的子物体）：每个姿势一套 localPosition / localScale / 排序。
    /// E 键：甩竿两帧 → 保持握竿；再按一次解除回坐。
    /// Q 键按住：循环 蹲 → 拉拽 → 推举；松开回坐。
    /// 船左右翻转时会跟着镜像（位置和缩放都取反）。
    /// </summary>
    [DisallowMultipleComponent]
    public class QinJunActor : MonoBehaviour
    {
        [Header("动作精灵")]
        [SerializeField] Sprite poseSit;
        [SerializeField] Sprite posePull;
        [SerializeField] Sprite poseRaise;
        [SerializeField] Sprite poseCrouch;
        [SerializeField] Sprite poseHoldRod;
        [SerializeField] Sprite poseCast1;
        [SerializeField] Sprite poseCast2;
        [SerializeField] Sprite poseReel;

        [Header("当前姿势")]
        [SerializeField] QinJunPose pose = QinJunPose.Sit;

        [Header("排序：坐姿要在船之上，其他在船之下")]
        [SerializeField] int sitSortingOrder = 3;
        [SerializeField] int hiddenSortingOrder = 1;

        [Header("按键")]
        [SerializeField] KeyCode castKey = KeyCode.F;          // 下钩 / 收线（也可用鼠标左键）
        [SerializeField] KeyCode tossKey = KeyCode.Q;          // 投雷（也可用鼠标右键）
        [SerializeField] KeyCode baitKey = KeyCode.E;          // 洒饵（也可用鼠标中键）
        [SerializeField] KeyCode whaleSummonKey = KeyCode.R;   // 天赋解锁的鲸鱼召唤
        [SerializeField] float castFrameGap = 0.18f;
        [SerializeField] float castHoldDelay = 0.25f;
        [SerializeField] float tossLoopGap = 0.22f;
        [SerializeField] bool debugKeys = false;

        [Header("钓鱼钩子")]
        [SerializeField] Sprite hookSprite;
        [SerializeField] float hookDropY = 16f;          // 钩子入水高度

        [Header("船况")]
        [SerializeField] BoatStatus boatStatus;
        [SerializeField, Range(0f, 1f)] float chestOilChance = 0.4f;   // 宝箱鱼开出汽油桶的概率
        [SerializeField] float chestOilAmount = 25f;                   // 一桶油补多少

        [Header("收获提示")]
        [SerializeField] CatchFeedUI catchFeed;

        const string SpriteFolder = "Image/角色/秦俊动作/";

        SpriteRenderer _renderer;
        Coroutine _routine;
        FishingHook _hook;
        bool _fishing;     // 已甩竿、保持握竿中
        bool _tossing;     // 按住 Q 循环中
        bool _mirrored;    // 随船镜像
        bool _ePrev;       // 上一帧 E 是否按下
        int _money;        // 累计收入（钓上来的鱼自动卖掉）
        bool _qPrev;       // 上一帧 Q 是否按下
        bool _rPrev;       // 上一帧 R 是否按下
        bool _bPrev;       // 上一帧 E（洒饵）是否按下
        FloatingHint hint; // 头顶飘字

        public QinJunPose Pose => pose;
        public bool IsFishing => _fishing;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            LoadSpritesIfMissing();
            Apply();

            // 提示从船上冒出来，不跟着人物（主人 2026-11-05 指定）
            var boatTf = transform.parent != null ? transform.parent : transform;
            hint = boatTf.GetComponent<FloatingHint>();
            if (hint == null) hint = boatTf.gameObject.AddComponent<FloatingHint>();
        }

        void Update()
        {
            // 本局结束了（船靠港）：收杆、停手，等结算页
            if (boatStatus == null) boatStatus = GetComponentInParent<BoatStatus>();
            if (boatStatus != null && boatStatus.RunOver)
            {
                if (_tossing) { StopRoutine(); _tossing = false; SetPose(QinJunPose.Sit); }
                if (_fishing) { StopRoutine(); _fishing = false; SetPose(QinJunPose.Sit); }
                if (_hook != null) ReelHook();
                _qPrev = false;
                _ePrev = false;
                return;
            }

            // 用 GetKey + 上一帧状态做边沿判断：GetKeyDown 依赖游戏视图的事件队列，
            // GameView 没焦点时收不到；GetKey 直接读键盘状态，和横向移动是同一套机制。
            // 移动端**不能**读鼠标键：触摸屏会模拟左键，按屏幕左右两侧移动时会连带触发下钩。
            bool mouse = !Application.isMobilePlatform;
            bool e = Input.GetKey(castKey) || (mouse && Input.GetMouseButton(0));      // 下钩
            bool q = Input.GetKey(tossKey) || (mouse && Input.GetMouseButton(1));      // 投雷
            bool b = Input.GetKey(baitKey) || (mouse && Input.GetMouseButton(2));      // 洒饵
            bool r = Input.GetKey(whaleSummonKey);
            bool inBattle = _hook != null && _hook.InBattle;

            if (q && !_qPrev)
            {
                if (debugKeys) Debug.Log("[秦俊] 投雷键");
                if (inBattle) _hook.SummonTurtle();    // 战斗中 Q = 召唤海龟撞击
                else { PlayTossOnce(); UseTorpedo(); } // 平时 = 抛洒动作 + 丢鱼雷
            }
            if (b && !_bPrev)
            {
                if (debugKeys) Debug.Log("[秦俊] 洒饵键");
                PlayTossOnce(true);                     // 洒饵跟满仓无关，照样播动作
                UseBait();
            }
            if (r && !_rPrev && inBattle) _hook.SummonWhale();   // 战斗中 R = 召唤鲸鱼（天赋解锁后）
            if (e && !_ePrev) { if (debugKeys) Debug.Log("[秦俊] 下钩键, fishing=" + _fishing); ToggleCast(); }

            _qPrev = q;
            _bPrev = b;
            _rPrev = r;
            _ePrev = e;
        }

        [ContextMenu("应用当前姿势")]
        public void Apply()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) return;

            GetPoseData(out var sprite, out var pos, out float scale, out int order);
            if (sprite != null) _renderer.sprite = sprite;
            ApplyTransform(pos, scale, order);
        }

        /// <summary>只同步位置/缩放/排序，不动精灵（镜像时用，避免把选好的图换掉）。</summary>
        void ApplyTransformOnly()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) return;
            GetPoseData(out _, out var pos, out float scale, out int order);
            ApplyTransform(pos, scale, order);
        }

        void ApplyTransform(Vector3 pos, float scale, int order)
        {
            // 船朝右时整体镜像：位置 x 取反、缩放 x 取反
            transform.localPosition = _mirrored ? new Vector3(-pos.x, pos.y, pos.z) : pos;
            float sx = _mirrored ? -scale : scale;
            transform.localScale = new Vector3(sx, scale, 1f);
            _renderer.sortingOrder = order;
        }

        void GetPoseData(out Sprite sprite, out Vector3 pos, out float scale, out int order)
        {
            switch (pose)
            {
                case QinJunPose.Sit:
                    sprite = poseSit; pos = new Vector3(-2.71f, 0.43f, 0f); scale = 0.8f; order = sitSortingOrder; break;
                case QinJunPose.Pull:
                    sprite = posePull; pos = new Vector3(0f, 0.6f, 0f); scale = 0.8f; order = hiddenSortingOrder; break;
                case QinJunPose.Raise:
                    sprite = poseRaise; pos = new Vector3(0f, 0.6f, 0f); scale = 0.8f; order = hiddenSortingOrder; break;
                case QinJunPose.Crouch:
                    sprite = poseCrouch; pos = new Vector3(0f, 0.16f, 0f); scale = 0.6f; order = hiddenSortingOrder; break;
                case QinJunPose.HoldRod:
                    sprite = poseHoldRod; pos = new Vector3(0f, 0.95f, 0f); scale = 0.9f; order = hiddenSortingOrder; break;
                case QinJunPose.Cast1:
                    sprite = poseCast1; pos = new Vector3(0f, 0.95f, 0f); scale = 0.9f; order = hiddenSortingOrder; break;
                case QinJunPose.Cast2:
                    sprite = poseCast2; pos = new Vector3(0f, 0.95f, 0f); scale = 0.9f; order = hiddenSortingOrder; break;
                default: // Reel
                    sprite = poseReel; pos = new Vector3(0f, 0.95f, 0f); scale = 0.9f; order = hiddenSortingOrder; break;
            }
        }

        public void SetPose(QinJunPose next)
        {
            pose = next;
            Apply();
        }

        /// <summary>船水平翻转时同步镜像（只动位置和缩放，不换精灵）。</summary>
        public void SetMirrored(bool mirrored)
        {
            if (_mirrored == mirrored) return;
            _mirrored = mirrored;
            ApplyTransformOnly();
        }

        /// <summary>一次完整抛竿：甩竿1 → 甩竿2 → 保持握竿。</summary>
        [ContextMenu("播放抛竿动作")]
        public void PlayCast()
        {
            StopRoutine();
            _routine = StartCoroutine(CastRoutine());
        }

        void ToggleCast()
        {
            if (_tossing) return;

            // 只要钩子还在（或者自认为在钓鱼），就按「收线 / 剪线」处理。
            // 不能只看 _fishing：万一状态被别的动作清掉，会把钩子误判成"还没甩竿"而重新甩一竿。
            if (_fishing || _hook != null)
            {
                // 战斗中按 E = 剪断鱼线（大鱼放回水里）
                if (_hook != null && _hook.InBattle)
                {
                    _hook.CutLine();
                    _hook = null;
                    _fishing = false;
                    SetPose(QinJunPose.Sit);
                    return;
                }

                // 按 E 收线：钩子匀速往回收（不瞬间结算），途中照样能挂鱼；
                // 到水面才走 FinishHook 结算（主人 2026-11-05 要求）
                if (_hook != null)
                {
                    if (!_hook.IsReeling)
                    {
                        StopRoutine();
                        _hook.StartReeling();
                        SetPose(QinJunPose.Reel);
                    }
                }
                else if (_routine != null)
                {
                    // 甩竿动画还没走完（钩子尚未生成）：这次按键不算收线。
                    // 否则会在动画中途把 _fishing 清掉，动画结束后又多甩一竿，
                    // 看起来就是「按 E 直接瞬移回来还甩下一钩」（主人 2026-11-05 反馈）
                }
                else
                {
                    _fishing = false;
                    SetPose(QinJunPose.Sit);
                }
                return;
            }

            // 船舱满了要先回码头卸货
            if (boatStatus == null) boatStatus = GetComponentInParent<BoatStatus>();
            if (boatStatus != null && boatStatus.LoadFull)
            {
                if (hint != null) hint.Show("船舱满了，先回码头卸货");
                Debug.Log("[秦俊] 船舱满了，先开回码头卸货");
                return;
            }

            StopRoutine();
            _routine = StartCoroutine(CastRoutine());
        }

        IEnumerator CastRoutine()
        {
            _fishing = true;
            ReelHook();

            // 新的一竿，连击清零
            if (catchFeed == null) catchFeed = FindObjectOfType<CatchFeedUI>();
            if (catchFeed != null) catchFeed.ResetCombo();

            SetPose(QinJunPose.Cast1);
            yield return new WaitForSeconds(castFrameGap);
            SetPose(QinJunPose.Cast2);
            yield return new WaitForSeconds(castHoldDelay);
            SetPose(QinJunPose.HoldRod);
            ThrowHook();
            _routine = null;
        }

        /// <summary>甩竿完成：从船头外侧丢一个钩子下去，相机跟随它下沉。</summary>
        void ThrowHook()
        {
            if (hookSprite == null) hookSprite = Resources.Load<Sprite>("Image/道具/钩子");

            var go = new GameObject("FishingHook");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = hookSprite;
            go.AddComponent<FishingHook>();

            var hook = go.GetComponent<FishingHook>();
            _hook = hook;

            // 钩子从船中心入水（船 sprite 的中心就是父对象的位置）
            Transform boat = transform.parent;
            float centerX = boat != null ? boat.position.x : transform.position.x;
            var start = new Vector3(centerX, hookDropY, 0f);

            hook.Drop(start, boat, this, FindObjectOfType<SandbarTerrain>(), FindObjectOfType<CameraFollow>());
            GameAudio.Ensure().PlaySplash();

            // 到顶自动回收结算
            hook.OnFinished = h => FinishHook(h);
        }

        /// <summary>收杆后把这一杆的鱼装船（还没卖钱），宝箱鱼有概率开出汽油桶。</summary>
        void FinishHook(FishingHook hook)
        {
            if (hook == null) return;

            if (!hook.Settled)
            {
                hook.MarkSettled();
                int value = hook.TotalValue;
                float weight = hook.TotalWeight;

                // 收获消息不一次弹完，每 0.2 秒滚一条（协程在钩子销毁后仍能播）
                if (catchFeed == null) catchFeed = FindObjectOfType<CatchFeedUI>();
                var catches = hook.GetCatches();

                if (boatStatus == null) boatStatus = GetComponentInParent<BoatStatus>();
                if (boatStatus != null)
                {
                    // 只有一条鱼时把名字也传过去：空船一竿满仓时船上要直接显示这条鱼
                    string single = catches.Count == 1 ? catches[0].name : null;
                    boatStatus.AddCatch(value, weight, hook.CaughtCount, single);
                }

                // 经验：连击越高指数增长（本竿第几条决定倍率）
                int expGain = 0;
                int ups = 0;
                for (int i = 0; i < catches.Count; i++)
                {
                    expGain += PlayerProgress.ExpForCombo(i + 1);
                    ups += PlayerProgress.GainExpForCombo(i + 1);
                }
                if (catches.Count > 0)
                    Debug.Log("[经验] 本竿 " + catches.Count + " 条，连击加成 +" + expGain + " 经验" +
                              (ups > 0 ? "，升 " + ups + " 级（异能点 " + PlayerProgress.SkillPoints + "）" : ""));

                if (catchFeed != null && catches.Count > 0)
                    StartCoroutine(PlayCatchFeed(catches));

                Debug.Log("[装船] " + hook.CatchReport() + "，" + weight.ToString("0.0") + "kg" +
                          "（回码头才结算）");

                if (hook.HasChestFish && Random.value < chestOilChance)
                {
                    if (boatStatus != null) boatStatus.AddFuel(chestOilAmount);
                    Debug.Log("[宝箱鱼] 开出汽油桶，+" + chestOilAmount + " 油");
                }
            }

            if (_hook == hook) _hook = null;
            _fishing = false;
            SetPose(QinJunPose.Sit);
            hook.Reel();
        }

        /// <summary>一条一条地弹收获消息，间隔 0.2 秒，每条都把连击数滚一次。</summary>
        IEnumerator PlayCatchFeed(System.Collections.Generic.List<FishingHook.CatchItem> catches)
        {
            for (int i = 0; i < catches.Count; i++)
            {
                if (catchFeed == null) yield break;
                catchFeed.Push(catches[i].name, catches[i].weight);
                yield return new WaitForSeconds(0.2f);
            }
        }

        /// <summary>竿尖位置（按握竿 sprite 实测：中心偏 (-2, +30) 像素），供鱼线连接用。</summary>
        public Vector3 RodTipPosition
        {
            get
            {
                // 16 PPU、scale 0.9 换算成世界单位；镜像时 x 偏移取反
                float dir = _mirrored ? -1f : 1f;
                Vector3 offset = new Vector3(dir * -0.113f, 1.688f, 0f);
                return transform.position + offset;
            }
        }

        void ReelHook()
        {
            if (_hook != null)
            {
                // 提前收杆也要结算（E 键收线）
                FinishHook(_hook);
                return;
            }
            _hook = null;
        }

        public int Money => _money;

        /// <summary>鱼雷：在船下方炸一片，范围内的鱼直接算上船，但卖价只算一半。</summary>
        void UseTorpedo()
        {
            if (ShopData.Torpedoes <= 0)
            {
                if (hint != null) hint.Show("没有鱼雷了，去码头商店买");
                return;
            }

            if (boatStatus == null) boatStatus = GetComponentInParent<BoatStatus>();
            if (boatStatus != null && boatStatus.LoadFull)
            {
                if (hint != null) hint.Show("船舱满了，先回码头卸货");
                return;
            }

            ShopData.Torpedoes--;

            Vector3 center = transform.position + Vector3.down * 3f;
            var hits = Physics2D.OverlapCircleAll(center, 7f);
            int count = 0;
            int value = 0;
            float weight = 0f;

            foreach (var h in hits)
            {
                if (h == null) continue;
                var fish = h.GetComponent<FishEntity>();
                if (fish == null || fish.hooked) continue;

                fish.releaseRequested = false;
                fish.consumeRequested = true;
                count++;
                value += Mathf.RoundToInt(fish.value * 0.5f);
                weight += FishDatabase.Get(fish.fishName).weight;
            }

            if (count == 0)
            {
                if (hint != null) hint.Show("这一炮没炸到鱼");
                return;
            }

            if (boatStatus != null) boatStatus.AddCatch(value, weight, count, null);
            if (hint != null) hint.Show("鱼雷炸到 " + count + " 条，+" + value + " 金（半价）");
            Debug.Log("[鱼雷] 炸到 " + count + " 条 / " + weight.ToString("0.0") + "kg，+" + value + " 金");
        }
        /// <summary>洒饵：消耗一个，短时间把附近的鱼吸到船下方。</summary>
        void UseBait()
        {
            if (ShopData.Baits <= 0)
            {
                if (hint != null) hint.Show("没有饵料了，去码头商店买");
                return;
            }

            ShopData.Baits--;

            var spawner = FindObjectOfType<FishSpawner>();
            Vector3 center = transform.position + Vector3.down * 4f;
            if (spawner != null) spawner.AttractFish(center, 15f, 7f);

            if (hint != null) hint.Show("撒了把饵料，鱼群聚过来了（剩 " + ShopData.Baits + "）");
            Debug.Log("[饵料] 吸引鱼群，剩 " + ShopData.Baits);
        }

        /// <summary>移动端：中间轻点 = 下杆 / 收杆 / 战斗中剪线。</summary>
        public void MobileCast()
        {
            if (_tossing) return;
            ToggleCast();
        }

        /// <summary>移动端：鱼雷按钮（战斗中改成召唤海龟）。</summary>
        public void MobileTorpedo()
        {
            if (_hook != null && _hook.InBattle) { _hook.SummonTurtle(); return; }
            PlayTossOnce();
            UseTorpedo();
        }

        /// <summary>移动端：饵料按钮（战斗中改成召唤鲸鱼）。</summary>
        public void MobileBait()
        {
            if (_hook != null && _hook.InBattle) { _hook.SummonWhale(); return; }
            PlayTossOnce(true);
            UseBait();
        }

        /// <summary>是否在打大鱼（给移动端按钮换文字用）。</summary>
        public bool InBattleHint => _hook != null && _hook.InBattle;

        /// <summary>抛洒一次：蹲 → 拉拽 → 推举，播完回坐（不循环）。满仓按不了（洒饵除外）。</summary>
        void PlayTossOnce(bool ignoreLoadFull = false)
        {
            if (_tossing) return;

            if (!ignoreLoadFull)
            {
                if (boatStatus == null) boatStatus = GetComponentInParent<BoatStatus>();
                if (boatStatus != null && boatStatus.LoadFull)
                {
                    if (hint != null) hint.Show("船舱满了，先回码头卸货");
                    Debug.Log("[秦俊] 船舱满了，抛不了");
                    return;
                }
            }

            StopRoutine();
            _routine = StartCoroutine(TossOnceRoutine());
        }

        IEnumerator TossOnceRoutine()
        {
            // 抛洒只是动作，不能动钓鱼状态：钓着鱼 / 战斗中也可能按投雷、洒饵，
            // 之前这里把 _fishing 清成 false，之后按收杆键就走进"还没甩竿"分支，
            // 把正在战斗的钩子直接结算 + 又甩一竿（主人 2026-11-05 复现）
            bool wasFishing = _fishing;
            _tossing = true;

            SetPose(QinJunPose.Crouch);
            yield return new WaitForSeconds(tossLoopGap);
            SetPose(QinJunPose.Pull);
            yield return new WaitForSeconds(tossLoopGap);
            SetPose(QinJunPose.Raise);
            yield return new WaitForSeconds(tossLoopGap);

            _tossing = false;
            _fishing = wasFishing;

            // 动作播完回到当前该有的姿势
            if (_hook != null && _hook.InBattle) SetPose(QinJunPose.Reel);
            else if (_fishing) SetPose(QinJunPose.HoldRod);
            else SetPose(QinJunPose.Sit);

            _routine = null;
        }

        void StopRoutine()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }

        void OnDisable()
        {
            StopRoutine();
            _fishing = false;
            _tossing = false;
        }

        void LoadSpritesIfMissing()
        {
            if (poseSit == null) poseSit = Load("秦俊_坐");
            if (posePull == null) posePull = Load("秦俊_拉拽");
            if (poseRaise == null) poseRaise = Load("秦俊_推举");
            if (poseCrouch == null) poseCrouch = Load("秦俊_蹲");
            if (poseHoldRod == null) poseHoldRod = Load("秦俊_握竿");
            if (poseCast1 == null) poseCast1 = Load("秦俊_甩竿1");
            if (poseCast2 == null) poseCast2 = Load("秦俊_甩竿2");
            if (poseReel == null) poseReel = Load("秦俊_横拉");
        }

        Sprite Load(string fileName)
        {
            var sprite = Resources.Load<Sprite>(SpriteFolder + fileName);
            if (sprite == null) Debug.LogWarning("[QinJunActor] 找不到动作精灵：" + SpriteFolder + fileName);
            return sprite;
        }
    }
}
