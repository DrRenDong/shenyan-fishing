using System;
using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 主界面对话框：姓名栏 + 正文 + 说话人立绘（+ 可选的选择按钮）。
    /// 普通对话点一下翻下一句，最后一句之后再点关闭；
    /// 带选项的对话会弹出按钮，点选后回调并关闭。
    /// 字体在运行时用系统字体生成，场景里不需要准备字体资源。
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] GameObject panel;
        [SerializeField] Text speakerText;
        [SerializeField] Text bodyText;

        [Header("立绘（按说话人切换）")]
        [SerializeField] Image qinLanPortrait;
        [SerializeField] Image daHuangPortrait;
        [SerializeField] Image wangXuePortrait;
        [SerializeField] Image wangBossPortrait;
        [SerializeField] Image zhangMaPortrait;

        [Header("选项按钮")]
        [SerializeField] GameObject choiceRoot;
        [SerializeField] Button[] choiceButtons;
        [SerializeField] Text[] choiceTexts;

        string[] _lines;
        int _index;
        bool _open;
        Action<int> _choiceCallback;

        public bool IsOpen => _open;
        public int Line => _index + 1;

        void Awake()
        {
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Arial" },
                speakerText != null ? speakerText.fontSize : 28);

            if (speakerText != null) speakerText.font = font;
            if (bodyText != null) bodyText.font = font;
            if (choiceTexts != null)
                foreach (var t in choiceTexts) if (t != null) t.font = font;
        }

        void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (choiceRoot != null) choiceRoot.SetActive(false);
        }

        /// <summary>弹出对话：speaker 填进姓名栏，lines 逐句显示，立绘按说话人切换。</summary>
        public void Show(string speaker, string[] lines)
        {
            if (lines == null || lines.Length == 0) return;

            _lines = lines;
            _index = 0;
            _open = true;
            _choiceCallback = null;

            if (panel != null) panel.SetActive(true);
            if (speakerText != null) speakerText.text = speaker;
            if (bodyText != null) bodyText.text = lines[0];
            if (choiceRoot != null) choiceRoot.SetActive(false);
            SetPortrait(speaker);
        }

        /// <summary>
        /// 弹出带选项的对话：body 一句话 + options 若干按钮。
        /// 点选项后回调 onPick(index) 并关闭对话框（功能由调用方决定）。
        /// </summary>
        public void ShowChoice(string speaker, string body, string[] options, Action<int> onPick)
        {
            if (options == null || options.Length == 0) return;

            _lines = null;          // 选项模式不翻页
            _index = 0;
            _open = true;
            _choiceCallback = onPick;

            if (panel != null) panel.SetActive(true);
            if (speakerText != null) speakerText.text = speaker;
            if (bodyText != null) bodyText.text = body;
            SetPortrait(speaker);

            if (choiceRoot != null) choiceRoot.SetActive(true);
            if (choiceButtons != null && choiceTexts != null)
            {
                for (int i = 0; i < choiceButtons.Length; i++)
                {
                    bool on = i < options.Length;
                    if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(on);
                    if (on && i < choiceTexts.Length && choiceTexts[i] != null)
                        choiceTexts[i].text = options[i];
                }
            }
        }

        /// <summary>选项按钮回调（编辑器里绑 0/1）。</summary>
        public void PickChoice(int index)
        {
            var cb = _choiceCallback;
            _choiceCallback = null;
            Close();
            cb?.Invoke(index);
        }

        /// <summary>点对话框：翻下一句，翻完关闭。</summary>
        public void Advance()
        {
            if (!_open || _lines == null) return;      // 选项模式不翻页

            _index++;
            if (_index >= _lines.Length)
            {
                Close();
                return;
            }
            if (bodyText != null) bodyText.text = _lines[_index];
        }

        public void Close()
        {
            _open = false;
            _lines = null;
            _choiceCallback = null;
            if (panel != null) panel.SetActive(false);
            if (choiceRoot != null) choiceRoot.SetActive(false);
        }

        /// <summary>只显示当前说话人的立绘。</summary>
        void SetPortrait(string speaker)
        {
            SetOne(qinLanPortrait, speaker == "秦兰");
            SetOne(daHuangPortrait, speaker == "大黄");
            SetOne(wangXuePortrait, speaker == "王雪");
            SetOne(wangBossPortrait, speaker == "王老板");
            SetOne(zhangMaPortrait, speaker == "张妈");
        }

        static void SetOne(Image img, bool on)
        {
            if (img != null) img.gameObject.SetActive(on);
        }
    }
}
