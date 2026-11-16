using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 主角的渔船：点选船体会进入「划航线」模式，每回合沿航线走一格；船上带着「神眼」，用来探查鱼群。
    /// 船的点击层与鼠标描边都用烘焙好的船体轮廓（和其他工厂一样），四个方向靠换图 + flipX 镜像。
    /// </summary>
    public class PlayerBoatVehicle : PlacedTile
    {
        [Header("移动")]
        public List<Vector2Int> movePath = new List<Vector2Int>();
        [Tooltip("每回合走几格（默认 1）")]
        public int cellsPerTurn = 1;

        [Header("外观（占一格）")]
        [Tooltip("船体缩放")]
        public float bodyScale = 1.44f;
        [Tooltip("船体在格子里的偏移（世界单位，y 向上）：0.66 = 等距格顶面中心")]
        public Vector2 bodyOffset = new Vector2(0f, 0.66f);

        SpriteRenderer _body;
        IsoTileMap _map;
        Sprite _frontLeft;   // 左前.png
        Sprite _backRight;   // 右后.png

        public bool HasPath => movePath != null && movePath.Count > 0;
        public Vector2Int NextCell => HasPath ? movePath[0] : Cell;

        protected override void Awake()
        {
            base.Awake();
            SetupSprites();
        }

        void Start()
        {
            SetupSprites();
        }

        public override void EnsureVisual()
        {
            base.EnsureVisual();
            SetupSprites();
        }

        void SetupSprites()
        {
            if (_frontLeft == null) _frontLeft = Resources.Load<Sprite>("Image/渔船/左前");
            if (_backRight == null) _backRight = Resources.Load<Sprite>("Image/渔船/右后");

            if (_body == null)
            {
                Transform old = transform.Find("boat");
                if (old != null) _body = old.GetComponent<SpriteRenderer>();
            }
            if (_body == null)
            {
                var go = new GameObject("boat");
                go.transform.SetParent(transform, false);
                _body = go.AddComponent<SpriteRenderer>();
            }

            _body.sprite = _frontLeft != null ? _frontLeft : _body.sprite;
            RefreshSortingOrder();
            _body.transform.localScale = Vector3.one * Mathf.Max(0.05f, bodyScale);
            _body.transform.localPosition = new Vector3(bodyOffset.x, bodyOffset.y, 0f);
            ApplyClickShape();
        }

        /// <summary>
        /// 排序号跟着所在格走（和地块一样按 i+j 分层）。
        /// 以前写死 950，船开到远处时海面的 (i+j)*4 会超过它，船就被水体盖住了。
        /// </summary>
        void RefreshSortingOrder()
        {
            if (_body == null) return;
            _body.sortingOrder = (gridX + gridY) * 4 + 20;
        }

        /// <summary>
        /// 点击层：用烘焙好的船体轮廓（和其他建筑一样），flipX 时把轮廓也镜像，
        /// 这样「看到的高亮」始终等于「点得到的范围」。
        /// </summary>
        void ApplyClickShape()
        {
            if (_body == null || _body.sprite == null) return;

            var box = _body.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                if (Application.isPlaying) Destroy(box);
                else DestroyImmediate(box);
            }

            var poly = _body.GetComponent<PolygonCollider2D>();
            if (poly == null) poly = _body.gameObject.AddComponent<PolygonCollider2D>();

            if (!ClickShapeDatabase.TryGet(_body.sprite.name, out Vector2[] shape) || shape.Length < 3)
            {
                var b = _body.sprite.bounds;
                shape = new[]
                {
                    new Vector2(b.min.x, b.center.y),
                    new Vector2(b.center.x, b.max.y),
                    new Vector2(b.max.x, b.center.y),
                    new Vector2(b.center.x, b.min.y),
                };
            }

            if (_body.flipX)
            {
                var mirrored = new Vector2[shape.Length];
                for (int k = 0; k < shape.Length; k++) mirrored[k] = new Vector2(-shape[k].x, shape[k].y);
                poly.points = mirrored;
            }
            else
            {
                poly.points = shape;
            }
        }

        /// <summary>划一条移动路线（格子列表，不含起点）。</summary>
        public void SetPath(List<Vector2Int> path)
        {
            movePath.Clear();
            if (path == null) return;
            foreach (var c in path)
            {
                if (c == Cell) continue;
                movePath.Add(c);
            }

            // 设完航线就让船头朝向下回合要去的方向
            FaceNextWaypoint();
        }

        public void ClearPath() => movePath.Clear();

        // ---------------- 船舱（捕鱼带回来的鱼） ----------------

        [System.Serializable]
        public class CargoSlot
        {
            public string itemId;
            public float kg;
            public float quality = 1f;
        }

        [Header("船舱")]
        [Tooltip("捕鱼带回来的鱼：按品种攒着，船靠近路面/仓库时每回合卸进相连仓库")]
        public List<CargoSlot> cargo = new List<CargoSlot>();

        /// <summary>海神号船舱按商店载重的 10 倍算（和捕鱼场景里的船保持一致，免得渔获带回工厂被丢）。</summary>
        public const float CargoScale = 10f;

        public float CargoCapacity => Mathf.Max(1f, ShopData.MaxLoad * CargoScale);

        public float CargoWeight
        {
            get
            {
                float sum = 0f;
                for (int k = 0; k < cargo.Count; k++)
                    if (cargo[k] != null) sum += cargo[k].kg;
                return sum;
            }
        }

        /// <summary>往船舱里装鱼，超出容量的部分装不下（返回实际装入量）。</summary>
        public float AddCargo(string itemId, float kg, float quality)
        {
            if (string.IsNullOrEmpty(itemId) || kg <= 0f) return 0f;
            if (ItemDatabase.IsTreasureItem(itemId)) return 0f;   // 贵重品不占船舱、不从船上卸

            float room = Mathf.Max(0f, CargoCapacity - CargoWeight);
            float add = Mathf.Min(room, kg);
            if (add <= 0.001f) return 0f;

            for (int k = 0; k < cargo.Count; k++)
            {
                CargoSlot slot = cargo[k];
                if (slot == null || slot.itemId != itemId) continue;

                float total = slot.kg + add;
                slot.quality = total > 0f ? (slot.quality * slot.kg + quality * add) / total : quality;
                slot.kg = total;
                return add;
            }

            cargo.Add(new CargoSlot { itemId = itemId, kg = add, quality = Mathf.Clamp01(quality) });
            return add;
        }

        public void ClearCargo() => cargo.Clear();

        /// <summary>船是不是贴着路面或仓库（只有这样才能卸货）。</summary>
        public bool AdjacentToLogistics()
        {
            if (FactoryWorld.Instance == null) return false;

            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };
            foreach (var d in dirs)
            {
                Vector2Int c = Cell + d;
                if (FactoryWorld.Instance.IsRoad(c)) return true;
                if (FactoryWorld.Instance.OccupantAt(c) is WarehouseBuilding) return true;
            }
            return false;
        }

        /// <summary>每回合：贴着路面/仓库就把船舱里的鱼全部送进相连仓库，返回卸掉多少公斤。</summary>
        public float UnloadToWarehouses(bool silent = false)
        {
            if (cargo.Count == 0) return 0f;
            if (FactoryWorld.Instance == null || !AdjacentToLogistics()) return 0f;

            var warehouses = new List<WarehouseBuilding>();
            FactoryWorld.Instance.WarehousesConnectedTo(this, warehouses);
            if (warehouses.Count == 0) return 0f;

            float moved = 0f;
            for (int k = 0; k < cargo.Count; k++)
            {
                CargoSlot slot = cargo[k];
                if (slot == null || slot.kg <= 0f) continue;
                if (ItemDatabase.IsTreasureItem(slot.itemId)) continue;   // 贵重品不会运上岸
                warehouses[0].storage.Add(slot.itemId, slot.kg, slot.quality);
                moved += slot.kg;
            }
            cargo.Clear();

            if (!silent && moved > 0f)
                Debug.Log($"[渔船] 卸货 {moved:0.0}KG 到仓库（{warehouses[0].gridX},{warehouses[0].gridY}）");
            return moved;
        }

        /// <summary>船头朝向下一个航点（也就是下回合要去的方向）。</summary>
        void FaceNextWaypoint()
        {
            if (!HasPath) return;
            Vector2Int next = movePath[0];
            ApplyDirection(next.x - gridX, next.y - gridY);
        }

        /// <summary>推进一个回合：沿着路径走 cellsPerTurn 格。</summary>
        public void AdvanceTurn()
        {
            if (!HasPath) return;

            for (int step = 0; step < Mathf.Max(1, cellsPerTurn) && movePath.Count > 0; step++)
            {
                Vector2Int next = movePath[0];

                // 航线是设的时候算好的，之后地图可能变了（比如往航线上填了地块组）。
                // 前面不是水就停下，别让船开上陆地。
                if (!CanSailTo(next))
                {
                    movePath.Clear();
                    Debug.Log($"[渔船] 航线在（{next.x},{next.y}）被挡住了，停船");
                    break;
                }

                movePath.RemoveAt(0);
                ApplyDirection(next.x - gridX, next.y - gridY);
                gridX = next.x;
                gridY = next.y;
            }

            transform.localPosition = CellToWorld(gridX, gridY);
            RefreshSortingOrder();
            if (FactoryWorld.Instance != null) FactoryWorld.Instance.Register(this);

            // 移动完立刻转向下一个航点，而不是等下一次移动
            FaceNextWaypoint();
        }

        /// <summary>这一格能不能开过去：没有地形（开放海域）或者确实是水。</summary>
        bool CanSailTo(Vector2Int cell)
        {
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_map == null) return true;

            string ground = _map.TileAt(cell.x, cell.y);
            if (string.IsNullOrEmpty(ground)) return true;
            return _map.IsWaterAt(cell.x, cell.y);
        }

        /// <summary>
        /// 素材只有两张：左前（左前.png）与右后（右后.png）。
        /// 另外两个方向用水平镜像补：右前 = 左前镜像，左后 = 右后镜像。
        /// </summary>
        void ApplyDirection(int di, int dj)
        {
            SetupSprites();
            if (_body == null) return;

            if (dj > 0)          // +j：屏幕左下 = 左前
            {
                if (_frontLeft != null) _body.sprite = _frontLeft;
                _body.flipX = false;
            }
            else if (di > 0)     // +i：屏幕右下 = 右前（左前镜像）
            {
                if (_frontLeft != null) _body.sprite = _frontLeft;
                _body.flipX = true;
            }
            else if (dj < 0)     // -j：屏幕右上 = 右后
            {
                if (_backRight != null) _body.sprite = _backRight;
                _body.flipX = false;
            }
            else if (di < 0)     // -i：屏幕左上 = 左后（右后镜像）
            {
                if (_backRight != null) _body.sprite = _backRight;
                _body.flipX = true;
            }

            ApplyClickShape();
        }
    }
}
