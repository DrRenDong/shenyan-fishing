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
            SetAmb(GameAudio.Ambience.Waves);   // 堂屋叠在村落地图上，沿用海浪
            Debug.Log("[主菜单] 进入堂屋");
        }

        /// <summary>堂屋里点木门：收起堂屋层，回到村落地图。</summary>
        public void CloseTangwu()
        {
            SetGroup(tangwuObjects, false);
            if (dialogue != null) dialogue.Close();
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
            Debug.Log("[主菜单] 打开天赋面板");
        }

        public void CloseTalent()
        {
            SetGroup(talentObjects, false);
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
            Debug.Log("[主菜单] 打开渔具商店");
        }

        public void CloseShop()
        {
            SetGroup(shopObjects, false);
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
            SetAmb(GameAudio.Ambience.Waves);   // 村落地图：海浪
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
