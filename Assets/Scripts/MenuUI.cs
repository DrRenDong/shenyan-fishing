using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 右上角菜单：设置 / 退出；在渔港场景里额外有「返回码头」「返回地图」。
    /// 设置里有音量、分辨率、陀螺仪开关、重置存档（二次确认，重置后退出游戏）。
    /// 全部 UI 运行时生成；坐标统一用「相对父物体中心」。
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuUI : MonoBehaviour
    {
        [Header("参考分辨率")]
        [SerializeField] Vector2 referenceResolution = new Vector2(1280f, 720f);

        static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(1600, 900),
            new Vector2Int(1280, 720),
            new Vector2Int(960, 540),
        };

        Canvas _canvas;
        GameObject _menuPanel, _settingsPanel;
        Font _font;
        Text _volumeText, _resText, _gyroText, _resetText;
        bool _resetArmed;

        void Awake()
        {
            Build();
        }

        bool InGame => SceneManager.GetActiveScene().name == "SampleScene";

        void Build()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);

            // 左下角菜单按钮：右上角被连击数占用，左上角是载重/金币 HUD
            var btn = NewImage(canvasGo.transform, "MenuButton");
            var brt = btn.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0f);
            brt.pivot = new Vector2(0f, 0f);
            brt.anchoredPosition = new Vector2(20f, 20f);
            brt.sizeDelta = new Vector2(96f, 96f);
            btn.color = new Color(0f, 0f, 0f, 0.45f);
            MakeLabel(btn.transform, "☰", 34, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            AddButton(btn.gameObject, ToggleMenu);

            // 遮罩层（全屏，点空白关闭）
            _menuPanel = NewOverlay(canvasGo.transform, "MenuPanel");
            AddButton(_menuPanel, () => SetMenu(false));

            // 设置面板（内容固定，只建一次）
            _settingsPanel = NewOverlay(canvasGo.transform, "SettingsPanel");
            BuildSettingsBox(_settingsPanel.transform);
            AddButton(_settingsPanel, () => SetSettings(false));

            _menuPanel.SetActive(false);
            _settingsPanel.SetActive(false);
        }

        GameObject NewOverlay(Transform parent, string name)
        {
            var go = NewImage(parent, name).gameObject;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            return go;
        }

        // ---------------- 菜单内容（每次打开重建，因为局内/主菜单项不同） ----------------

        void BuildMenuBox(Transform overlay)
        {
            var old = overlay.Find("Box");
            if (old != null) Destroy(old.gameObject);

            float height = InGame ? 620f : 440f;
            var box = NewImage(overlay, "Box");
            var rt = box.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(440f, height);
            box.color = new Color(0.10f, 0.075f, 0.06f, 0.98f);
            AddButton(box.gameObject, () => { });   // 挡住穿透

            float top = height * 0.5f - 60f;
            MakeLabel(box.transform, "菜　单", 32, new Color(0.98f, 0.93f, 0.80f),
                      TextAnchor.MiddleCenter, new Vector2(0f, top), new Vector2(380f, 50f));

            float y = top - 110f;
            MakeMenuButton(box.transform, "设　置", y, () => { SetMenu(false); SetSettings(true); });
            y -= 96f;

            if (InGame)
            {
                MakeMenuButton(box.transform, "返回码头", y, ReturnToDock);
                y -= 96f;
                MakeMenuButton(box.transform, "返回地图", y, ReturnToMap);
                y -= 96f;
            }

            MakeMenuButton(box.transform, "退出游戏", y, QuitGame);
        }

        void MakeMenuButton(Transform parent, string label, float y, System.Action action)
        {
            var go = NewImage(parent, "Btn_" + label);
            var rt = go.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(340f, 72f);
            go.color = new Color(0.24f, 0.15f, 0.10f, 0.95f);
            MakeLabel(go.transform, label, 26, new Color(0.98f, 0.93f, 0.80f), TextAnchor.MiddleCenter,
                      Vector2.zero, Vector2.zero);
            AddButton(go.gameObject, action);
        }

        // ---------------- 设置内容 ----------------

        void BuildSettingsBox(Transform overlay)
        {
            var box = NewImage(overlay, "Box");
            var rt = box.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(560f, 560f);
            box.color = new Color(0.10f, 0.075f, 0.06f, 0.98f);
            AddButton(box.gameObject, () => { });

            float top = 560f * 0.5f - 56f;
            MakeLabel(box.transform, "设　置", 32, new Color(0.98f, 0.93f, 0.80f),
                      TextAnchor.MiddleCenter, new Vector2(0f, top), new Vector2(480f, 50f));

            float y = top - 90f;

            // 音量
            MakeLabel(box.transform, "音量", 24, new Color(0.95f, 0.90f, 0.78f), TextAnchor.MiddleLeft,
                      new Vector2(-240f, y), new Vector2(100f, 40f), new Vector2(0f, 0.5f));
            _volumeText = MakeLabel(box.transform, "100%", 24, new Color(0.95f, 0.90f, 0.78f),
                                    TextAnchor.MiddleCenter, new Vector2(0f, y), new Vector2(120f, 40f));
            MakeSmallButton(box.transform, "-", new Vector2(120f, y), () => ChangeVolume(-0.1f));
            MakeSmallButton(box.transform, "+", new Vector2(190f, y), () => ChangeVolume(0.1f));
            y -= 78f;

            // 分辨率
            MakeLabel(box.transform, "分辨率", 24, new Color(0.95f, 0.90f, 0.78f), TextAnchor.MiddleLeft,
                      new Vector2(-240f, y), new Vector2(120f, 40f), new Vector2(0f, 0.5f));
            _resText = MakeLabel(box.transform, "", 22, new Color(0.95f, 0.90f, 0.78f),
                                 TextAnchor.MiddleCenter, new Vector2(20f, y), new Vector2(170f, 40f));
            MakeSmallButton(box.transform, "<", new Vector2(120f, y), () => ChangeResolution(-1));
            MakeSmallButton(box.transform, ">", new Vector2(190f, y), () => ChangeResolution(1));
            y -= 78f;

            // 陀螺仪
            _gyroText = MakeLabel(box.transform, "开启陀螺仪　关", 24, new Color(0.95f, 0.90f, 0.78f),
                                  TextAnchor.MiddleLeft, new Vector2(-240f, y), new Vector2(340f, 40f),
                                  new Vector2(0f, 0.5f));
            MakeSmallButton(box.transform, "切换", new Vector2(190f, y), ToggleGyro);
            y -= 86f;

            // 重置存档
            var resetGo = NewImage(box.transform, "ResetBtn");
            var rrt = resetGo.rectTransform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(0f, y);
            rrt.sizeDelta = new Vector2(340f, 64f);
            resetGo.color = new Color(0.30f, 0.12f, 0.10f, 0.95f);
            _resetText = MakeLabel(resetGo.transform, "重置存档", 24, new Color(0.98f, 0.90f, 0.80f),
                                   TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            AddButton(resetGo.gameObject, ResetSaveArmed);
            y -= 90f;

            MakeMenuButton(box.transform, "返　回", y, () => SetSettings(false));

            RefreshSettingsTexts();
        }

        void RefreshSettingsTexts()
        {
            if (_volumeText != null)
                _volumeText.text = (AudioListener.volume * 100f).ToString("0") + "%";
            if (_resText != null)
                _resText.text = Screen.width + "×" + Screen.height;
            if (_gyroText != null)
            {
                var mc = Object.FindObjectOfType<MobileControls>();
                bool on = mc != null && mc.GyroEnabled;
                _gyroText.text = "开启陀螺仪　" + (on ? "开" : "关");
            }
        }

        void ChangeVolume(float delta)
        {
            AudioListener.volume = Mathf.Clamp01(AudioListener.volume + delta);
            RefreshSettingsTexts();
        }

        void ChangeResolution(int dir)
        {
            int idx = 0;
            for (int i = 0; i < Resolutions.Length; i++)
                if (Resolutions[i].x == Screen.width && Resolutions[i].y == Screen.height) { idx = i; break; }

            idx = (idx + dir + Resolutions.Length) % Resolutions.Length;
            var r = Resolutions[idx];
            Screen.SetResolution(r.x, r.y, Screen.fullScreen);
            RefreshSettingsTexts();
        }

        void ToggleGyro()
        {
            var mc = Object.FindObjectOfType<MobileControls>();
            if (mc != null) mc.GyroEnabled = !mc.GyroEnabled;
            RefreshSettingsTexts();
        }

        void ResetSaveArmed()
        {
            if (!_resetArmed)
            {
                _resetArmed = true;
                if (_resetText != null) _resetText.text = "再点一次确认重置";
                CancelInvoke(nameof(DisarmReset));
                Invoke(nameof(DisarmReset), 3f);
                return;
            }

            if (System.IO.File.Exists(SaveSystem.SavePath))
                System.IO.File.Delete(SaveSystem.SavePath);

            Debug.Log("[菜单] 存档已重置，退出游戏");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        void DisarmReset()
        {
            _resetArmed = false;
            if (_resetText != null) _resetText.text = "重置存档";
        }

        void ToggleMenu()
        {
            bool willOpen = !_menuPanel.activeSelf;
            if (willOpen) BuildMenuBox(_menuPanel.transform);
            SetMenu(willOpen);
            if (willOpen) SetSettings(false);
        }

        void SetMenu(bool on)
        {
            _menuPanel.SetActive(on);
        }

        void SetSettings(bool on)
        {
            // 打开设置时一定要把菜单关掉，否则两层面板会叠在一起透字
            if (on) SetMenu(false);
            _settingsPanel.SetActive(on);
            if (on) RefreshSettingsTexts();
        }

        // ---------------- 行为 ----------------

        void ReturnToDock()
        {
            SetMenu(false);
            var status = Object.FindObjectOfType<BoatStatus>();
            if (status != null) status.ReturnToDock();
            Debug.Log("[菜单] 返回码头（全仓渔获已抛弃）");
        }

        void ReturnToMap()
        {
            SetMenu(false);
            SceneManager.LoadScene("MainMenu");
        }

        void QuitGame()
        {
            Debug.Log("[菜单] 退出游戏");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ---------------- UI 小工具 ----------------

        Image NewImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = true;
            return img;
        }

        void AddButton(GameObject go, System.Action action)
        {
            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => action());
        }

        Text MakeLabel(Transform parent, string text, int size, Color color, TextAnchor align,
                       Vector2 pos, Vector2 size2, Vector2? pivot = null)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();

            if (size2 == Vector2.zero)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = size2;
            }

            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        void MakeSmallButton(Transform parent, string label, Vector2 pos, System.Action action)
        {
            var go = NewImage(parent, "Small_" + label);
            var rt = go.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(60f, 44f);
            go.color = new Color(0.24f, 0.15f, 0.10f, 0.95f);
            MakeLabel(go.transform, label, 24, new Color(0.98f, 0.93f, 0.80f), TextAnchor.MiddleCenter,
                      Vector2.zero, Vector2.zero);
            AddButton(go.gameObject, action);
        }
    }
}
