using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 捞齐 36 条沉船（36 个国宝）后播放的授勋漫画：
    /// 点一下显示一片，7 片拼成完整的一页；播完写存档，不再重复播。
    /// </summary>
    public class TreasureComicUI : MonoBehaviour
    {
        public static TreasureComicUI Instance { get; private set; }

        [Header("素材")]
        public string folder = "Image/UI/授勋漫画";
        public int pieceCount = 7;
        [Tooltip("需要多少个国宝（= 沉船数量）才播放")]
        public int requiredTreasures = 36;

        [Header("布局（1920x1080 设计分辨率）")]
        public Vector2 pageSize = new Vector2(950f, 1040f);

        Canvas _canvas;
        GameObject _root;
        Image[] _pieces;
        Text _hint;
        float[] _alpha;
        int _shown;
        bool _playing;
        int _openedFrame = -1;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            TryPlayIfComplete();
        }

        /// <summary>国宝集齐且没播过就播一次。</summary>
        public void TryPlayIfComplete()
        {
            if (SaveSystem.NationalTreasureComicPlayed) return;
            if (PlayerProgress.TreasureCount(ItemDatabase.NationalTreasure) < requiredTreasures) return;
            Play();
        }

        public void Play()
        {
            if (_playing) return;

            EnsureUI();
            _root.SetActive(true);
            _playing = true;
            _shown = 0;
            _openedFrame = Time.frameCount;

            for (int i = 0; i < _pieces.Length; i++)
            {
                _pieces[i].gameObject.SetActive(false);
                _pieces[i].color = new Color(1f, 1f, 1f, 0f);
                _alpha[i] = 0f;
            }

            ShowNext();
            Debug.Log("[授勋漫画] 开始播放");
        }

        void Update()
        {
            if (!_playing) return;

            for (int i = 0; i < _pieces.Length; i++)
            {
                if (!_pieces[i].gameObject.activeSelf) continue;
                _alpha[i] = Mathf.MoveTowards(_alpha[i], 1f, Time.unscaledDeltaTime * 5f);
                Color c = _pieces[i].color;
                c.a = _alpha[i];
                _pieces[i].color = c;
            }

            if (Time.frameCount == _openedFrame) return;   // 打开这一帧的点击不算

            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                Advance();
        }

        void Advance()
        {
            if (_shown < _pieces.Length) ShowNext();
            else Finish();
        }

        void ShowNext()
        {
            _pieces[_shown].gameObject.SetActive(true);
            _alpha[_shown] = 0f;
            _shown++;
            UpdateHint();
        }

        void UpdateHint()
        {
            if (_hint == null) return;
            _hint.text = _shown < _pieces.Length
                ? $"点击继续　{_shown} / {_pieces.Length}"
                : "点击关闭";
        }

        void Finish()
        {
            _playing = false;
            if (_root != null) _root.SetActive(false);

            SaveSystem.NationalTreasureComicPlayed = true;
            SaveSystem.Save();
            Debug.Log("[授勋漫画] 播放完毕，已记录");
        }

        // ---------------- UI ----------------

        void EnsureUI()
        {
            if (_root != null) return;

            var canvasGo = new GameObject("TreasureComicCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 6400;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // 黑色底：挡住场景与下面所有 UI
            _root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            var rootRect = _root.GetComponent<RectTransform>();
            rootRect.SetParent(canvasGo.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 0.96f);

            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);

            int count = Mathf.Max(1, pieceCount);
            _pieces = new Image[count];
            _alpha = new float[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Piece" + (i + 1), typeof(RectTransform), typeof(Image));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(rootRect, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = pageSize;
                rect.anchoredPosition = Vector2.zero;

                var img = go.GetComponent<Image>();
                img.sprite = Resources.Load<Sprite>($"{folder}/comic_{i + 1}");
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 0f);
                go.SetActive(false);

                if (img.sprite == null)
                    Debug.LogWarning($"[授勋漫画] 缺少素材 {folder}/comic_{i + 1}");

                _pieces[i] = img;
            }

            var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.SetParent(rootRect, false);
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(600f, 40f);
            hintRect.anchoredPosition = new Vector2(0f, 18f);

            _hint = hintGo.GetComponent<Text>();
            _hint.font = font;
            _hint.fontSize = 22;
            _hint.alignment = TextAnchor.MiddleCenter;
            _hint.color = new Color(0.95f, 0.95f, 0.92f);
            _hint.raycastTarget = false;

            _root.SetActive(false);
        }
    }
}
