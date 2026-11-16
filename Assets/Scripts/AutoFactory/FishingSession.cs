using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 「捕鱼」入口：从工厂地图叠加加载 2D 捕鱼场景（SampleScene），
    /// 把工厂场景整体挂起（根物体 SetActive(false)，相机/UI/输入一起停），
    /// 返回时把这一趟的渔获写进渔船船舱，再卸载捕鱼场景、恢复工厂。
    /// 渔船必须停在水面上才能进入；如果脚下是海产聚集点，这片海只刷那个品种。
    /// </summary>
    public class FishingSession : MonoBehaviour
    {
        public static FishingSession Instance { get; private set; }
        public static bool Active => Instance != null && Instance._active;

        [Header("捕鱼场景")]
        public string fishingSceneName = "SampleScene";

        [Header("开阔海域")]
        public int baseDepth = 50;
        public int depthJitter = 3;
        public float surfaceY = 16f;
        public float boatStartX = 40f;

        bool _active;
        bool _busy;
        Scene _factoryScene;
        Scene _fishingScene;
        readonly List<GameObject> _frozenRoots = new List<GameObject>();

        PlayerBoat _fishingBoat;
        BoatStatus _fishingStatus;
        PlayerBoatVehicle _factoryBoat;   // 工厂场景的渔船（进入时缓存：工厂物体被禁用后 FindObjectOfType 找不到它）
        GameObject _returnCanvas;
        Text _hudText;

        Canvas _canvas;
        GameObject _arrowGo;
        RectTransform _arrowRect;
        Sprite _arrowSprite;
        Transform _wreckTarget;          // 海底的沉船/宝藏（这片海域只出现一个）
        Vector2Int _wreckCell;           // 目标在工厂地图上的格子（拉上来时标记已打捞）
        TimeOfDaySystem _timeSystem;     // 到天黑自动收船
        string _notice;
        float _noticeUntil;

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!_active) return;

            // 到天黑自动收船回工厂
            if (_timeSystem != null && _timeSystem.IsNight)
            {
                Debug.Log("[捕鱼] 天黑了，自动返回工厂");
                Exit();
                return;
            }

            if (_hudText != null)
            {
                if (Time.unscaledTime < _noticeUntil) _hudText.text = _notice;
                else if (_fishingStatus != null)
                    _hudText.text = $"渔获 {_fishingStatus.Load:0.0} / {_fishingStatus.MaxLoad:0} KG\n" +
                                    "回到工厂后自动装进渔船船舱";
            }

            UpdateWreckArrow();
        }

        /// <summary>点「捕鱼」按钮：检查渔船是不是停在水面上，然后进入。</summary>
        public void Enter()
        {
            if (_active || _busy) return;

            // 进捕鱼前先把工厂进度落盘（捕鱼期间工厂场景是挂起的）
            SaveSystem.Save();

            var boat = FindObjectOfType<PlayerBoatVehicle>();
            if (boat == null)
            {
                Notify("找不到渔船，先把渔船开到水面上再来。");
                return;
            }
            _factoryBoat = boat;

            var map = FindObjectOfType<IsoTileMap>();
            string ground = map != null ? map.TileAt(boat.gridX, boat.gridY) : null;
            bool water = string.IsNullOrEmpty(ground) || (map != null && map.IsWaterAt(boat.gridX, boat.gridY));
            if (!water)
            {
                Notify("渔船现在停在陆地上，先开到水面再捕鱼。");
                return;
            }

            // 脚下的海产聚集点决定这一片海域的鱼种
            string species = null;
            if (FactoryWorld.Instance != null)
            {
                var spot = FactoryWorld.Instance.OccupantAt(boat.Cell) as SeaSpotBuilding;
                if (spot != null) species = spot.species;
            }

            // 脚下是沉船 / 宝藏：这片海域会随机出现一个对应目标（只出现一个）
            int wreckKind = -1;
            var site = WreckSiteSystem.GetSite(boat.Cell);
            if (site != null && !site.salvaged)
            {
                wreckKind = site.kind;
                _wreckCell = boat.Cell;
            }

            StartCoroutine(EnterRoutine(species, wreckKind));
        }

        IEnumerator EnterRoutine(string species, int wreckKind)
        {
            _busy = true;
            _factoryScene = SceneManager.GetActiveScene();

            // 之前没卸载干净的捕鱼场景先清掉：叠加出两份 SampleScene 会出现两份地形/鱼，
            // 沉底鱼会按另一份（渔港）的海床高度浮在水体半空。
            Scene existing = SceneManager.GetSceneByName(fishingSceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                AsyncOperation cleanup = SceneManager.UnloadSceneAsync(existing);
                if (cleanup != null) yield return cleanup;
            }

            // 1) 挂起工厂场景：根物体全禁用（相机、UI、输入、渲染一起停，Update 也不再跑）
            _frozenRoots.Clear();
            foreach (var root in _factoryScene.GetRootGameObjects())
            {
                if (root == null || !root.activeSelf) continue;
                _frozenRoots.Add(root);
                root.SetActive(false);
            }

            // 2) 工厂的 GameAudio 跟着禁用了，把静态引用让给捕鱼场景
            GameAudio.ClearInstance();

            // 3) 叠加加载捕鱼场景
            AsyncOperation load = SceneManager.LoadSceneAsync(fishingSceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                RestoreFactory();
                Notify("捕鱼场景加载失败（检查 Build Settings 里有没有 SampleScene）。");
                _busy = false;
                yield break;
            }
            yield return load;

            _fishingScene = SceneManager.GetSceneByName(fishingSceneName);
            if (!_fishingScene.IsValid() || !_fishingScene.isLoaded)
            {
                RestoreFactory();
                Notify("捕鱼场景没有加载成功。");
                _busy = false;
                yield break;
            }

            SceneManager.SetActiveScene(_fishingScene);
            yield return null;   // 让新场景的 Awake / Start 跑完

            ConfigureFishingScene(species);
            SpawnWreckTarget(wreckKind);

            // 先切到可操作状态，再建返回按钮：万一 UI 出问题也不会卡在"进不去也退不出"
            _active = true;
            _busy = false;
            BuildReturnUI();

            Debug.Log($"[捕鱼] 进入开阔海域（海床约 {baseDepth}±{depthJitter} 格）" +
                      (string.IsNullOrEmpty(species) ? "，全鱼种" : $"，只刷「{species}」"));
        }

        void ConfigureFishingScene(string species)
        {
            GameObject[] roots = _fishingScene.GetRootGameObjects();

            foreach (var r in roots)
            {
                // 这一趟没有码头，也没有冷链车
                if (r.name == "DockPier" || r.name == "复古像素冷藏货车侧视图")
                    r.SetActive(false);
                // 捕鱼时用我们自己的「返回工厂」按钮，关掉通用菜单避免误点返回码头
                else if (r.name == "MenuUI")
                    r.SetActive(false);
            }

            GameObject boatRoot = FindRoot(roots, "PlayerBoat");
            if (boatRoot != null)
            {
                _fishingBoat = boatRoot.GetComponent<PlayerBoat>();
                _fishingStatus = boatRoot.GetComponent<BoatStatus>();

                boatRoot.transform.position = new Vector3(boatStartX, surfaceY + 0.5f, 0f);

                if (_fishingBoat != null)
                {
                    // 开阔海域：左右都没有空气墙
                    _fishingBoat.SetSailLimits(-1e9f, 1e9f);

                    // 捕鱼入口专用外观：海神号捕捞船（素材船头朝左）
                    Sprite hull = Resources.Load<Sprite>("Image/渔船/海神号");
                    if (hull != null) _fishingBoat.SetBoatSprite(hull, false);
                }

                // 不显示主角，鱼线/抓钩从船尾绞盘伸出；普通模式仍用原来的鱼钩
                var qin = boatRoot.GetComponentInChildren<QinJunActor>(true);
                if (qin != null)
                {
                    qin.SetBoatAnchorMode(true);

                    // 抓钩的 4 帧（张开 → 闭合），只在船下方有沉船/宝藏时启用
                    var frames = new List<Sprite>();
                    for (int k = 1; k <= 4; k++)
                    {
                        Sprite f = Resources.Load<Sprite>($"Image/道具/抓钩/抓钩_{k}");
                        if (f != null) frames.Add(f);
                    }
                    if (frames.Count > 0) qin.SetClawSprites(frames.ToArray());
                }
                if (_fishingStatus != null)
                {
                    _fishingStatus.SetDockless();   // 不靠港、不补油、不结算

                    // 把工厂船的货带进来：不然捕鱼界面一开始永远显示 0 / 1500KG
                    if (_factoryBoat != null)
                        _fishingStatus.SeedCargo(_factoryBoat.CargoWeight, _factoryBoat.cargo.Count);
                }
            }

            var terrain = FindComponent<SandbarTerrain>(roots);
            if (terrain != null)
                terrain.ConfigureOpenSea(surfaceY, baseDepth, depthJitter);

            _timeSystem = FindComponent<TimeOfDaySystem>(roots);

            var spawner = FindComponent<FishSpawner>(roots);
            if (spawner != null)
            {
                spawner.SetSpeciesFilter(species);
                // 地形刚按开阔海域重建过：清掉可能按旧（渔港）地形刷出来的鱼，等新地形就绪重刷
                spawner.ResetForNewSea();
            }
        }

        // ---------------- 沉船 / 宝藏目标 ----------------

        public bool HasWreckTarget => _wreckTarget != null;

        public Vector2 WreckPosition =>
            _wreckTarget != null ? (Vector2)_wreckTarget.position : Vector2.zero;

        /// <summary>海底目标的贴图包围盒（抓钩按它判断有没有碰到，比固定半径稳）。</summary>
        public Bounds WreckBounds
        {
            get
            {
                if (_wreckTarget == null) return new Bounds(Vector3.zero, Vector3.zero);
                var sr = _wreckTarget.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null) return sr.bounds;
                return new Bounds(_wreckTarget.position, Vector3.one);
            }
        }

        /// <summary>抓钩拖着目标上浮时用。</summary>
        public void SetWreckPosition(Vector3 p)
        {
            if (_wreckTarget == null) return;
            _wreckTarget.position = new Vector3(p.x, p.y, _wreckTarget.position.z);
        }

        /// <summary>海底目标是否就在船的正下方（决定这一竿用钩子还是抓钩）。</summary>
        public bool IsWreckUnderBoat(float boatX, float range = 3f)
        {
            return _wreckTarget != null && Mathf.Abs(_wreckTarget.position.x - boatX) <= range;
        }

        /// <summary>抓钩把目标拉到水面：宝物进贵重品库、标记这处已打捞、目标消失。</summary>
        public void CollectWreck()
        {
            string lootName = null;
            bool treasure = false;
            var site = WreckSiteSystem.GetSite(_wreckCell);
            if (site != null)
            {
                lootName = site.loot;
                treasure = site.kind == 1;
                if (!site.salvaged)
                {
                    if (WreckSiteSystem.Instance != null) WreckSiteSystem.Instance.Salvage(_wreckCell);
                    else site.salvaged = true;
                }
            }

            int gold = WreckSiteSystem.LastSalvageGold;

            if (_wreckTarget != null)
            {
                Destroy(_wreckTarget.gameObject);
                _wreckTarget = null;
            }

            if (treasure)
                ShowNotice($"抓钩捞上宝箱：+{gold:N0} 金");
            else if (string.IsNullOrEmpty(lootName))
                ShowNotice("抓钩上来了：这次什么也没捞到");
            else
                ShowNotice($"抓钩捞到「{lootName}」！已放进贵重品库");
        }

        void ShowNotice(string message)
        {
            _notice = message;
            _noticeUntil = Time.unscaledTime + 5f;
            Debug.Log("[抓钩] " + message);
        }

        /// <summary>脚下是沉船/宝藏时，在海床上随机生成一个目标（这片海域只出现一个）。</summary>
        void SpawnWreckTarget(int kind)
        {
            if (kind < 0) return;

            string path = kind == 1 ? "Image/渔场/宝箱_2d" : "Image/渔场/沉船_2d";
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning("[捕鱼] 找不到目标贴图 " + path);
                return;
            }

            float x = boatStartX + Random.Range(-30f, 30f);
            float floorY = surfaceY - baseDepth;                          // 海床顶面
            float halfH = sprite.rect.height / sprite.pixelsPerUnit * 0.5f;

            var go = new GameObject(kind == 1 ? "FishingTreasure" : "FishingWreck");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 3;                                          // 和鱼同层：泡在水色下面
            go.transform.position = new Vector3(x, floorY + halfH, 0f);
            if (_fishingScene.IsValid()) SceneManager.MoveGameObjectToScene(go, _fishingScene);

            _wreckTarget = go.transform;
            Debug.Log($"[捕鱼] 海里有{(kind == 1 ? "宝藏" : "沉船")}：({x:0},{floorY + halfH:0})");
        }

        /// <summary>方向箭头：目标在屏幕外时贴在屏幕边缘指向它，进屏幕就隐藏。</summary>
        void UpdateWreckArrow()
        {
            if (_arrowGo == null || _wreckTarget == null || _canvas == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(_wreckTarget.position);
            const float margin = 70f;
            bool onScreen = sp.z > 0f &&
                            sp.x > margin && sp.x < Screen.width - margin &&
                            sp.y > margin && sp.y < Screen.height - margin;

            _arrowGo.SetActive(!onScreen);
            if (onScreen) return;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = new Vector2(sp.x, sp.y) - center;
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            dir.Normalize();

            float halfW = Screen.width * 0.5f - margin;
            float halfH = Screen.height * 0.5f - margin;
            float tx = Mathf.Abs(dir.x) > 1e-4f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 1e-4f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
            Vector2 pos = center + dir * Mathf.Min(tx, ty);

            float scaleFactor = Mathf.Max(0.01f, _canvas.scaleFactor);
            _arrowRect.anchoredPosition = (pos - center) / scaleFactor;
            _arrowRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        }

        static Sprite MakeArrowSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            for (int y = 4; y < size - 4; y++)
            {
                float t = 1f - (y - 4f) / (size - 8f);
                int half = Mathf.RoundToInt(t * (size * 0.5f - 6f));
                for (int x = size / 2 - half; x <= size / 2 + half; x++)
                    if (x >= 0 && x < size) tex.SetPixel(x, y, Color.white);
            }

            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // ---------------- 返回工厂 ----------------

        public void Exit()
        {
            if (!_active || _busy) return;
            StartCoroutine(ExitRoutine());
        }

        IEnumerator ExitRoutine()
        {
            _busy = true;
            _active = false;

            // 1) 渔获写进工厂渔船的船舱（超出容量的部分会丢，日志里能看到）
            var boatVehicle = _factoryBoat;
            float gained = 0f;
            if (boatVehicle != null && _fishingStatus != null)
            {
                float quality = AbilityData.CatchQuality;   // 动物交流·优质：每级 +5%
                foreach (var kv in _fishingStatus.CatchByName)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value <= 0f) continue;
                    gained += boatVehicle.AddCargo(kv.Key, kv.Value, quality);
                }
            }
            Debug.Log($"[捕鱼] 返回：本趟渔获写进船舱 {gained:0.0}KG");

            // 2) 销毁返回按钮
            if (_returnCanvas != null) Destroy(_returnCanvas);
            _returnCanvas = null;
            _hudText = null;

            // 3) 卸载捕鱼场景（最多等 5 秒，别把协程卡死在卸载上）
            if (_fishingScene.IsValid() && _fishingScene.isLoaded)
            {
                if (_factoryScene.IsValid()) SceneManager.SetActiveScene(_factoryScene);
                AsyncOperation unload = SceneManager.UnloadSceneAsync(_fishingScene);
                if (unload != null)
                {
                    float t0 = Time.realtimeSinceStartup;
                    while (!unload.isDone && Time.realtimeSinceStartup - t0 < 5f) yield return null;
                }
            }

            // 4) 恢复工厂场景
            RestoreFactory();
            _busy = false;

            // 5) 回工厂自动过一回合：船舱贴着路边/仓库时会顺手卸货，工厂也走一步
            var turn = FindObjectOfType<TurnManager>();
            if (turn != null) turn.EndTurn();
            else if (boatVehicle != null) boatVehicle.UnloadToWarehouses(silent: true);

            // 6) 国宝集齐（捞完 36 条沉船）就播授勋漫画
            var comic = FindObjectOfType<TreasureComicUI>();
            if (comic != null) comic.TryPlayIfComplete();
        }

        void RestoreFactory()
        {
            for (int k = 0; k < _frozenRoots.Count; k++)
                if (_frozenRoots[k] != null) _frozenRoots[k].SetActive(true);
            _frozenRoots.Clear();

            if (_factoryScene.IsValid()) SceneManager.SetActiveScene(_factoryScene);
            GameAudio.RebindFromScene();

            _fishingBoat = null;
            _fishingStatus = null;
            _wreckTarget = null;
            _arrowGo = null;
            _arrowRect = null;
            _canvas = null;
        }

        // ---------------- UI ----------------

        void BuildReturnUI()
        {
            var canvasGo = new GameObject("FishingReturnCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6200;   // 高于工厂 UI(5000+) 与捕鱼 HUD

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);

            // 左下角：渔获信息（Image 与 Text 不能挂在同一个物体上，Text 放子物体）
            var hudGo = new GameObject("CatchHud", typeof(RectTransform), typeof(Image));
            var hudRect = hudGo.GetComponent<RectTransform>();
            hudRect.SetParent(canvasGo.transform, false);
            hudRect.anchorMin = new Vector2(0f, 0f);
            hudRect.anchorMax = new Vector2(0f, 0f);
            hudRect.pivot = new Vector2(0f, 0f);
            hudRect.sizeDelta = new Vector2(420f, 84f);
            hudRect.anchoredPosition = new Vector2(24f, 104f);
            hudGo.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.10f, 0.85f);

            var hudTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var hudTextRect = hudTextGo.GetComponent<RectTransform>();
            hudTextRect.SetParent(hudRect, false);
            hudTextRect.anchorMin = Vector2.zero;
            hudTextRect.anchorMax = Vector2.one;
            hudTextRect.offsetMin = new Vector2(14f, 6f);
            hudTextRect.offsetMax = new Vector2(-14f, -6f);

            _hudText = hudTextGo.GetComponent<Text>();
            _hudText.font = font;
            _hudText.fontSize = 20;
            _hudText.color = new Color(0.93f, 0.96f, 0.98f);
            _hudText.alignment = TextAnchor.MiddleLeft;
            _hudText.verticalOverflow = VerticalWrapMode.Overflow;

            // 左下角：返回工厂
            var btnGo = new GameObject("ReturnFactory", typeof(RectTransform), typeof(Image), typeof(Button));
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.SetParent(canvasGo.transform, false);
            btnRect.anchorMin = new Vector2(0f, 0f);
            btnRect.anchorMax = new Vector2(0f, 0f);
            btnRect.pivot = new Vector2(0f, 0f);
            btnRect.sizeDelta = new Vector2(300f, 62f);
            btnRect.anchoredPosition = new Vector2(24f, 26f);
            btnGo.GetComponent<Image>().color = new Color(0.20f, 0.44f, 0.34f, 1f);

            var button = btnGo.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            button.colors = colors;
            button.onClick.AddListener(Exit);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(btnRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<Text>();
            label.font = font;
            label.fontSize = 24;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.96f, 0.98f, 1f);
            label.alignment = TextAnchor.MiddleCenter;
            label.text = "返回工厂";

            // 方向箭头：指向海底的沉船 / 宝藏（只在屏幕外时贴在边缘）
            _canvas = canvas;
            _arrowGo = new GameObject("WreckArrow", typeof(RectTransform), typeof(Image));
            _arrowRect = _arrowGo.GetComponent<RectTransform>();
            _arrowRect.SetParent(canvasGo.transform, false);
            _arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            _arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowRect.pivot = new Vector2(0.5f, 0.5f);
            _arrowRect.sizeDelta = new Vector2(64f, 64f);

            var arrowImage = _arrowGo.GetComponent<Image>();
            if (_arrowSprite == null) _arrowSprite = MakeArrowSprite();
            arrowImage.sprite = _arrowSprite;
            arrowImage.color = new Color(1f, 0.85f, 0.35f, 0.95f);
            arrowImage.raycastTarget = false;
            _arrowGo.SetActive(false);

            _returnCanvas = canvasGo;
        }

        void Notify(string message)
        {
            Debug.Log("[捕鱼] " + message);
            var panel = TileInfoPanel.Instance;
            if (panel != null) panel.Show("捕鱼", message);
        }

        // ---------------- 小工具 ----------------

        static GameObject FindRoot(GameObject[] roots, string name)
        {
            foreach (var g in roots)
                if (g != null && g.name == name) return g;
            return null;
        }

        static T FindComponent<T>(GameObject[] roots) where T : Component
        {
            foreach (var g in roots)
            {
                if (g == null) continue;
                var c = g.GetComponent<T>();
                if (c != null) return c;
                c = g.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }
    }
}
