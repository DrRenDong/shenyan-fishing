using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 工厂玩法的新手教程，分三段各自只在第一次出现时播：
    /// 1）开局建筑教程：仓库 → 路面 → 捕捞港口 → 指定捕捞点 → 货运码头 → 下一回合 → 库存买卖，
    ///    目标格会高亮并自动把镜头拉过去，玩家按提示建好就自动进下一步；
    /// 2）地块组拼接教程：土地局第一次批地（LandGrantPanel.Open）时弹一张说明卡；
    /// 3）电力与电力设施教程：第一次打开电力图、或第一次放下接收塔/棱镜塔时弹说明卡。
    /// 三段各自把「已看过」写进 SaveSystem，不再重复打扰。
    /// </summary>
    public class FactoryTutorial : MonoBehaviour
    {
        public static FactoryTutorial Instance { get; private set; }

        [Header("开局教学")]
        public int budget = 3000;          // 开工厂前保证手里至少有这么多钱
        public float cardWidth = 700f;
        public float cardTop = 232f;

        public bool BasicRunning => _state == State.Basic;
        public int Step => _step;

        enum State { None, Basic, LandInfo, PowerInfo, Sail }

        State _state = State.None;
        bool _started;
        int _step;
        int _startTurn;
        int _lastPortTurns = -1;
        bool _cellGuided;

        Vector2Int _cellRoad, _cellWarehouse, _cellPort, _cellDock;

        IsoTileMap _map;
        TurnManager _turn;

        bool _pendingLand;
        bool _pendingPower;

        // ---- UI ----
        Font _font;
        Canvas _canvas;
        RectTransform _card;
        Text _titleText;
        Text _bodyText;
        Text _hintText;
        Button _actionBtn;
        Text _actionLabel;
        GameObject _skipGo;

        // ---- 高亮 ----
        LineRenderer _hl;
        Vector2Int _hlCell;
        bool _hlOn;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!_started)
            {
                if (!Ready()) return;

                // 等读档重建完成再开始（否则会把重建出来的建筑当成玩家刚盖的）；最多等 3 秒兜底
                _readyTime += Time.unscaledDeltaTime;
                if (!FactorySaveSystem.Applied && _readyTime < 3f) return;

                StartOnce();
                return;
            }

            if (_hlOn) DrawHighlight();

            if (_state == State.Basic) EvaluateBasicStep();
            else if (_state == State.Sail) EvaluateSailStep();
        }

        float _readyTime;

        bool Ready()
        {
            if (_map == null) _map = FindObjectOfType<IsoTileMap>();
            if (_turn == null) _turn = FindObjectOfType<TurnManager>();
            return _map != null && _turn != null && FactoryWorld.Instance != null;
        }

        void StartOnce()
        {
            _started = true;

            if (!SaveSystem.FactoryBasicTutorialDone) BeginBasic();
            else if (!SaveSystem.FactorySailTutorialDone) BeginSailTutorial();
        }

        // ---------------- 启动 ----------------

        void BeginBasic()
        {
            EnsureBudget();
            _state = State.Basic;
            _startTurn = _turn != null ? _turn.Turn : 1;
            FindBlock();
            SnapshotExisting();
            ShowStep(0);

            Debug.Log($"[教程] 工厂开局教程开始：路面 {_cellRoad} / 仓库 {_cellWarehouse} / " +
                      $"捕捞港 {_cellPort} / 码头 {_cellDock}（引导格={_cellGuided}）");
        }

        // 教程开始时场上已有的建筑：只有「这次新盖出来的」才算完成当前步骤，
        // 否则老存档里已经有一堆工厂时会一路秒过教程。
        readonly HashSet<int> _knownAtStart = new HashSet<int>();

        void SnapshotExisting()
        {
            _knownAtStart.Clear();
            foreach (var x in FindObjectsOfType<WarehouseBuilding>()) _knownAtStart.Add(x.GetInstanceID());
            foreach (var x in FindObjectsOfType<RoadTile>()) _knownAtStart.Add(x.GetInstanceID());
            foreach (var x in FindObjectsOfType<FishingPortBuilding>()) _knownAtStart.Add(x.GetInstanceID());
            foreach (var x in FindObjectsOfType<CargoDockBuilding>()) _knownAtStart.Add(x.GetInstanceID());
        }

        /// <summary>玩家第一次开工厂时手里至少有 budget 金，免得连第一个仓库都建不起。</summary>
        void EnsureBudget()
        {
            if (PlayerProgress.Gold >= budget) return;
            int add = budget - PlayerProgress.Gold;
            PlayerProgress.AddGold(add);
            Debug.Log($"[教程] 开工厂补贴 {add} 金（当前 {PlayerProgress.Gold}）");
        }

        // ---------------- 目标格 ----------------

        /// <summary>优先用岛东南角的教程预留区，其次在地图上找一处「两格陆地 + 下面两格水」的 2×2。</summary>
        void FindBlock()
        {
            _cellGuided = false;
            if (_map == null || FactoryWorld.Instance == null) return;

            if (TryBlock(new Vector2Int(11, 10))) { _cellGuided = true; return; }

            for (int j = _map.MinJ; j < _map.MaxJ; j++)
            {
                for (int i = _map.MinI; i < _map.MaxI; i++)
                {
                    if (!TryBlock(new Vector2Int(i, j))) continue;
                    _cellGuided = true;
                    return;
                }
            }

            Debug.LogWarning("[教程] 没找到合适的 2×2 教学用地，改成只要求建筑类型（不引导具体格子）");
        }

        bool TryBlock(Vector2Int road)
        {
            Vector2Int wh = new Vector2Int(road.x + 1, road.y);
            Vector2Int port = new Vector2Int(road.x, road.y + 1);
            Vector2Int dock = new Vector2Int(road.x + 1, road.y + 1);

            if (!LandOk(road) || !LandOk(wh)) return false;
            if (!WaterOk(port) || !WaterOk(dock)) return false;

            _cellRoad = road;
            _cellWarehouse = wh;
            _cellPort = port;
            _cellDock = dock;
            return true;
        }

        bool LandOk(Vector2Int c)
        {
            string g = _map.TileAt(c.x, c.y);
            if (string.IsNullOrEmpty(g)) return false;
            if (_map.IsWaterAt(c.x, c.y)) return false;
            if (_map.HeightAt(c.x, c.y) > 0.001f) return false;
            if (BuildController.IsOccupiedGround(g)) return false;
            return FactoryWorld.Instance.OccupantAt(c) == null;
        }

        bool WaterOk(Vector2Int c)
        {
            if (!_map.IsWaterAt(c.x, c.y)) return false;
            if (!HasAdjacentLand(c)) return false;
            if (_map.IsEnclosedWater(c.x, c.y)) return false;
            return FactoryWorld.Instance.OccupantAt(c) == null;
        }

        bool HasAdjacentLand(Vector2Int c)
        {
            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };
            foreach (var d in dirs)
            {
                Vector2Int n = c + d;
                string g = _map.TileAt(n.x, n.y);
                if (!string.IsNullOrEmpty(g) && !_map.IsWaterAt(n.x, n.y)) return true;
            }
            return false;
        }

        // ---------------- 开局七步 ----------------

        void ShowStep(int step, bool focus = true)
        {
            _step = step;
            _state = State.Basic;

            switch (step)
            {
                case 0:
                    ApplyCard("教程 1/7 · 先建一座仓库",
                        "王老板先给了你启动资金。第一步：建一座「小型仓库」（300 金）。\n" +
                        "点右下角「建造」→「小型仓库」→ 点高亮的格子。\n" +
                        "仓库是所有物资的中转站：港口捞的鱼、工厂要用的辅料，都从这里进出。",
                        null, null, true);
                    Highlight(_cellWarehouse, focus);
                    OpenBuildPanel();
                    break;

                case 1:
                    ApplyCard("教程 2/7 · 铺一格路面",
                        "仓库旁边再铺一格「路面」（30 金）。\n" +
                        "物资不靠马车，靠路面流转——路面把建筑连成一张「路网」，同一张路网里的仓库才能给工厂供货。\n" +
                        "注意：两块路面如果被建筑隔开，就是两张不同的路网，铺的时候记得绕开建筑连起来。",
                        null, null, true);
                    Highlight(_cellRoad, focus);
                    OpenBuildPanel();
                    break;

                case 2:
                    ApplyCard("教程 3/7 · 捕捞港口",
                        "在水里建一座「捕捞港口」（900 金）。港口只能建在水体里，而且必须挨着陆地。\n" +
                        "它的活是出海捞鱼，捞上来的鱼会送进相连的仓库。\n" +
                        "施工要 3 回合，先放下就行，后面点「下一回合」它会自己建好。",
                        null, null, true);
                    Highlight(_cellPort, focus);
                    OpenBuildPanel();
                    break;

                case 3:
                {
                    var port = FindPort();
                    int left = (port != null && port.UnderConstruction) ? port.BuildTurnsLeft : 0;
                    _lastPortTurns = left;

                    string wait = left > 0
                        ? $"<color=#FFC46B>港口还在施工：还剩 {left} 回合，先点右下角「下一回合」等它建好。</color>\n"
                        : "";

                    ApplyCard("教程 4/7 · 指定捕捞点",
                        wait +
                        "建好之后点一下捕捞港口，在面板里点「自动选最近的聚集点」。\n" +
                        "海产聚集点就是海里的鱼群：港口只捞指定点位的鱼，点位离港口越远，品质掉得越多。\n" +
                        "（没有聚集点也没关系，点「指定捕捞点」再点一片水面就行；以后要换点位也是这个操作。）",
                        null, null, true);
                    Highlight(_cellPort, focus);
                    break;
                }

                case 4:
                    ApplyCard("教程 5/7 · 货运码头",
                        "最后在水里再建一座「货运码头」（800 金），同样要挨着陆地。\n" +
                        "码头负责买卖：把仓库里的货卖成金币，也把工厂要用的辅料买回来。\n" +
                        "它和仓库靠路面（或直接四邻接）连在一起才算连通，连通了才做得了生意。",
                        null, null, true);
                    Highlight(_cellDock, focus);
                    OpenBuildPanel();
                    break;

                case 5:
                    ApplyCard("教程 6/7 · 下一回合",
                        "点右下角最右边的「下一回合」，多点几次，等捕捞港口和货运码头的施工条走完。\n" +
                        "一回合 = 游戏里的一天：港口捕捞、工厂生产、码头买卖都在这个时候结算，施工进度也按回合走。\n" +
                        "（键盘空格也是下一回合。）",
                        null, null, true);
                    HighlightOff();
                    break;

                default:
                    ApplyCard("教程 7/7 · 库存与买卖",
                        "点开货运码头面板看看：\n" +
                        "· 上半部分是仓库里能卖的货，点「售出」按品质换成金币；\n" +
                        "· 下半部分是工厂要用的辅料，点「购入」买回来；\n" +
                        "· 「自动供货 / 自动出货」可以交给系统托管，缺料自动买、成品自动卖。\n" +
                        "工厂还没解锁之前，就先捞鱼卖钱攒金币，再去完成王老板的订单解锁加工厂。",
                        "完成", FinishBasic, false);
                    Highlight(_cellDock, focus);
                    break;
            }
        }

        void EvaluateBasicStep()
        {
            switch (_step)
            {
                case 0:
                    if (HasTile<WarehouseBuilding>(_cellWarehouse)) ShowStep(1);
                    break;
                case 1:
                    if (HasTile<RoadTile>(_cellRoad)) ShowStep(2);
                    break;
                case 2:
                    if (HasTile<FishingPortBuilding>(_cellPort)) ShowStep(3);
                    break;
                case 3:
                {
                    var port = FindPort();
                    if (port == null) break;
                    if (port.HasTarget) { ShowStep(4); break; }

                    // 施工剩多少回合变了，就只刷新一张卡片（不重复拉镜头）
                    int left = port.UnderConstruction ? port.BuildTurnsLeft : 0;
                    if (left != _lastPortTurns) ShowStep(3, false);
                    break;
                }
                case 4:
                    if (HasTile<CargoDockBuilding>(_cellDock)) ShowStep(5);
                    break;
                case 5:
                    if (_turn != null && _turn.Turn > _startTurn && ConstructionDone()) ShowStep(6);
                    break;
            }
        }

        bool HasTile<T>(Vector2Int cell) where T : PlacedTile
        {
            if (FactoryWorld.Instance == null) return false;

            // 引导格上已经有这一项（读档续玩、或者玩家先建好了）就算过
            if (_cellGuided && FactoryWorld.Instance.OccupantAt(cell) is T) return true;

            // 否则只认教程开始之后新盖出来的
            foreach (var x in FindObjectsOfType<T>())
            {
                if (x != null && !_knownAtStart.Contains(x.GetInstanceID())) return true;
            }
            return false;
        }

        FishingPortBuilding FindPort()
        {
            if (FactoryWorld.Instance != null)
            {
                var p = FactoryWorld.Instance.OccupantAt(_cellPort) as FishingPortBuilding;
                if (p != null) return p;
            }

            foreach (var x in FindObjectsOfType<FishingPortBuilding>())
            {
                if (x != null && !_knownAtStart.Contains(x.GetInstanceID())) return x;
            }
            return null;
        }

        /// <summary>港口和码头都施工完了才算「建好」——施工条没走完之前先别进最后一步。</summary>
        bool ConstructionDone()
        {
            var port = FindPort();
            if (port != null && port.UnderConstruction) return false;

            CargoDockBuilding dock = null;
            if (_cellGuided && FactoryWorld.Instance != null)
                dock = FactoryWorld.Instance.OccupantAt(_cellDock) as CargoDockBuilding;
            if (dock == null)
            {
                foreach (var x in FindObjectsOfType<CargoDockBuilding>())
                {
                    if (x != null && !_knownAtStart.Contains(x.GetInstanceID())) { dock = x; break; }
                }
            }
            if (dock != null && dock.UnderConstruction) return false;

            return true;
        }

        void OpenBuildPanel()
        {
            if (!_cellGuided) return;
            var panel = BuildPanel.Instance;
            if (panel != null && !panel.IsShown) panel.Open();
        }

        void FinishBasic()
        {
            SaveSystem.FactoryBasicTutorialDone = true;
            SaveSystem.Save();
            Debug.Log("[教程] 工厂开局教程完成");
            EndCard();

            if (_pendingLand) { _pendingLand = false; ShowLandTutorial(); }
            else if (_pendingPower) { _pendingPower = false; ShowPowerTutorial(); }
            else if (!SaveSystem.FactorySailTutorialDone) BeginSailTutorial();
        }

        // ---------------- 远洋教程（航线 / 沉船宝藏 / 贵重品升异能 / 通关目标） ----------------

        int _sailStep;
        bool _sailPathTouched;

        void BeginSailTutorial()
        {
            if (SaveSystem.FactorySailTutorialDone) return;
            _state = State.Sail;
            _sailStep = 0;
            _sailPathTouched = false;
            ShowSailStep(0);
            Debug.Log("[教程] 远洋教程开始");
        }

        void ShowSailStep(int step)
        {
            _sailStep = step;
            _state = State.Sail;

            switch (step)
            {
                case 0:
                    ApplyCard("远洋 1/3 · 开船去看沉船和宝藏",
                        "点右下角「航线」（或者直接点一下渔船），再点海面的一格，船就会每回合沿航线走一格。\n" +
                        "海里散落着 36 艘沉船和一箱箱宝藏，船靠近到 5 格以内才会显形；离远了它们会变暗，开回去就恢复。\n" +
                        "看到沉船或宝箱后，把船开到那一格，点右下角「捕鱼」进海，用抓钩把它捞上来。",
                        "我记住了", () => ShowSailStep(1), false);
                    HighlightOff();
                    break;

                case 1:
                    ApplyCard("远洋 2/3 · 贵重品换异能",
                        "打捞上来的贵重品（南海黑珍珠 / 美乐珠 / 龙涎香）不能卖钱，专门用来升异能。\n" +
                        "回主菜单 → 点民居进堂屋 → 点大黄，就能花贵重品升级：\n" +
                        "· 南海黑珍珠 → 地形改造（拆地块、移草地）\n" +
                        "· 美乐珠 → 神眼·远眺（看得更远）\n" +
                        "· 龙涎香 → 动物交流·优质（渔获品质更高）",
                        "继续", () => ShowSailStep(2), false);
                    HighlightOff();
                    break;

                default:
                    ApplyCard("远洋 3/3 · 通关目标",
                        "最终目标：捞起 36 艘沉船，每艘沉船给 1 个「国宝」，集齐 36 个国宝即通关。\n" +
                        "· 沉船给国宝和贵重品，宝箱只给金币。\n" +
                        "· 出海只能白天：天一开始黑，船会自动返航，并且直接进入下一天（回合 +1）。\n" +
                        "· 时段只在捕鱼界面显示（左上角），出海前先看一眼，尽量白天把目标捞完。",
                        "开始探索", FinishSail, false);
                    HighlightOff();
                    break;
            }
        }

        void EvaluateSailStep()
        {
            if (_sailStep != 0) return;

            var picker = FindObjectOfType<TilePicker>();
            if (picker != null && picker.BoatPathMode) _sailPathTouched = true;
            if (!_sailPathTouched) return;

            // 玩家真的点开过航线模式，说明看懂了，自动进下一步
            ShowSailStep(1);
        }

        void FinishSail()
        {
            SaveSystem.FactorySailTutorialDone = true;
            SaveSystem.Save();
            _state = State.None;
            EndCard();
            Debug.Log("[教程] 远洋教程完成");
        }

        // ---------------- 地块组拼接 ----------------

        public static void OnLandPanelOpened()
        {
            if (Instance != null) Instance.ShowLandTutorial();
        }

        void ShowLandTutorial()
        {
            if (SaveSystem.FactoryLandTutorialDone) return;
            if (_state == State.Basic) { _pendingLand = true; return; }
            if (_state != State.None) { _pendingLand = true; return; }

            _state = State.LandInfo;
            HighlightOff();
            ApplyCard("教程 · 地块组拼接",
                "这是土地局批下来的地块组：3 个方案里选 1 个，选定后不能取消。\n" +
                "· 地块组是 2×3 或 3×2，按 R 键（或点屏幕下方的「旋转」）换方向；\n" +
                "· 整组必须落在水面上，并且至少有一格挨着已有陆地，不能扔到远海；\n" +
                "· 组里空着的格子会留成水面，正好可以拿来补已有的凹位；\n" +
                "· 放好之后，地块外围的深水会变成浅海，海岸线会自然过渡。",
                "知道了", AcknowledgeLand, false);

            Debug.Log("[教程] 地块组拼接教程");
        }

        void AcknowledgeLand()
        {
            SaveSystem.FactoryLandTutorialDone = true;
            SaveSystem.Save();
            EndCard();
            if (_pendingPower) { _pendingPower = false; ShowPowerTutorial(); }
        }

        // ---------------- 电力与电力设施 ----------------

        public static void OnPowerViewOpened()
        {
            if (Instance != null) Instance.ShowPowerTutorial();
        }

        public static void OnPowerFacilityBuilt()
        {
            if (Instance != null) Instance.ShowPowerTutorial();
        }

        void ShowPowerTutorial()
        {
            if (SaveSystem.FactoryPowerTutorialDone) return;
            if (_state != State.None) { _pendingPower = true; return; }

            _state = State.PowerInfo;
            HighlightOff();
            ApplyCard("教程 · 电力与电力设施",
                "打开电力图（右下角「电力」，键盘 P）会看到三种颜色：\n" +
                "· 灰色 = 没电，工厂会停工（缺电停机不扣日运营费）；\n" +
                "· 绿色 = 有电，是接收塔点亮的 7×7 范围；\n" +
                "· 红色 = 横竖两条电流束经过的格子，也算有电。\n" +
                "两座电力设施：\n" +
                "· 接收塔：截住电流束，把周围 7×7 点亮；\n" +
                "· 棱镜塔：电流束穿过时向两侧分叉，拉出新的束。\n" +
                "电流束每 30 回合换一次行列，别把工厂全压在同一条线上。一级烟熏房和一级预处理厂不需要电。",
                "知道了", AcknowledgePower, false);

            Debug.Log("[教程] 电力教程");
        }

        void AcknowledgePower()
        {
            SaveSystem.FactoryPowerTutorialDone = true;
            SaveSystem.Save();
            EndCard();
        }

        // ---------------- 高亮 ----------------

        void Highlight(Vector2Int cell, bool focus = true)
        {
            if (!_cellGuided) { HighlightOff(); return; }
            _hlCell = cell;
            _hlOn = true;
            EnsureHighlight();

            if (focus)
            {
                var cam = FindObjectOfType<MapCameraController>();
                if (cam != null)
                {
                    Vector3 p = EngineeringView.CellToWorld(cell.x, cell.y);
                    cam.FocusOn(p + new Vector3(0f, 0.66f, 0f));
                }
            }

            DrawHighlight();
        }

        void HighlightOff()
        {
            _hlOn = false;
            if (_hl != null) _hl.enabled = false;
        }

        void EnsureHighlight()
        {
            if (_hl != null) return;

            Transform old = transform.Find("TutorialHighlight");
            if (old != null) _hl = old.GetComponent<LineRenderer>();
            if (_hl == null)
            {
                var go = new GameObject("TutorialHighlight");
                go.transform.SetParent(transform, false);
                _hl = go.AddComponent<LineRenderer>();
                _hl.useWorldSpace = true;
                _hl.loop = true;
                _hl.positionCount = 4;
                _hl.widthMultiplier = 0.04f;
                _hl.numCapVertices = 0;
                _hl.numCornerVertices = 0;
                _hl.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                _hl.sortingOrder = 3000;
                _hl.enabled = false;
            }
        }

        void DrawHighlight()
        {
            EnsureHighlight();
            if (_hl == null) return;

            bool top = EngineeringView.Active;
            Vector3 c = EngineeringView.CellToWorld(_hlCell.x, _hlCell.y);

            if (top)
            {
                _hl.SetPosition(0, c + new Vector3(-0.5f, 0.5f, 0f));
                _hl.SetPosition(1, c + new Vector3(0.5f, 0.5f, 0f));
                _hl.SetPosition(2, c + new Vector3(0.5f, -0.5f, 0f));
                _hl.SetPosition(3, c + new Vector3(-0.5f, -0.5f, 0f));
            }
            else
            {
                c += new Vector3(0f, 0.66f, 0f);
                _hl.SetPosition(0, c + new Vector3(0f, 0.25f, 0f));
                _hl.SetPosition(1, c + new Vector3(0.5f, 0f, 0f));
                _hl.SetPosition(2, c + new Vector3(0f, -0.25f, 0f));
                _hl.SetPosition(3, c + new Vector3(-0.5f, 0f, 0f));
            }

            float a = 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
            Color col = new Color(1f, 0.86f, 0.35f, a);
            _hl.startColor = col;
            _hl.endColor = col;
            _hl.enabled = true;
        }

        // ---------------- 卡片 UI ----------------

        void EndCard()
        {
            _state = State.None;
            if (_card != null) _card.gameObject.SetActive(false);
            HighlightOff();
        }

        void ApplyCard(string title, string body, string actionLabel, System.Action onAction, bool showSkip)
        {
            if (_card == null) BuildUI();

            _card.gameObject.SetActive(true);
            _titleText.text = title;
            _bodyText.text = body;

            if (_actionBtn != null)
            {
                bool hasAction = !string.IsNullOrEmpty(actionLabel);
                _actionBtn.gameObject.SetActive(hasAction);
                if (hasAction)
                {
                    _actionLabel.text = actionLabel;
                    _actionBtn.onClick.RemoveAllListeners();
                    if (onAction != null) _actionBtn.onClick.AddListener(() => onAction());
                }
            }

            if (_skipGo != null) _skipGo.SetActive(showSkip);
            if (_hintText != null)
                _hintText.text = showSkip ? "跟着高亮格子走，或者点「跳过」" : "";

            // 按正文实际行数收放卡片高度
            Canvas.ForceUpdateCanvases();
            float bodyH = _bodyText.preferredHeight;
            float h = Mathf.Clamp(bodyH + 116f, 150f, 470f);
            _card.sizeDelta = new Vector2(cardWidth, h);
            _card.anchoredPosition = new Vector2(24f, -cardTop);
        }

        void BuildUI()
        {
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 18);

            var canvasGo = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = Camera.main;
            _canvas.planeDistance = 1f;
            _canvas.sortingOrder = 5095;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            _card = cardGo.GetComponent<RectTransform>();
            _card.SetParent(canvasGo.transform, false);
            _card.anchorMin = new Vector2(0f, 1f);
            _card.anchorMax = new Vector2(0f, 1f);
            _card.pivot = new Vector2(0f, 1f);
            _card.sizeDelta = new Vector2(cardWidth, 200f);
            _card.anchoredPosition = new Vector2(24f, -cardTop);
            cardGo.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 0.94f);

            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var accent = accentGo.GetComponent<RectTransform>();
            accent.SetParent(_card, false);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accentGo.GetComponent<Image>().color = new Color(1f, 0.80f, 0.42f, 1f);

            _titleText = MakeText(_card, "Title", 22, FontStyle.Bold, new Color(1f, 0.88f, 0.58f));
            _titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _titleText.rectTransform.sizeDelta = new Vector2(-36f, 28f);
            _titleText.rectTransform.anchoredPosition = new Vector2(18f, -12f);

            _bodyText = MakeText(_card, "Body", 17, FontStyle.Normal, new Color(0.88f, 0.92f, 0.95f));
            _bodyText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _bodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _bodyText.rectTransform.offsetMin = new Vector2(18f, 58f);
            _bodyText.rectTransform.offsetMax = new Vector2(-18f, -46f);
            _bodyText.lineSpacing = 1.12f;

            _hintText = MakeText(_card, "Hint", 15, FontStyle.Normal, new Color(0.62f, 0.70f, 0.76f));
            _hintText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _hintText.rectTransform.anchorMax = new Vector2(0.6f, 0f);
            _hintText.rectTransform.pivot = new Vector2(0f, 0f);
            _hintText.rectTransform.sizeDelta = new Vector2(0f, 26f);
            _hintText.rectTransform.anchoredPosition = new Vector2(18f, 16f);

            _actionBtn = MakeButton(_card, "Action", "完成", out _actionLabel, 150f, 40f,
                                    new Color(0.20f, 0.42f, 0.50f, 1f));
            var aRect = _actionBtn.GetComponent<RectTransform>();
            aRect.anchorMin = new Vector2(1f, 0f);
            aRect.anchorMax = new Vector2(1f, 0f);
            aRect.pivot = new Vector2(1f, 0f);
            aRect.anchoredPosition = new Vector2(-16f, 12f);

            var skip = MakeButton(_card, "Skip", "跳过", out _, 76f, 30f, new Color(0.18f, 0.20f, 0.24f, 0.95f));
            var sRect = skip.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(1f, 1f);
            sRect.anchorMax = new Vector2(1f, 1f);
            sRect.pivot = new Vector2(1f, 1f);
            sRect.anchoredPosition = new Vector2(-10f, -10f);
            skip.onClick.AddListener(FinishBasic);
            _skipGo = skip.gameObject;

            cardGo.SetActive(false);
        }

        Text MakeText(RectTransform parent, string name, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Button MakeButton(RectTransform parent, string name, string label, out Text labelText,
                          float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(w, h);
            go.GetComponent<Image>().color = color;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            btn.colors = colors;

            labelText = MakeText(rect, "Label", 18, FontStyle.Normal, new Color(0.95f, 0.97f, 1f));
            labelText.rectTransform.anchorMin = Vector2.zero;
            labelText.rectTransform.anchorMax = Vector2.one;
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.text = label;

            return btn;
        }
    }
}
