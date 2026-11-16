using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 海面流式铺砖 = 神眼视野：只铺玩家船所在格周围 5×5 的深海地块，
    /// 已有的地形（岛/浅水）和建筑格不覆盖；船走开后视野外的海面立刻销毁。
    /// 地图本身可以无限延伸（没有边界限制，只按需生成/回收）。
    /// </summary>
    public class SeaStreamer : MonoBehaviour
    {
        public PlayerBoatVehicle boat;
        [Tooltip("视野半径：2 = 以船所在格为中心的 5×5（神眼范围）")]
        public int viewRadius = 2;

        /// <summary>实际视野半径 = 基础 + 神眼·远眺加成。</summary>
        public int CurrentViewRadius => Mathf.Max(0, viewRadius + AbilityData.BoatViewBonus);
        [Tooltip("每块陆地外围多少格内也要铺海面（避免相机看到地图外的漆黑），0 = 关")]
        public int landHalo = 5;
        public string seaTile = "深水";

        const string ChildPrefix = "sea_";

        readonly Dictionary<Vector2Int, GameObject> _shown = new Dictionary<Vector2Int, GameObject>();
        readonly HashSet<Vector2Int> _want = new HashSet<Vector2Int>();
        IsoTileMap _map;
        Sprite _seaSprite;

        public int ShownCount => _shown.Count;

        /// <summary>
        /// 拿到渔船。读档时场景里的船会被销毁重建，序列化引用就变空，
        /// 这时视野只剩「陆地外扩」——船开在远海就整片黑，而且怎么走都没用。
        /// 所以引用空了就自己找回来。
        /// </summary>
        PlayerBoatVehicle ResolveBoat()
        {
            if (boat == null) boat = FindObjectOfType<PlayerBoatVehicle>();
            return boat;
        }

        void Awake()
        {
            _seaSprite = TileCatalog.LoadSprite(seaTile);
        }

        void Start()
        {
            // 开局先铺一次，顺便清掉场景里上次遗留下来的海面物体
            Refresh();
        }

        /// <summary>重新计算视野并增删海面地块。</summary>
        public void Refresh()
        {
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_seaSprite == null) _seaSprite = TileCatalog.LoadSprite(seaTile);
            if (_seaSprite == null) return;

            _want.Clear();
            var currentBoat = ResolveBoat();
            if (currentBoat != null) AddAround(_want, currentBoat.Cell, CurrentViewRadius);
            AddLandHalo(_want);

            // 回收：只处理自己铺的 sea_* 物体（别把用户手动摆进来的东西一起删了）
            _shown.Clear();
            for (int k = transform.childCount - 1; k >= 0; k--)
            {
                Transform ch = transform.GetChild(k);
                if (!TryParseCell(ch.name, out Vector2Int c)) continue;    // 不是我铺的海面，跳过
                if (_want.Contains(c) && !_shown.ContainsKey(c))
                {
                    _shown[c] = ch.gameObject;
                    continue;
                }

                ch.gameObject.SetActive(false);   // 立刻从画面消失，帧末再销毁
                if (Application.isPlaying) Destroy(ch.gameObject);
                else DestroyImmediate(ch.gameObject);
            }

            // 补上新的
            foreach (var c in _want)
            {
                if (_shown.ContainsKey(c)) continue;
                var go = new GameObject(ChildPrefix + c.x + "_" + c.y);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = PlacedTile.CellToWorld(c.x, c.y);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _seaSprite;
                sr.sortingOrder = (c.x + c.y) * 4 - 1;    // 略低于建筑
                _shown[c] = go;
            }
        }

        static bool TryParseCell(string name, out Vector2Int cell)
        {
            cell = default;
            if (string.IsNullOrEmpty(name) || !name.StartsWith(ChildPrefix)) return false;
            string[] parts = name.Substring(ChildPrefix.Length).Split('_');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out int i) || !int.TryParse(parts[1], out int j)) return false;
            cell = new Vector2Int(i, j);
            return true;
        }

        /// <summary>
        /// 这一格算不算「在视野里」：船周围 viewRadius，或者离任何一块陆地不超过 landHalo。
        /// 聚集点/沉船/宝藏的显形、压暗、区块活跃都按这个来。
        /// </summary>
        public bool IsInVision(Vector2Int cell)
        {
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_map == null) return false;

            var b = ResolveBoat();
            if (b != null &&
                Mathf.Max(Mathf.Abs(cell.x - b.gridX), Mathf.Abs(cell.y - b.gridY)) <= CurrentViewRadius)
                return true;

            return IsNearLand(cell, landHalo);
        }

        /// <summary>这一格周围 halo 格内有没有陆地。</summary>
        public bool IsNearLand(Vector2Int cell, int halo)
        {
            if (_map == null || halo <= 0) return false;

            for (int dx = -halo; dx <= halo; dx++)
            {
                for (int dy = -halo; dy <= halo; dy++)
                {
                    int i = cell.x + dx, j = cell.y + dy;
                    string g = _map.TileAt(i, j);
                    if (!string.IsNullOrEmpty(g) && !_map.IsWaterAt(i, j)) return true;
                }
            }
            return false;
        }

        /// <summary>这一格是不是「自己就带整块水体贴图」的东西（沉船 / 宝藏）。
        /// 它们不用再垫一层海面，否则会多出一块水体方块。</summary>
        static bool HasOwnWaterTile(Vector2Int cell)
        {
            if (FactoryWorld.Instance == null) return false;
            return FactoryWorld.Instance.OccupantAt(cell) is WreckSiteTile;
        }

        void AddAround(HashSet<Vector2Int> set, Vector2Int center, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var c = new Vector2Int(center.x + dx, center.y + dy);

                    // 已经有地形（陆地/水）的格子不重复铺。
                    // 这里只按地形判断：船自己所在的格子、海面上的物件也都要有海面垫底。
                    if (_map != null && !string.IsNullOrEmpty(_map.TileAt(c.x, c.y))) continue;
                    if (HasOwnWaterTile(c)) continue;      // 沉船/宝藏自带水体，不用再垫

                    set.Add(c);
                }
            }
        }

        /// <summary>
        /// 每块陆地外围 landHalo 格内也铺海面：船在岛上时相机往地图外看不会是一片黑。
        /// 已经有地形（岛上/近海）、或者有建筑的格子都跳过。
        /// </summary>
        void AddLandHalo(HashSet<Vector2Int> set)
        {
            if (landHalo <= 0 || _map == null) return;

            // 遍历已有地形的包围盒（地图可以往任意方向长出去）
            for (int j = _map.MinJ; j <= _map.MaxJ; j++)
            {
                for (int i = _map.MinI; i <= _map.MaxI; i++)
                {
                    string ground = _map.TileAt(i, j);
                    if (string.IsNullOrEmpty(ground)) continue;
                    if (_map.IsWaterAt(i, j)) continue;      // 只从陆地往外算

                    for (int dx = -landHalo; dx <= landHalo; dx++)
                    {
                        for (int dy = -landHalo; dy <= landHalo; dy++)
                        {
                            var c = new Vector2Int(i + dx, j + dy);
                            if (!string.IsNullOrEmpty(_map.TileAt(c.x, c.y))) continue;
                            if (HasOwnWaterTile(c)) continue;      // 沉船/宝藏自带水体

                            // 这里也不看「格子上有没有东西」：聚集点/沉船/宝藏都算贴在海面上的东西，
                            // 底下必须有海面，否则会出现缺一格的空洞。
                            set.Add(c);
                        }
                    }
                }
            }
        }
    }
}
