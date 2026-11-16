using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 鱼群：不是按镜头现刷的，而是跟着地形一起生成的一张「鱼群噪点图」——
    /// 每一列一个期望条数，越往右越密，同一列上下的密度完全一样。
    /// 镜头往右推时，提前把右边的噪点刷成实体；刷过的列就定下来了，
    /// 捞走、游过去都不会再补，玩家只能一直往右跑。
    /// 上下滚镜头不会回收任何鱼：鱼一直待在自己那一列的 world y 上。
    /// </summary>
    [DisallowMultipleComponent]
    public class FishSpawner : MonoBehaviour
    {
        [Header("鱼")]
        [SerializeField] string spriteFolder = "Image/鱼_像素/";   // 按体型等级降采样，头朝右
        [SerializeField, Min(4)] int fishCount = 150;               // 对象池上限（含画面外提前刷出来的）
        [SerializeField] Vector2 speedRange = new Vector2(0.7f, 2.0f);
        [SerializeField] Vector2 scaleRange = new Vector2(0.85f, 1.15f);

        [Header("大鱼限制")]
        [SerializeField] int maxBigFish = 2;              // 同时在场的巨物上限（保证同屏不会更多）
        [SerializeField] float bigFishWidthPx = 430f;     // 超过这个像素宽（约 9 格）算巨物

        [Header("刷鱼（跟着噪点图往前铺，铺过的不再生）")]
        [SerializeField] float realizeLookAhead = 30f;    // 画面右侧提前多少格把噪点刷成实体
        [SerializeField] float cullBehind = 14f;          // 落到画面左后方多远才回收进池子
        [SerializeField, Range(0f, 1f)] float nightDensity = 0.35f;   // 夜里只刷白天的这个比例

        [Header("补鱼（开回头路 / 已捞空的区域，沿途补一点点）")]
        [SerializeField, Range(0f, 1f)] float backfillFraction = 0.35f;  // 补到「正常密度」的多少
        [SerializeField, Min(0)] int backfillMax = 10;                   // 视野里最多补到几条
        [SerializeField, Min(0.02f)] float backfillInterval = 0.12f;     // 每隔多久补一条
        [SerializeField] float backfillSpread = 12f;                     // 补在画面外多远的地方

        [Header("水域")]
        [SerializeField] float surfaceY = 15.5f;          // 最高不超过水面
        [SerializeField] float bottomMargin = 1.2f;       // 离海床至少这么远
        [SerializeField] float unknownFloorY = -14f;

        [Tooltip("被「威慑」吓到的鱼往反方向窜的速度（世界单位/秒）")]
        [SerializeField] float fleeSpeed = 7f;      // 拿不到海床高度时按这个深度算

        [Header("外观")]
        [SerializeField] string sortingLayer = "Default";
        [SerializeField] int sortingOrder = 3;            // 海水(4)之下，会被水色罩住

        [Header("参考")]
        [SerializeField] SandbarTerrain terrain;
        [SerializeField] TimeOfDaySystem timeSystem;

        [Header("鱼种限制（海产聚集点入口）")]
        [Tooltip("空 = 全鱼种随机；填鱼名（如「沙丁鱼」）= 这一片水域只有这种鱼")]
        [SerializeField] string speciesFilter = "";

        /// <summary>限制只刷某个品种（聚集点入口用）。传空或素材里没有该品种时退回全鱼种。</summary>
        public void SetSpeciesFilter(string species)
        {
            if (string.IsNullOrEmpty(species)) { speciesFilter = ""; return; }
            if (_sprites.Count == 0) LoadSprites();
            foreach (var s in _sprites)
                if (s != null && s.name == species) { speciesFilter = species; return; }
            speciesFilter = "";
            Debug.LogWarning($"[FishSpawner] 素材里没有「{species}」，这片海域改回全鱼种");
        }

        public string SpeciesFilter => speciesFilter;

        class Fish
        {
            public Transform node;
            public SpriteRenderer renderer;
            public BoxCollider2D collider;
            public FishEntity entity;
            public float speed;
            public float bobPhase;
            public float bobAmp;
            public float baseY;
            public int dir;
            public bool bottom;      // 沉底类（蟹/螺/鲍等）
            public float halfW;      // 含缩放的半宽半高，用来判断"有没有露在画面里"
            public float halfH;
        }

        readonly List<Fish> _fish = new List<Fish>();

        // 饵料：一段时间内把半径内的鱼往中心吸
        float _attractTimer;
        Vector3 _attractCenter;
        float _attractRadius;
        [SerializeField] float attractSpeed = 5f;        readonly List<Sprite> _sprites = new List<Sprite>();
        readonly List<Sprite> _pickPool = new List<Sprite>();
        readonly List<float> _pickWeights = new List<float>();

        Camera _camera;
        Rect _view;              // 画面在世界里的范围
        float _realizeX;         // 下一列要刷成实体的世界 x（只往右推进，不回头）
        int _spawnCursor;
        int _warmupFrames = 2;   // 开局等 CameraFollow 在 LateUpdate 里把相机摆好
        Vector2 _lastCamPos;
        Vector2 _camVel;
        float _backfillTimer;

        void Start()
        {
            _camera = Camera.main;
            LoadSprites();
            BuildFish();
            if (_camera == null || _sprites.Count == 0) return;
            ResolveRefs();
            _lastCamPos = _camera.transform.position;
        }

        void LoadSprites()
        {
            if (_sprites.Count > 0) return;
            var all = Resources.LoadAll<Sprite>(spriteFolder);
            foreach (var s in all) if (s != null) _sprites.Add(s);
            if (_sprites.Count == 0) Debug.LogWarning("[FishSpawner] 没找到鱼图片：" + spriteFolder);
        }

        void ResolveRefs()
        {
            // 只找「自己这个场景」里的地形：叠加加载时 FindObjectOfType 可能拿到另一份场景的对象，
            // 导致沉底鱼按错误的海床高度浮在水体半空。
            if (terrain == null)
            {
                var scene = gameObject.scene;
                if (scene.IsValid())
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        var t = root.GetComponent<SandbarTerrain>();
                        if (t == null) t = root.GetComponentInChildren<SandbarTerrain>(true);
                        if (t != null) { terrain = t; break; }
                    }
                }
            }
            if (terrain == null) terrain = FindObjectOfType<SandbarTerrain>();

            if (timeSystem == null)
            {
                var scene = gameObject.scene;
                if (scene.IsValid())
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        var t = root.GetComponent<TimeOfDaySystem>();
                        if (t == null) t = root.GetComponentInChildren<TimeOfDaySystem>(true);
                        if (t != null) { timeSystem = t; break; }
                    }
                }
            }
            if (timeSystem == null) timeSystem = FindObjectOfType<TimeOfDaySystem>();
        }

        /// <summary>换一片海（开阔海域）之后调用：清掉按旧地形刷的鱼，等新地形就绪重新刷。</summary>
        public void ResetForNewSea()
        {
            for (int i = 0; i < _fish.Count; i++)
            {
                var f = _fish[i];
                if (f == null) continue;
                if (f.entity != null)
                {
                    f.entity.hooked = false;
                    f.entity.releaseRequested = false;
                    f.entity.consumeRequested = false;
                }
                Deactivate(f);
            }
            _attractTimer = 0f;
            _warmupFrames = 2;
            _realizeX = float.NegativeInfinity;
        }

        void BuildFish()
        {
            if (_fish.Count > 0) return;

            var parent = new GameObject("Fishes").transform;
            parent.SetParent(transform, false);

            for (int i = 0; i < fishCount; i++)
            {
                var go = new GameObject("Fish" + i);
                go.transform.SetParent(parent, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;

                // 碰撞箱：Trigger，供钩子判定用；不参与物理推挤
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = Vector2.one;

                _fish.Add(new Fish
                {
                    node = go.transform,
                    renderer = sr,
                    collider = col,
                    entity = go.AddComponent<FishEntity>()
                });

                go.SetActive(false);   // 全部先收进池子，由噪点图往前刷
            }
        }

        void Update()
        {
            if (_camera == null) { _camera = Camera.main; if (_camera == null) return; }
            if (_fish.Count == 0 || _sprites.Count == 0) return;
            ResolveRefs();

            float dt = Time.deltaTime;
            if (_attractTimer > 0f) _attractTimer -= dt;
            UpdateRects();

            // 开局两帧什么也别做：CameraFollow 是在 LateUpdate 里摆位的，
            // 这时算出来的视野还停在场景里相机摆的位置，按那个视野刷等于一条都刷不出来
            if (_warmupFrames > 0)
            {
                _warmupFrames--;
                _camVel = Vector2.zero;       // 相机刚被摆到正位，这一下的位移不是真的在移动
                if (_warmupFrames == 0)
                {
                    // 开局这一屏玩家还没看见，直接把这一段的噪点刷满
                    _realizeX = Mathf.Floor(_view.xMin) - 2f;
                    RealizeAhead(true);
                }
                return;
            }

            // 1) 各自游动；只按水平距离回收——上下怎么滚都不动它们
            for (int i = 0; i < _fish.Count; i++)
            {
                var f = _fish[i];
                if (f.node == null) continue;

                var e = f.entity;
                if (e != null && e.hooked) continue;              // 挂在钩子上的交给钩子管
                if (e != null && e.consumeRequested)
                {
                    // 已经收上船/卖掉：直接收回池子，绝不能再放回水里
                    e.consumeRequested = false;
                    e.releaseRequested = false;
                    e.hooked = false;
                    Deactivate(f);
                    continue;
                }
                if (e != null && e.releaseRequested) ReleaseFish(f);
                if (!f.node.gameObject.activeSelf) continue;

                MoveFish(f, dt);

                float x = f.node.position.x;
                if (x < _view.xMin - cullBehind || x > _view.xMax + realizeLookAhead + 40f)
                    Deactivate(f);
            }

            // 2) 把镜头右边的噪点提前刷成实体；刷过的列就定死了，不再回头补
            RealizeAhead(false);

            // 3) 开过的地方被捞空/游走了，视野里太稀就沿途补一点（回头路上不至于空水）
            Backfill(dt);
        }

        void UpdateRects()
        {
            Vector3 p = _camera.transform.position;
            float halfH = _camera.orthographicSize;
            float halfW = halfH * _camera.aspect;
            _view = new Rect(p.x - halfW, p.y - halfH, halfW * 2f, halfH * 2f);

            Vector2 now = p;
            _camVel = Time.deltaTime > 0f ? (now - _lastCamPos) / Time.deltaTime : Vector2.zero;
            _lastCamPos = now;
        }

        // ---------------- 补鱼（开回头路 / 已捞空的区域） ----------------

        /// <summary>
        /// 这处水质本来该有多少鱼，就按比例补回来；只在视野里明显偏少时补，
        /// 而且补在行进方向的前方（画面外），所以不会凭空冒在屏幕里。
        /// </summary>
        void Backfill(float dt)
        {
            if (terrain == null || Mathf.Abs(_camVel.x) < 0.05f) return;

            _backfillTimer += dt;
            if (_backfillTimer < backfillInterval) return;
            _backfillTimer = 0f;

            if (!terrain.TryGetFishNoise(_camera.transform.position.x, out float density)) return;
            int target = Mathf.Clamp(Mathf.RoundToInt(density * _view.width * backfillFraction), 0, backfillMax);
            if (target <= 0) return;
            if (CountInView() >= target) return;

            var f = TakeInactive();
            if (f == null) return;

            float x = _camVel.x < 0f
                ? _view.xMin - Random.Range(2f, backfillSpread)
                : _view.xMax + Random.Range(2f, backfillSpread);

            float floorY = TryGetFloor(x, out float fl) ? fl : unknownFloorY;
            float top = surfaceY;
            float bottom = floorY + bottomMargin;
            if (top - bottom < 0.3f) return;

            PlaceFish(f, x, Random.Range(bottom, top), false);
        }

        int CountInView()
        {
            int n = 0;
            for (int i = 0; i < _fish.Count; i++)
            {
                var f = _fish[i];
                if (f.node == null || !f.node.gameObject.activeSelf) continue;
                var e = f.entity;
                if (e != null && (e.hooked || e.consumeRequested)) continue;

                Vector3 p = f.node.position;
                if (p.x + f.halfW > _view.xMin && p.x - f.halfW < _view.xMax &&
                    p.y + f.halfH > _view.yMin && p.y - f.halfH < _view.yMax) n++;
            }
            return n;
        }

        // ---------------- 按噪点图往前刷 ----------------

        void RealizeAhead(bool allowInView)
        {
            if (terrain == null) return;

            // 池子/地形跟不上时游标会落后。落后到画面里就跳过，
            // 补进去等于在画面中间凭空冒鱼
            float minX = _view.xMin - 1f;
            if (!allowInView && _realizeX < minX) _realizeX = minX;

            float until = _view.xMax + realizeLookAhead;
            int guard = 0;
            while (_realizeX <= until && guard++ < 512)
            {
                if (!RealizeColumn(_realizeX, allowInView)) break;
                _realizeX += 1f;
            }
        }

        /// <summary>把一列噪点刷成实体。返回 false 表示这一列要留到下一帧再刷。</summary>
        bool RealizeColumn(float worldX, bool allowInView)
        {
            if (!terrain.TryGetFishNoise(worldX, out float density)) return false;   // 地形还没铺到

            if (timeSystem != null && timeSystem.IsNight) density *= nightDensity;
            if (density <= 0.0005f) return true;

            int count = Mathf.FloorToInt(density);
            if (Random.value < density - count) count++;
            if (count <= 0) return true;

            if (InactiveCount() < count) return false;    // 池子不够，下一帧再刷这一列

            for (int i = 0; i < count; i++)
            {
                // 列内随机一点，别让鱼排成等距的一列
                float x = worldX + Random.Range(-0.5f, 0.5f);
                float floorY = TryGetFloor(x, out float fl) ? fl : unknownFloorY;
                float top = surfaceY;
                float bottom = floorY + bottomMargin;
                if (top - bottom < 0.3f) return true;      // 这一列没水（岸上），跳过

                var f = TakeInactive();
                if (f == null) return false;
                PlaceFish(f, x, Random.Range(bottom, top), allowInView);
            }
            return true;
        }

        int InactiveCount()
        {
            int n = 0;
            for (int i = 0; i < _fish.Count; i++)
            {
                var f = _fish[i];
                if (f.node == null || f.node.gameObject.activeSelf) continue;
                if (f.entity != null && (f.entity.hooked || f.entity.releaseRequested || f.entity.consumeRequested)) continue;
                n++;
            }
            return n;
        }

        Fish TakeInactive()
        {
            for (int i = 0; i < _fish.Count; i++)
            {
                int idx = (_spawnCursor + i) % _fish.Count;
                var f = _fish[idx];
                if (f.node == null || f.node.gameObject.activeSelf) continue;
                if (f.entity != null && (f.entity.hooked || f.entity.releaseRequested || f.entity.consumeRequested)) continue;
                _spawnCursor = (idx + 1) % _fish.Count;
                return f;
            }
            return null;
        }

        void Deactivate(Fish f)
        {
            if (f.node != null) f.node.gameObject.SetActive(false);
        }

        /// <summary>
        /// 钩子放回来的鱼（大鱼挤掉的小鱼、收杆放生）：
        /// 还在水里就原地接着游，绝不传送；已经出水（船边收杆）就直接收回池子，
        /// 看起来像是被扔进船舱了。
        /// </summary>
        void ReleaseFish(Fish f)
        {
            f.entity.releaseRequested = false;

            Vector3 p = f.node.position;
            bool inWater = p.y < surfaceY - 0.3f && IsWaterPoint(p);
            if (!f.node.gameObject.activeSelf || !inWater)
            {
                Deactivate(f);
                return;
            }

            f.bottom = f.entity != null && f.entity.bottom;
            f.dir = Random.value < 0.5f ? -1 : 1;
            f.speed = f.bottom ? 0f : Random.Range(speedRange.x, speedRange.y);
            f.bobPhase = Random.Range(0f, Mathf.PI * 2f);
            f.bobAmp = f.bottom ? 0f : Random.Range(0.1f, 0.35f);
            f.baseY = p.y;
            f.renderer.flipX = f.dir < 0;
        }

        // ---------------- 落位 ----------------

        /// <summary>
        /// 按水深挑鱼（水深决定能不能出现），再按高度加权：
        /// 上层大鱼多、中层小鱼多、贴底是沉底类和珍稀物。
        /// </summary>
        void PlaceFish(Fish f, float x, float y, bool allowInView)
        {
            bool hasFloor = TryGetFloor(x, out float fl);
            float floor = hasFloor ? fl : unknownFloorY;

            float bottom = floor + bottomMargin;
            float top = surfaceY;
            y = Mathf.Clamp(y, bottom, Mathf.Max(bottom, top));
            float yRatio = top > bottom ? Mathf.InverseLerp(bottom, top, y) : 0.5f;

            float depth = Mathf.Max(0f, surfaceY - floor);
            var sprite = PickSprite(f, depth, yRatio);
            if (sprite == null) return;

            float scale = Random.Range(scaleRange.x, scaleRange.y);
            float w = sprite.rect.width / sprite.pixelsPerUnit;
            float h = sprite.rect.height / sprite.pixelsPerUnit;

            var info = FishDatabase.Get(sprite.name);
            bool bottomDweller = info.behavior == FishBehavior.Bottom;

            // 地形还没铺到这一列 / 海床离水面太近（异常值或浅滩）：沉底鱼会悬在水体半空中，这一条先跳过
            if (bottomDweller && (!hasFloor || surfaceY - floor < 20f)) return;

            float placeY = bottomDweller ? Mathf.Min(floor + 0.3f, top) : y;

            // 巨物 12 格宽：用身体的实际包围盒再挡一次，别让脑袋从画面边上探进来
            float halfW = w * scale * 0.5f;
            float halfH = h * scale * 0.5f;
            if (!allowInView)
            {
                var body = new Rect(x - halfW, placeY - halfH, halfW * 2f, halfH * 2f);
                if (body.Overlaps(_view)) return;
            }

            f.renderer.sprite = sprite;
            f.node.localScale = new Vector3(scale, scale, 1f);
            if (f.collider != null) f.collider.size = new Vector2(w, h);

            f.halfW = halfW;
            f.halfH = halfH;
            f.bottom = bottomDweller;
            f.dir = Random.value < 0.5f ? -1 : 1;
            f.speed = bottomDweller ? 0f : Random.Range(speedRange.x, speedRange.y);
            f.bobPhase = Random.Range(0f, Mathf.PI * 2f);
            f.bobAmp = bottomDweller ? 0f : Random.Range(0.1f, 0.35f);
            f.baseY = placeY;
            f.renderer.flipX = f.dir < 0;

            f.node.position = new Vector3(x, placeY, 0f);
            f.node.gameObject.SetActive(true);
            SetupEntity(f, sprite.name);
        }

        Sprite PickSprite(Fish self, float depth, float yRatio)
        {
            _pickPool.Clear();
            _pickWeights.Clear();
            float total = 0f;

            foreach (var s in _sprites)
            {
                if (s == null) continue;
                if (!string.IsNullOrEmpty(speciesFilter) && s.name != speciesFilter) continue;
                var info = FishDatabase.Get(s.name);
                if (depth + 0.01f < info.minDepth) continue;
                if (IsBig(s) && CountBig(self) >= maxBigFish) continue;

                float w;
                if (info.behavior == FishBehavior.Bottom)
                {
                    // 沉底类和海底珍稀物，只在贴近海床的一段出现
                    w = yRatio < 0.35f ? 2.2f : 0.03f;
                }
                else if (info.level >= 4)
                {
                    // 大鱼在上层
                    w = yRatio > 0.6f ? 2.0f * Mathf.InverseLerp(0.6f, 1f, yRatio) + 0.2f : 0.05f;
                }
                else if (info.level == 3)
                {
                    w = 1.0f;
                }
                else
                {
                    // 小鱼在中层
                    w = 0.2f + 2.0f * Mathf.Exp(-Mathf.Pow((yRatio - 0.45f) / 0.28f, 2f));
                }

                _pickPool.Add(s);
                _pickWeights.Add(w);
                total += w;
            }

            if (_pickPool.Count == 0) return null;

            float pick = Random.value * total;
            for (int i = 0; i < _pickPool.Count; i++)
            {
                pick -= _pickWeights[i];
                if (pick <= 0f) return _pickPool[i];
            }
            return _pickPool[_pickPool.Count - 1];
        }

        bool IsBig(Sprite s) => s != null && s.rect.width >= bigFishWidthPx;

        int CountBig(Fish exclude)
        {
            int n = 0;
            foreach (var f in _fish)
            {
                if (f == exclude || f.node == null || !f.node.gameObject.activeSelf) continue;
                if (f.renderer == null || f.renderer.sprite == null) continue;
                if (IsBig(f.renderer.sprite)) n++;
            }
            return n;
        }

        // ---------------- 每帧 ----------------

        void MoveFish(Fish f, float dt)
        {
            Vector3 p = f.node.position;

            // 被「威慑」吓到：先往离开钩子的方向窜一小段。
            // （只标一个 intimidated 记号不够 —— 鱼还贴在钩子上，下一帧照样会被勾住）
            if (f.entity != null && f.entity.fleeTimer > 0f)
            {
                f.entity.fleeTimer -= dt;
                float sx = p.x >= f.entity.fleeFrom.x ? 1f : -1f;
                p.x += sx * fleeSpeed * dt;

                float fy = p.y;
                if (!f.bottom)
                    fy += (p.y >= f.entity.fleeFrom.y ? 1f : -1f) * fleeSpeed * 0.35f * dt;
                if (TryGetFloor(p.x, out float fleeFloor) && fy < fleeFloor + bottomMargin)
                    fy = fleeFloor + bottomMargin;
                if (fy > surfaceY) fy = surfaceY;

                p.y = fy;
                f.dir = sx >= 0f ? 1 : -1;
                f.baseY = p.y;
                f.node.position = p;
                if (f.renderer != null) f.renderer.flipX = f.dir < 0;
                return;
            }

            // 饵料生效期间：半径内的鱼朝撒饵点游过去
            if (_attractTimer > 0f && !f.bottom)
            {
                Vector3 toCenter = _attractCenter - p;
                toCenter.z = 0f;
                if (toCenter.sqrMagnitude < _attractRadius * _attractRadius &&
                    toCenter.sqrMagnitude > 0.36f)
                {
                    Vector3 step = toCenter.normalized * attractSpeed * dt;
                    p += step;
                    f.dir = step.x >= 0f ? 1 : -1;
                    f.baseY = p.y;
                    f.node.position = p;
                    if (f.renderer != null) f.renderer.flipX = f.dir < 0;
                    return;
                }
            }

            if (!f.bottom) p.x += f.dir * f.speed * dt;

            float y = f.baseY + (f.bottom ? 0f : Mathf.Sin(Time.time * 1.6f + f.bobPhase) * f.bobAmp);

            if (!f.bottom && TryGetFloor(p.x, out float floor) && y < floor + bottomMargin)
                y = floor + bottomMargin;
            if (y > surfaceY) y = surfaceY;

            p.y = y;
            f.node.position = p;
            if (f.renderer != null) f.renderer.flipX = f.dir < 0;
        }

        bool IsWaterPoint(Vector2 p)
        {
            if (p.y > surfaceY - 0.3f) return false;
            if (!TryGetFloor(p.x, out float floor)) return true;
            return p.y > floor + 0.1f;
        }

        /// <summary>给鱼挂上数据组件，供鱼钩判定勾到了什么。</summary>
        void SetupEntity(Fish f, string fishName)
        {
            if (f.entity == null) f.entity = f.node.gameObject.AddComponent<FishEntity>();
            f.entity.Setup(FishDatabase.Get(fishName), fishName);
        }

        bool TryGetFloor(float worldX, out float floorY)
        {
            floorY = unknownFloorY;
            if (terrain == null) return false;
            return terrain.TryGetGroundTopY(worldX, out floorY);
        }

        public void SetTerrain(SandbarTerrain t) => terrain = t;

        /// <summary>撒饵：duration 秒内把 radius 范围内的鱼往 center 聚。</summary>
        public void AttractFish(Vector3 center, float radius, float duration)
        {
            _attractCenter = center;
            _attractRadius = radius;
            _attractTimer = Mathf.Max(_attractTimer, duration);
        }
    }
}
