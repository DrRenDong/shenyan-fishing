using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 水面轻微上下起伏：所有水体格子（深水/浅水/水面*，以及流式海面 sea_*）
    /// 各自按「格坐标相位 + 时间」做很小的正弦位移，整片海会缓缓呼吸。
    ///
    /// 幅度刻意压得很小（默认 0.02 世界单位 ≈ 方块宽度的 1.6%），
    /// 因为相邻水块是严丝合缝拼的，位移一大接缝处就会露缝。
    /// 相位在空间上也是缓慢变化的（每格 0.18 弧度），相邻两块高差很小，看不出裂缝。
    /// </summary>
    [DefaultExecutionOrder(505)]
    public class WaterBob : MonoBehaviour
    {
        [Header("幅度与节奏")]
        [Tooltip("上下位移幅度（世界单位）。方块宽 1.26，0.02 约为它的 1.6%")]
        public float amplitude = 0.02f;
        [Tooltip("一次完整起伏的周期（秒）")]
        public float period = 4.5f;
        [Tooltip("相邻格子的相位差（弧度），越小相邻块越同步、越不容易露缝")]
        public float spatialPhase = 0.18f;
        [Tooltip("棋盘交错：相邻的两格永远一上一下（(i+j) 奇偶分相反相位）。会让接缝处出现台阶")]
        public bool checkerboard = false;

        [Header("范围")]
        public bool includeSeaStreamer = true;
        public bool includeGroundWater = true;

        [Header("跟随水面起伏的物体")]
        [Tooltip("渔船贴图跟着同格水面的相位一起上下动（只挪贴图，不影响格子逻辑）")]
        public bool includeBoat = true;
        [Tooltip("聚集点、沉船、宝藏这些浮在水面上的地块也跟着动")]
        public bool includeSeaObjects = true;
        [Tooltip("多久重新扫一次海面物件（秒），它们会不断刷新/消失")]
        public float objectRefreshInterval = 1f;

        [Header("按缩放收幅")]
        public Camera targetCamera;
        [Tooltip("正交尺寸低于这个值时开始收幅（拉得越近位移越小，避免相邻方块露缝）")]
        public float dampBelowSize = 5f;
        [Tooltip("收幅下限，0.35 表示最近时只保留 35% 的幅度")]
        [Range(0.05f, 1f)] public float minDamp = 0.35f;

        readonly List<Transform> _tiles = new List<Transform>();
        readonly List<Vector3> _basePos = new List<Vector3>();
        readonly List<float> _phase = new List<float>();

        int _lastGroundCount = -1;
        int _lastSeaCount = -1;
        IsoTileMap _map;
        Transform _seaRoot;
        PlayerBoatVehicle _boat;
        readonly List<PlacedTile> _seaObjects = new List<PlacedTile>();
        float _objectTimer;

        public int TileCount => _tiles.Count;

        void Start()
        {
            Rebuild();
            if (includeSeaObjects) RefreshSeaObjects();
        }

        /// <summary>
        /// 重新收集浮在水面上的地块（聚集点会不断刷新、沉船/宝藏捞掉就没了）。
        /// 连未激活的一起收，避免刚生成还没显形的点漏掉。
        /// </summary>
        void RefreshSeaObjects()
        {
            _seaObjects.Clear();
            foreach (var s in FindObjectsOfType<SeaSpotBuilding>(true)) _seaObjects.Add(s);
            foreach (var w in FindObjectsOfType<WreckSiteTile>(true)) _seaObjects.Add(w);
        }

        void LateUpdate()
        {
            // 地形被重新拼装时子物体数量可能不变（拆了再建），
            // 所以除了数量还要检查缓存里的引用有没有失效。
            bool broken = false;
            for (int i = 0; i < _tiles.Count; i++)
                if (_tiles[i] == null) { broken = true; break; }

            if (broken) Rebuild();
            else RebuildIfChanged();

            float t = Time.time * Mathf.PI * 2f / Mathf.Max(0.1f, period);

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            float damp = 1f;
            if (cam != null && cam.orthographic && dampBelowSize > 0.01f)
                damp = Mathf.Clamp(cam.orthographicSize / dampBelowSize, minDamp, 1f);

            float amp = amplitude * damp;

            for (int i = 0; i < _tiles.Count; i++)
            {
                Transform tr = _tiles[i];
                if (tr == null) continue;

                float y = Mathf.Sin(t + _phase[i]) * amp;
                Vector3 p = _basePos[i];
                p.y += y;
                tr.localPosition = p;
            }

            // 渔船跟着它所在那一格的水面一起上下
            if (includeBoat)
            {
                if (_boat == null) _boat = FindObjectOfType<PlayerBoatVehicle>();
                if (_boat != null)
                {
                    Vector2Int cell = _boat.Cell;
                    float boatY = Mathf.Sin(t + PhaseFor(cell.x, cell.y)) * amp;
                    _boat.SetWaterBobOffset(boatY);
                }
            }

            // 聚集点 / 沉船 / 宝藏：同样按各自格子的相位上下浮动
            if (includeSeaObjects && _seaObjects.Count > 0)
            {
                for (int i = 0; i < _seaObjects.Count; i++)
                {
                    PlacedTile o = _seaObjects[i];
                    if (o == null) continue;
                    Vector2Int cell = o.Cell;
                    o.SetWaterBobOffset(Mathf.Sin(t + PhaseFor(cell.x, cell.y)) * amp);
                }
            }

            _objectTimer += Time.deltaTime;
            if (_objectTimer >= objectRefreshInterval)
            {
                _objectTimer = 0f;
                if (includeSeaObjects) RefreshSeaObjects();
            }
        }

        /// <summary>重建列表（地形/海面被重新拼装后调用）。</summary>
        public void Rebuild()
        {
            _tiles.Clear();
            _basePos.Clear();
            _phase.Clear();

            _map = FindObjectOfType<IsoTileMap>();
            if (_map != null && includeGroundWater)
            {
                CollectFrom(_map.transform, useCellPhase: true);
                _lastGroundCount = _map.transform.childCount;
            }
            else _lastGroundCount = -1;

            if (includeSeaStreamer)
            {
                var streamer = FindObjectOfType<SeaStreamer>();
                _seaRoot = streamer != null ? streamer.transform : null;
                if (_seaRoot != null)
                {
                    CollectFrom(_seaRoot, useCellPhase: false);
                    _lastSeaCount = _seaRoot.childCount;
                }
                else _lastSeaCount = -1;
            }
            else _lastSeaCount = -1;
        }

        void RebuildIfChanged()
        {
            int ground = _map != null ? _map.transform.childCount : 0;
            int sea = _seaRoot != null ? _seaRoot.childCount : 0;
            if (_map == null || ground != _lastGroundCount || sea != _lastSeaCount)
                Rebuild();
        }

        void CollectFrom(Transform root, bool useCellPhase)
        {
            for (int c = 0; c < root.childCount; c++)
            {
                Transform ch = root.GetChild(c);
                if (ch == null) continue;
                if (ch.name.Contains("_base")) continue;

                if (!IsWaterName(ch.name)) continue;

                int i = 0, j = 0;
                if (useCellPhase)
                {
                    string[] parts = ch.name.Split('_');
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0], out i);
                        int.TryParse(parts[1], out j);
                    }
                }
                else
                {
                    // 流式海面叫 sea_i_j
                    string[] parts = ch.name.Split('_');
                    if (parts.Length >= 3)
                    {
                        int.TryParse(parts[1], out i);
                        int.TryParse(parts[2], out j);
                    }
                }

                _tiles.Add(ch);
                _basePos.Add(ch.localPosition);
                _phase.Add(PhaseFor(i, j));
            }
        }

        /// <summary>某一格的相位：棋盘模式按 (i+j) 奇偶分成相差 π 的两组，否则按坐标线性错开。</summary>
        float PhaseFor(int i, int j)
        {
            if (checkerboard) return ((i + j) & 1) == 0 ? 0f : Mathf.PI;
            return (i + j) * spatialPhase;
        }

        static bool IsWaterName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("sea_")) return true;
            return name.Contains("深水") || name.Contains("浅水") || name.Contains("水面");
        }
    }
}
