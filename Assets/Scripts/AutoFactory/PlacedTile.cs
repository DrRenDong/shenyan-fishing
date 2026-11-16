using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 地图上的一个地块（建筑 / 路面标记 / 资源点）的基类：
    /// 负责按 TileCatalog 的规则摆好精灵（抬升 + 按等距斜列排序），并按格坐标落到正确位置。
    /// 排序号与 IsoTileMap 一致：(i + j) * 4，建筑比同格地面高 1 层。
    /// </summary>
    public class PlacedTile : MonoBehaviour
    {
        [Header("网格")]
        public int gridX;
        public int gridY;

        [Header("素材")]
        public string tileId = "";

        public Vector2Int Cell => new Vector2Int(gridX, gridY);

        SpriteRenderer _renderer;
        /// <summary>精灵渲染器（懒查找：编辑期没跑 Awake 时也能拿到）。</summary>
        public SpriteRenderer Renderer
        {
            get
            {
                if (_renderer == null && transform.childCount > 0)
                    _renderer = transform.GetChild(0).GetComponent<SpriteRenderer>();
                return _renderer;
            }
        }

        protected const int OrderPerDiagonal = 4;
        protected const int BuildingOrderOffset = 1;
        protected const int TilePixelWidth = 304;
        protected const int TilePixelHeight = 152;
        protected const float SurfacePixels = 129f;

        /// <summary>网格坐标 → 世界坐标（该格方块底面「前角」的位置），与 IsoTileMap 一致。</summary>
        public static Vector3 CellToWorld(int i, int j)
        {
            return new Vector3((i - j) * 0.5f, -(i + j) * 0.25f, 0f);
        }

        /// <summary>世界坐标 → 网格坐标（按地面菱形中心的高度取样）。</summary>
        public static Vector2Int WorldToCell(Vector3 world, float surfaceOffset = 0.66f)
        {
            float x = world.x;
            float y = world.y - surfaceOffset;
            float u = x / 0.5f;          // = i - j
            float v = -y / 0.25f;        // = i + j
            return new Vector2Int(Mathf.RoundToInt((u + v) * 0.5f),
                                  Mathf.RoundToInt((v - u) * 0.5f));
        }

        protected virtual void Awake()
        {
            EnsureVisual();
        }

        protected virtual void OnEnable()
        {
            if (FactoryWorld.Instance != null) FactoryWorld.Instance.Register(this);
        }

        protected virtual void Start()
        {
            if (FactoryWorld.Instance != null) FactoryWorld.Instance.Register(this);
        }

        /// <summary>生成/找回精灵子物体（编辑期由构建脚本调用一次，运行期 Awake 复用已有的）。</summary>
        public virtual void EnsureVisual()
        {
            Vector3 pos = CellToWorld(gridX, gridY);

            // 地势（小山）上的地块要跟着地面一起抬高
            var map = FindObjectOfType<IsoTileMap>();
            if (map != null) pos.y += map.HeightOffsetWorld(gridX, gridY);

            transform.localPosition = pos;
            if (Renderer != null) return;
            BuildVisual();
        }

        // ---------------- 施工状态 ----------------

        // ---------------- 等级（1~3 级，升两级） ----------------

        public const int MaxLevel = 3;

        [Tooltip("建筑等级 1~3：等级越高工作速率与运营费越高")]
        public int level = 1;

        public bool CanUpgrade => level < MaxLevel;

        /// <summary>升级要花的钱：造价越高、等级越高越贵；没造价的建筑按 400/级 估。</summary>
        public int UpgradeCost
        {
            get
            {
                int baseCost = buildCost > 0 ? buildCost : 400;
                return Mathf.RoundToInt(baseCost * (0.5f + 0.5f * level));
            }
        }

        /// <summary>工作速率倍率：1 级 ×1.0、2 级 ×1.5、3 级 ×2.0。</summary>
        public float LevelSpeedMultiplier => 1f + 0.5f * (level - 1);

        /// <summary>运营费倍率：1 级 ×1.0、2 级 ×1.4、3 级 ×1.8。</summary>
        public float LevelCostMultiplier => 1f + 0.4f * (level - 1);

        /// <summary>花钱升一级，成功返回 true。</summary>
        public bool TryUpgrade()
        {
            if (!CanUpgrade) return false;
            int cost = UpgradeCost;
            if (PlayerProgress.Gold < cost) return false;
            PlayerProgress.AddGold(-cost);
            level++;
            GameAudio.Ensure().PlayBuildSfx();   // 升级复用建造音效
            Debug.Log($"{name} 升到 {level} 级（花费 {cost} 金）");
            return true;
        }

        /// <summary>
        /// 能不能被拆除。默认不能——民居 / 棚屋 / 农田 / 树林 / 巨石 / 碎石 / 聚集点这些是地块，
        /// 渔船也不是玩家造的东西；只有建筑、设施、港口、路面才允许拆。
        /// </summary>
        public virtual bool CanDemolish => false;

        /// <summary>建造时花掉的金币（拆除退一半用）。</summary>
        public int buildCost;

        /// <summary>修路前这一格原本的地形（拆除时还原）。</summary>
        public string originalGround = "";

        /// <summary>还要修几回合；0 = 已完工。</summary>
        public int BuildTurnsLeft { get; private set; }
        public bool UnderConstruction => BuildTurnsLeft > 0;

        /// <summary>水平镜像（摆放时按 R 翻转）。精灵与点击轮廓一起翻，存档要带上。</summary>
        public bool mirrored;

        public void SetMirrored(bool mirrored)
        {
            this.mirrored = mirrored;

            SpriteRenderer sr = Renderer;
            if (sr == null) return;
            sr.flipX = mirrored;

            var poly = sr.GetComponent<PolygonCollider2D>();
            if (poly != null)
            {
                Vector2[] pts = poly.points;
                for (int k = 0; k < pts.Length; k++) pts[k].x = -pts[k].x;
                poly.points = pts;
            }
        }

        /// <summary>开始施工：工期 &gt; 0 时半透明显示。</summary>
        public void StartConstruction(int turns)
        {
            BuildTurnsLeft = Mathf.Max(0, turns);
            ApplyAlpha(UnderConstruction ? 0.45f : 1f);
        }

        /// <summary>推进一回合，返回这一回合是否完工。</summary>
        public bool AdvanceConstruction()
        {
            if (BuildTurnsLeft <= 0) return false;
            BuildTurnsLeft--;
            if (BuildTurnsLeft == 0) ApplyAlpha(1f);
            return BuildTurnsLeft == 0;
        }

        void ApplyAlpha(float alpha)
        {
            SpriteRenderer sr = Renderer;
            if (sr == null) return;
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }

        /// <summary>
        /// 只建点击层、不画精灵：农田 / 巨石 / 碎石这些地形已经由地面图层画好了，
        /// 这里只需要一个能点中的碰撞体，用来显示地块效果。
        /// </summary>
        public bool colliderOnly;

        void BuildVisual()
        {
            if (string.IsNullOrEmpty(tileId)) return;

            Sprite sprite = TileCatalog.LoadSprite(tileId);

            var go = new GameObject("sprite");
            go.transform.SetParent(transform, false);

            if (colliderOnly)
            {
                // 只挂碰撞体；视觉由地面瓦片负责
                var hit = go.AddComponent<PolygonCollider2D>();
                float cy = (SurfacePixels + TilePixelHeight / 2f) / TilePixelWidth;
                hit.points = new[]
                {
                    new Vector2(0f, cy + 0.25f),
                    new Vector2(0.5f, cy),
                    new Vector2(0f, cy - 0.25f),
                    new Vector2(-0.5f, cy),
                };
                return;
            }

            if (sprite == null)
            {
                DestroyImmediate(go);
                return;
            }

            TileDef def = TileCatalog.Find(tileId);
            float lift = def != null ? def.LiftPixels : 0f;
            go.transform.localPosition = new Vector3(0f, lift / TilePixelWidth, 0f);

            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = sprite;
            _renderer.sortingOrder = (gridX + gridY) * OrderPerDiagonal + BuildingOrderOffset;

            // 点选用的碰撞体：优先用烘焙好的 alpha 轮廓，没有则回退成一格底面菱形
            var poly = go.AddComponent<PolygonCollider2D>();
            if (!ClickShapeDatabase.TryGet(sprite.name, out Vector2[] shape) || shape.Length < 3)
            {
                float cy = (SurfacePixels + TilePixelHeight / 2f) / TilePixelWidth;
                shape = new[]
                {
                    new Vector2(0f, cy + 0.25f),
                    new Vector2(0.5f, cy),
                    new Vector2(0f, cy - 0.25f),
                    new Vector2(-0.5f, cy),
                };
            }
            poly.points = shape;
        }
    }
}
