using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 新游戏开场漫画：两页，每页由 4 块拼成（2×2）。
    /// 点一下多揭示一块，四块都出来后就是完整一页；再点切到下一页；看完直接进游戏。
    /// 图在 Resources/Image/UI/开场漫画/p{页}_{块}。
    /// </summary>
    [DisallowMultipleComponent]
    public class IntroUI : MonoBehaviour
    {
        [Header("内容")]
        [SerializeField, Min(1)] int pageCount = 2;
        [SerializeField] string resourceFolder = "Image/UI/开场漫画/intro_";
        [SerializeField] string tutorialResource = "Image/UI/开场漫画/教程_操作";
        [SerializeField] string gameSceneName = "SampleScene";

        [Header("表现")]
        [SerializeField] float pageSize = 620f;      // 整页显示边长（参考分辨率 1280×720）
        [SerializeField] float fadeInTime = 0.22f;
        [SerializeField] Color backdrop = new Color(0.03f, 0.03f, 0.04f, 1f);

        const int PiecesPerPage = 4;

        Canvas _canvas;
        Image[] _pieces = new Image[PiecesPerPage];
        Image _tutorial;
        Text _hint;
        Sprite[][] _pages;
        Sprite _tutorialSprite;
        int _page;
        int _piece;
        bool _playing;
        bool _tutorialShown;
        bool _inTutorial;
        bool _clickPrev;

        public bool IsPlaying => _playing;

        void Awake()
        {
            _pages = new Sprite[pageCount][];
            for (int p = 0; p < pageCount; p++)
            {
                _pages[p] = new Sprite[PiecesPerPage];
                for (int i = 0; i < PiecesPerPage; i++)
                    _pages[p][i] = Resources.Load<Sprite>(resourceFolder + (p * PiecesPerPage + i + 1));
            }

            _tutorialSprite = Resources.Load<Sprite>(tutorialResource);

            Build();
            _canvas.gameObject.SetActive(false);
        }

        void Build()
        {
            var canvasGo = new GameObject("IntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            var brt = bg.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var bimg = bg.GetComponent<Image>();
            bimg.color = backdrop;
            bimg.raycastTarget = true;

            // 每块都是整页大小、居中叠放：素材自带「元素在原图里的位置」，
            // 透明区域互不遮挡，四块依次出现后自然拼成完整一页。
            for (int i = 0; i < PiecesPerPage; i++)
            {
                var go = new GameObject("Piece" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvasGo.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(pageSize, pageSize);

                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _pieces[i] = img;
                go.SetActive(false);
            }

            // 操作教程（全屏，漫画看完再出现）
            var tutGo = new GameObject("Tutorial", typeof(RectTransform), typeof(Image));
            tutGo.transform.SetParent(canvasGo.transform, false);
            var trt = tutGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _tutorial = tutGo.GetComponent<Image>();
            _tutorial.preserveAspect = true;
            _tutorial.raycastTarget = false;
            tutGo.SetActive(false);

            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
            var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(canvasGo.transform, false);
            var hrt = hintGo.GetComponent<RectTransform>();
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0f);
            hrt.pivot = new Vector2(0.5f, 0f);
            hrt.anchoredPosition = new Vector2(0f, 20f);
            hrt.sizeDelta = new Vector2(700f, 36f);

            _hint = hintGo.GetComponent<Text>();
            _hint.font = font;
            _hint.fontSize = 20;
            _hint.alignment = TextAnchor.MiddleCenter;
            _hint.color = new Color(1f, 0.95f, 0.85f, 0.8f);
            _hint.raycastTarget = false;

            var outline = hintGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        public void Play()
        {
            if (_pages == null || _pages.Length == 0) { SceneManager.LoadScene(gameSceneName); return; }

            _page = 0;
            _piece = 0;
            _playing = true;
            _tutorialShown = false;
            _inTutorial = false;
            if (_tutorial != null) _tutorial.gameObject.SetActive(false);

            _canvas.gameObject.SetActive(true);
            RefreshPieces();
        }

        /// <summary>按当前进度显示：本页第 0.._piece 块可见，其余隐藏。</summary>
        void RefreshPieces()
        {
            for (int i = 0; i < PiecesPerPage; i++)
            {
                var img = _pieces[i];
                if (img == null) continue;

                var sprite = _pages[_page][i];
                bool visible = i <= _piece && sprite != null;

                if (!visible)
                {
                    if (img.gameObject.activeSelf) img.gameObject.SetActive(false);
                    continue;
                }

                // 已经在场的块原样保留，不能重新淡入（否则整页会闪一下）
                if (img.gameObject.activeSelf) continue;

                img.sprite = sprite;
                img.gameObject.SetActive(true);
                StartCoroutine(FadeIn(img));
            }

            if (_hint != null)
            {
                int total = pageCount * PiecesPerPage;
                int shown = _page * PiecesPerPage + _piece + 1;
                _hint.text = (shown >= total)
                    ? "点击进入渔港"
                    : $"点击继续　{shown} / {total}";
            }
        }

        System.Collections.IEnumerator FadeIn(Image img)
        {
            var c = img.color;
            c.a = 0f;
            img.color = c;
            float t = 0f;
            while (t < 1f && img != null)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeInTime);
                c.a = Mathf.Clamp01(t);
                img.color = c;
                yield return null;
            }
        }

        void HideAllPieces()
        {
            foreach (var img in _pieces)
                if (img != null) img.gameObject.SetActive(false);
        }

        void Advance()
        {
            // 教程阶段：再点一次就进游戏
            if (_inTutorial)
            {
                Finish();
                return;
            }

            if (_piece < PiecesPerPage - 1)
            {
                _piece++;
                RefreshPieces();
                return;
            }

            if (_page < pageCount - 1)
            {
                _page++;
                _piece = 0;
                HideAllPieces();      // 换页先全部清掉，新的一页从第 1 块重新拼
                RefreshPieces();
                return;
            }

            // 两页都看完：先放操作教程，再点一次才进游戏
            if (!_tutorialShown && _tutorialSprite != null && _tutorial != null)
            {
                ShowTutorial();
                return;
            }

            Finish();
        }

        void ShowTutorial()
        {
            _inTutorial = true;
            _tutorialShown = true;

            HideAllPieces();
            _tutorial.sprite = _tutorialSprite;
            _tutorial.gameObject.SetActive(true);
            StartCoroutine(FadeIn(_tutorial));

            if (_hint != null) _hint.text = "点击开始捕捞";
        }

        void Finish()
        {
            _playing = false;
            _canvas.gameObject.SetActive(false);

            SaveSystem.IntroPlayed = true;
            SaveSystem.Save();

            Debug.Log("[开场] 剧情结束，进入游戏");
            SceneManager.LoadScene(gameSceneName);
        }

        void Update()
        {
            if (!_playing) return;

            bool clicked = Input.GetMouseButton(0) || Input.touchCount > 0 ||
                           Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return);
            if (clicked && !_clickPrev) Advance();
            _clickPrev = clicked;
        }
    }
}
