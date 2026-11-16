using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shenyan
{
    /// <summary>
    /// 主界面逻辑，四层地图：
    ///   村落层（默认）——点房子进堂屋、点桥船开始游戏、点车出大地图
    ///   大地图层——点右下角渔村回村落、点左下角小镇进酒楼
    ///   酒楼层——点门口的蓝色轿车回大地图
    ///   堂屋层——点木门回村落，少女/大黄有对话
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] string gameSceneName = "SampleScene";
        [SerializeField] DialogueUI dialogue;

        [Header("村落层")]
        [SerializeField] GameObject[] villageObjects;
        [Tooltip("买到海神号后替换的渔村背景（自带海神号停泊）")]
        [SerializeField] Sprite villageBgWithShip;

        [Header("大地图层")]
        [SerializeField] GameObject[] bigMapObjects;

        [Header("酒楼层")]
        [SerializeField] GameObject[] restaurantObjects;

        [Header("会议厅层")]
        [SerializeField] GameObject[] roomObjects;

        [Header("堂屋层")]
        [SerializeField] GameObject[] tangwuObjects;

        [Header("商店层")]
        [SerializeField] GameObject[] shopObjects;

        [Header("住屋层")]
        [SerializeField] GameObject[] homeObjects;

        [Header("天赋面板")]
        [SerializeField] GameObject[] talentObjects;

        static readonly string[] QinLanLines =
        {
            "哥，你又要出海啊？早点回来。",
            "锅里给你留了粥，凉了自己热一下。",
            "妈说今晚风大，让你别去太远。"
        };

        // 秦俊能听懂动物语言，所以大黄的话直接写成第一人称
        static readonly string[] DaHuangLines =
        {
            "汪！今天海边的风里全是鱼味儿。",
            "带我一起去嘛，我能帮你把鱼叼回来。",
            "你昨天答应的肉骨头，还没给呢。"
        };

        // 王老板：实诚人，主角捞的鱼都批发给他
        static readonly string[] WangBossLines =
        {
            "今天的鱼我都要了，按老价钱算。",
            "你这鱼新鲜，我从来不压价。",
            "海上辛苦，多吃点，别亏着自己。"
        };

        // 王雪：拒绝见周立成之后的闲聊
        static readonly string[] WangXueChatLines =
        {
            "那下次吧，我随时都能带你去。",
            "我爸那人就是实诚，你别嫌他话多。",
            "海上有事就来找我们。"
        };

        // 张妈：秦俊的妈，一个人住在城里
        static readonly string[] ZhangMaLines =
        {
            "俊儿回来啦？饭在锅里温着呢。",
            "妈一个人住惯了，你别老惦记家里。",
            "海上风大，记得多穿件衣裳。"
        };

        // 周立成：会议厅里的笑容绅士
        static readonly string[] ZhouLines =
        {
            "久仰，秦俊是吧？王雪常提起你。",
            "海上的生意，我这边能给你条路。",
            "先坐，我们慢慢谈。"
        };

        void Awake()
        {
            if (dialogue == null) dialogue = GetComponentInChildren<DialogueUI>(true);
        }

        void Start()
        {
            ShowVillage();

            // 新游戏（还没看过开场）：进主菜单就直接播，不用点桥船
            if (!SaveSystem.IntroPlayed)
            {
                var intro = FindObjectOfType<IntroUI>();
                if (intro != null) intro.Play();
            }
        }

        /// <summary>开始游戏：直接进渔港场景（开场在进入主菜单时已经播过）。</summary>
        public void PlayGame()
        {
            Debug.Log("[主菜单] 开始游戏 -> " + gameSceneName);
            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>点车：出大地图。</summary>
        public void OpenBigMap()
        {
            if (dialogue != null) dialogue.Close();
            SetGroup(tangwuObjects, false);
            SetGroup(villageObjects, false);
            SetGroup(restaurantObjects, false);
            SetGroup(bigMapObjects, true);
            SetAmb(GameAudio.Ambience.None);   // 大地图不放循环音，改用一次性点火声
            GameAudio.Ensure().PlayIgnition();
            Debug.Log("[主菜单] 开车出大地图");
        }

        /// <summary>点大地图右下角的渔村：回村落。</summary>
        public void CloseBigMap()
        {
            ShowVillage();
            Debug.Log("[主菜单] 回到渔港村落");
        }

        /// <summary>点大地图左下角的小镇：进酒楼。</summary>
        public void OpenRestaurant()
        {
            SetGroup(bigMapObjects, false);
            SetGroup(restaurantObjects, true);
            SetAmb(GameAudio.Ambience.None);   // 酒楼：不再放喧闹声
            Debug.Log("[主菜单] 进海鲜汇大酒楼");
        }

        /// <summary>点酒楼门口的轿车：回大地图。</summary>
        public void CloseRestaurant()
        {
            SetGroup(restaurantObjects, false);
            SetGroup(bigMapObjects, true);
            SetAmb(GameAudio.Ambience.None);
            GameAudio.Ensure().PlayIgnition();   // 从酒楼开回大地图也点火一次
            Debug.Log("[主菜单] 从酒楼回大地图");
        }

        /// <summary>点左边的房子：堂屋浮上来盖住村落地图。</summary>
        public void OpenTangwu()
        {
            SetGroup(tangwuObjects, true);
            SetShipElements(false);             // 堂屋盖上来时，别让渔船/轿车还在后面晃
            SetAmb(GameAudio.Ambience.Waves);   // 堂屋叠在村落地图上，沿用海浪
            Debug.Log("[主菜单] 进入堂屋");
        }

        /// <summary>堂屋里点木门：收起堂屋层，回到村落地图。</summary>
        public void CloseTangwu()
        {
            SetGroup(tangwuObjects, false);
            if (dialogue != null) dialogue.Close();
            ApplyShipState();
            Debug.Log("[主菜单] 回到村落地图");
        }

        public void TalkToQinLan()
        {
            if (dialogue == null) dialogue = GetComponentInChildren<DialogueUI>(true);
            if (dialogue == null)
            {
                Debug.LogWarning("[主菜单] 没找到 DialogueUI");
                return;
            }
            dialogue.Show("秦兰", QinLanLines);
        }

        public void TalkToDaHuang()
        {
            if (dialogue == null) dialogue = GetComponentInChildren<DialogueUI>(true);
            if (dialogue == null)
            {
                Debug.LogWarning("[主菜单] 没找到 DialogueUI");
                return;
            }
            dialogue.ShowChoice("大黄", "老大，你又有奇怪的感觉了？",
                new[] { "看看天赋", "没事" },
                i =>
                {
                    if (i == 0) OpenTalent();
                    else dialogue.Show("大黄", DaHuangLines);
                });
        }

        /// <summary>天赋面板（复用渔具店边框）。</summary>
        public void OpenTalent()
        {
            if (dialogue != null) dialogue.Close();
            SetGroup(talentObjects, true);
            SetShipElements(false);
            Debug.Log("[主菜单] 打开天赋面板");
        }

        public void CloseTalent()
        {
            SetGroup(talentObjects, false);
            ApplyShipState();
            Debug.Log("[主菜单] 关闭天赋面板");
        }

        /// <summary>王老板：顺手推销装备，选「看看装备」开商店。</summary>
        public void TalkToWangBoss()
        {
            if (dialogue == null) dialogue = GetComponentInChildren<DialogueUI>(true);
            if (dialogue == null) return;

            dialogue.ShowChoice("王老板", "想换套装备？尽管提，我认识人，帮你便宜订。",
                new[] { "看看装备", "不用了" },
                i =>
                {
                    if (i == 0) OpenShop();
                    else dialogue.Show("王老板", WangBossLines);   // 不买就照常收鱼闲聊
                });
        }

        /// <summary>王雪：问要不要去见周立成。选「好啊」进会议厅，选「下次吧」接着闲聊。</summary>
        public void TalkToWangXue()
        {
            if (dialogue == null) dialogue = GetComponentInChildren<DialogueUI>(true);
            if (dialogue == null) return;

            dialogue.ShowChoice("王雪", "秦俊，要不要我带你去见周立成？",
                new[] { "好啊，麻烦你了", "下次吧" },
                i =>
                {
                    if (i == 0) OpenRoom();
                    else dialogue.Show("王雪", WangXueChatLines);
                });
        }

        /// <summary>点大地图上的城市：去城里的住屋（压在放大的大地图上，带遮罩）。</summary>
        public void OpenHome()
        {
            if (dialogue != null) dialogue.Close();
            SetGroup(villageObjects, false);
            SetGroup(restaurantObjects, false);
            SetGroup(roomObjects, false);
            SetGroup(tangwuObjects, false);
            // 大地图留着当底
            SetGroup(bigMapObjects, true);
            SetGroup(homeObjects, true);
            SetAmb(GameAudio.Ambience.None);   // 住屋：安静
            Debug.Log("[主菜单] 进城里的住屋");
        }

        /// <summary>住屋点门：回大地图。</summary>
        public void CloseHome()
        {
            if (dialogue != null) dialogue.Close();
            SetGroup(homeObjects, false);
            SetGroup(bigMapObjects, true);
            SetAmb(GameAudio.Ambience.None);
            GameAudio.Ensure().PlayIgnition();
            Debug.Log("[主菜单] 从住屋回大地图");
        }

        /// <summary>住屋里的张妈。</summary>
        public void TalkToZhangMa()
        {
            if (dialogue == null) dialogue = GetComponentInChildren<DialogueUI>(true);
            if (dialogue == null) return;
            dialogue.Show("张妈", ZhangMaLines);
        }

        /// <summary>开商店窗口（带遮罩，压在酒楼/大地图等任意界面上）。</summary>
        public void OpenShop()
        {
            if (dialogue != null) dialogue.Close();
            SetGroup(shopObjects, true);
            SetShipElements(false);             // 商店是叠加层，别让后面的渔船/轿车继续晃
            Debug.Log("[主菜单] 打开渔具商店");
        }

        public void CloseShop()
        {
            SetGroup(shopObjects, false);
            ApplyShipState();
            Debug.Log("[主菜单] 关闭渔具商店");
        }

        /// <summary>进会议厅（周立成）：酒楼留着当底，上面压半透明黑遮罩 + 房间。</summary>
        public void OpenRoom()
        {
            if (dialogue != null) dialogue.Close();
            SetGroup(villageObjects, false);
            SetGroup(bigMapObjects, false);
            SetGroup(tangwuObjects, false);
            // 酒楼层不隐藏：会议厅是叠在酒楼上面的半透明弹层
            SetGroup(restaurantObjects, true);
            SetGroup(roomObjects, true);
            SetAmb(GameAudio.Ambience.None);
            Debug.Log("[主菜单] 进会议厅");
        }

        /// <summary>会议厅点门：回酒楼。</summary>
        public void CloseRoom()
        {
            if (dialogue != null) dialogue.Close();
            SetGroup(roomObjects, false);
            SetGroup(restaurantObjects, true);
            SetAmb(GameAudio.Ambience.None);
            Debug.Log("[主菜单] 出会议厅回酒楼");
        }

        /// <summary>会议厅里的周立成。</summary>
        public void TalkToZhouLicheng()
        {
            if (dialogue == null) dialogue = GetComponentInChildren<DialogueUI>(true);
            if (dialogue == null) return;
            dialogue.Show("周立成", ZhouLines);
        }

        /// <summary>还没接功能的元素，先打日志占位。</summary>
        public void NotReady(string what)
        {
            Debug.Log("[主菜单] 点击了「" + what + "」，功能还没接");
        }

        public void QuitGame()
        {
            Debug.Log("[主菜单] 退出游戏");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ShowVillage()
        {
            SetGroup(bigMapObjects, false);
            SetGroup(restaurantObjects, false);
            SetGroup(roomObjects, false);
            SetGroup(shopObjects, false);
            SetGroup(homeObjects, false);
            SetGroup(talentObjects, false);
            SetGroup(tangwuObjects, false);
            SetGroup(villageObjects, true);
            ApplyShipState();                   // 买了海神号：换背景 + 隐藏旧元素 + 显示入口
            SetAmb(GameAudio.Ambience.Waves);   // 村落地图：海浪
        }

        // ---------------- 海神号 ----------------

        GameObject _shipHotspot;
        GameObject _fishingHotspot;
        GameObject _houseHotspot;
        GameObject _carHotspot;

        /// <summary>根据「是否买了海神号」切换渔村背景与两个入口热区。</summary>
        public void ApplyShipState()
        {
            if (!SaveSystem.ShipPurchased)
            {
                if (_shipHotspot != null) _shipHotspot.SetActive(false);
                if (_fishingHotspot != null) _fishingHotspot.SetActive(false);
                if (_houseHotspot != null) _houseHotspot.SetActive(false);
                if (_carHotspot != null) _carHotspot.SetActive(false);
                return;
            }

            EnsureShipHotspots();

            if (villageObjects != null)
            {
                foreach (var o in villageObjects)
                {
                    if (o == null) continue;
                    if (o.name == "VillageBg")
                    {
                        var img = o.GetComponent<UnityEngine.UI.Image>();
                        if (img != null && villageBgWithShip != null) img.sprite = villageBgWithShip;
                    }
                    else if (o.name == "VillageHouse" || o.name == "VillagePier" || o.name == "VillageCar")
                    {
                        o.SetActive(false);   // 新背景里已经画好了民居/轿车/栈桥
                    }
                }
            }

            // 堂屋还开着（例如在堂屋里点了大黄 → 异能面板 → 关闭）时不要把入口放出来，
            // 否则它们会盖在堂屋的门 / 人物上面。
            // 同理：当前不在村落地图层（大地图 / 酒楼 / 住屋等）时也不能放出来，
            // 不然会冒在镇子上面挡住别的东西（主人 2026-11-15 反馈）。
            SetShipElements(!TangwuVisible() && VillageLayerVisible());
        }

        /// <summary>村落地图这一层现在是不是显示着的（大地图 / 酒楼 / 住屋等层会把它藏起来）。</summary>
        bool VillageLayerVisible()
        {
            if (villageObjects == null) return false;
            foreach (var o in villageObjects)
                if (o != null && o.name == "VillageBg") return o.activeSelf;
            return villageObjects.Length > 0 && villageObjects[0] != null && villageObjects[0].activeSelf;
        }

        /// <summary>堂屋（含遮罩）当前是否显示着——堂屋是叠在村落上的层，里面不该冒出渔船/轿车这些入口。</summary>
        bool TangwuVisible()
        {
            if (tangwuObjects == null) return false;
            foreach (var o in tangwuObjects)
                if (o != null && o.name == "TangwuMask") return o.activeSelf;
            foreach (var o in tangwuObjects)
                if (o != null) return o.activeSelf;
            return false;
        }

        void EnsureShipHotspots()
        {
            if (_shipHotspot != null) return;

            Transform stage = null;
            Transform villageBg = null;
            if (villageObjects != null)
                foreach (var o in villageObjects)
                    if (o != null && o.name == "VillageBg") { villageBg = o.transform; stage = villageBg.parent; break; }
            if (stage == null) stage = transform;

            // 先放码头（最大，垫底），再放船/民居/轿车，保证小元素在上面优先响应点击
            Sprite pierSprite = Resources.Load<Sprite>("Image/UI/主界面/element_pier");
            _fishingHotspot = MakeHotspot(stage, "PierHotspot",
                new Vector2(1027.5f, -660f), new Vector2(1009f, 460f), PlayGame, pierSprite);

            // 海神号 → 工厂
            Sprite shipSprite = Resources.Load<Sprite>("Image/UI/主界面/ship_shentian");
            _shipHotspot = MakeHotspot(stage, "ShipHotspot",
                new Vector2(1027.5f, -345f), new Vector2(565f, 240f),
                () => UnityEngine.SceneManagement.SceneManager.LoadScene("FactoryDemo"),
                shipSprite);

            // 民居 → 堂屋
            Sprite houseSprite = Resources.Load<Sprite>("Image/UI/主界面/element_house");
            _houseHotspot = MakeHotspot(stage, "HouseHotspot",
                new Vector2(117.5f, -455.5f), new Vector2(235f, 229f),
                OpenTangwu, houseSprite);

            // 白色轿车 → 大地图
            Sprite carSprite = Resources.Load<Sprite>("Image/UI/主界面/element_car");
            _carHotspot = MakeHotspot(stage, "CarHotspot",
                new Vector2(407.5f, -652.5f), new Vector2(143f, 83f),
                OpenBigMap, carSprite);

            _fishingHotspot.SetActive(false);
            _shipHotspot.SetActive(false);
            _houseHotspot.SetActive(false);
            _carHotspot.SetActive(false);

            // 层级：村落地图背景 < 四个入口 < 堂屋遮罩/屋内元素。
            // 不这样排的话，入口会冒到堂屋上面挡住木门和人物。
            if (villageBg != null)
            {
                int idx = villageBg.GetSiblingIndex() + 1;
                _fishingHotspot.transform.SetSiblingIndex(idx);
                _shipHotspot.transform.SetSiblingIndex(idx + 1);
                _houseHotspot.transform.SetSiblingIndex(idx + 2);
                _carHotspot.transform.SetSiblingIndex(idx + 3);
            }

            // 并进 villageObjects：切到大地图/酒楼等会隐藏村落的层时，这些元素跟着一起隐藏
            var list = new System.Collections.Generic.List<GameObject>();
            if (villageObjects != null) list.AddRange(villageObjects);
            list.Add(_fishingHotspot);
            list.Add(_shipHotspot);
            list.Add(_houseHotspot);
            list.Add(_carHotspot);
            villageObjects = list.ToArray();
        }

        /// <summary>商店 / 异能 / 堂屋这类「村落仍在底层」的层：把新元素单独藏起来。</summary>
        void SetShipElements(bool on)
        {
            if (!SaveSystem.ShipPurchased) return;
            if (_shipHotspot != null) _shipHotspot.SetActive(on);
            if (_fishingHotspot != null) _fishingHotspot.SetActive(on);
            if (_houseHotspot != null) _houseHotspot.SetActive(on);
            if (_carHotspot != null) _carHotspot.SetActive(on);
        }

        static GameObject MakeHotspot(Transform parent, string name, Vector2 pos, Vector2 size,
                                      System.Action onClick, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;
            img.preserveAspect = false;

            // 点击层按贴图透明度烘焙：透明像素不响应点击（贴图需 Read/Write + FullRect）
            if (sprite != null && sprite.texture != null && sprite.texture.isReadable)
                img.alphaHitTestMinimumThreshold = 0.12f;

            var btn = go.GetComponent<UnityEngine.UI.Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());

            // 有可见元素时：待机呼吸 + 悬停放大提亮（和渔村其它可点元素一致）
            // 注意不要再挂 UiPressScale —— 它每帧写 localScale，会把呼吸/悬停缩放顶掉
            if (sprite != null)
            {
                var hover = go.AddComponent<UIScaleOnHover>();
                hover.Configure(1.06f, true);
            }
            return go;
        }

        static void SetGroup(GameObject[] group, bool on)
        {
            if (group == null) return;
            foreach (var go in group)
                if (go != null) go.SetActive(on);
        }

        /// <summary>切换环境音（海浪 / 酒楼喧闹 / 开车 / 安静）。</summary>
        void SetAmb(GameAudio.Ambience a)
        {
            var ga = GameAudio.Ensure();
            if (ga != null) ga.SetAmbience(a);
        }
    }
}
