using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 全局音频调度：海浪/马达两条常驻循环 + 一组一次性音效声道。
    /// 场景里没有就自动创建，其它脚本直接 GameAudio.Ensure() 调用。
    /// </summary>
    [DisallowMultipleComponent]
    public class GameAudio : MonoBehaviour
    {
        [Header("循环音源")]
        [SerializeField] AudioClip wavesClip;
        [SerializeField] AudioClip engineClip;
        [SerializeField, Range(0f, 1f)] float wavesVolume = 0.25f;
        [SerializeField, Range(0f, 1f)] float engineVolume = 0.45f;
        [SerializeField] float engineFadeSpeed = 4f;

        [Header("环境音（地图/场景切换）")]
        [SerializeField] AudioClip restaurantClip;   // 已弃用（主人不要酒楼声）
        [SerializeField] AudioClip trafficClip;      // 城市车流（备用）
        [SerializeField] AudioClip driveClip;        // 已弃用（改成一次性点火声）
        [SerializeField, Range(0f, 1f)] float ambienceVolume = 0.35f;

        [Header("一次性（地图）")]
        [SerializeField] AudioClip ignitionClip;     // 进大地图的点火发动声
        [SerializeField] AudioClip[] uiClickClips;   // 商店 / 异能面板点击

        [Header("一次性音效")]
        [SerializeField] AudioClip[] splashClips;   // 钩子入水
        [SerializeField] AudioClip[] comboClips;    // 连击：鱼拍在甲板上（每次滚一条消息响一次）
        [SerializeField] AudioClip endRunClip;      // 本局结束结算
        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.8f;

        [Header("连击音调")]
        [SerializeField, Min(1)] int comboPitchMax = 10;          // 连击到这个数音调封顶
        [SerializeField] float comboPitchStep = 1f;               // 每级升几个半音

        const int SfxVoices = 8;

        /// <summary>当前场景该放哪段环境音。</summary>
        public enum Ambience { None, Waves, Restaurant, Traffic, Drive }

        public static GameAudio Instance { get; private set; }

        AudioSource _waves;
        AudioSource _engine;
        AudioSource _ambience;
        AudioSource[] _voices;
        int _voice;
        float _engineTarget;
        float _ambienceTarget;
        Ambience _currentAmbience = Ambience.Waves;

        public static GameAudio Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindObjectOfType<GameAudio>();
            if (existing != null) return existing;
            var go = new GameObject("GameAudio");
            return go.AddComponent<GameAudio>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _waves = CreateSource("Waves", true);
            _engine = CreateSource("Engine", true);
            _ambience = CreateSource("Ambience", true);
            _voices = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++) _voices[i] = CreateSource("Sfx" + i, false);

            // 默认海浪（主菜单/游戏场景都是海边，进入后由各界面按需切换）
            SetAmbience(Ambience.Waves);
            if (engineClip != null)
            {
                // 马达常驻播放、音量从 0 淡入：比反复 Play/Stop 省事，也不会有起播爆音
                _engine.clip = engineClip;
                _engine.volume = 0f;
                _engine.Play();
            }
        }

        AudioSource CreateSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            return src;
        }

        void Update()
        {
            if (_engine == null) return;
            _engine.volume = Mathf.MoveTowards(_engine.volume, _engineTarget,
                                               engineFadeSpeed * Time.unscaledDeltaTime);

            if (_ambience != null)
                _ambience.volume = Mathf.MoveTowards(_ambience.volume, _ambienceTarget,
                                                     engineFadeSpeed * Time.unscaledDeltaTime);
        }

        /// <summary>切换环境音（村落地图=海浪、酒楼=喧闹、大地图=开车、住屋=安静）。</summary>
        public void SetAmbience(Ambience a)
        {
            if (_ambience == null) return;
            if (a == _currentAmbience && _ambience.isPlaying) return;

            _currentAmbience = a;
            var clip = ClipOf(a);

            if (clip == null)
            {
                _ambienceTarget = 0f;      // 渐隐，不硬切
                return;
            }

            _ambience.clip = clip;
            _ambience.volume = 0f;
            _ambience.Play();
            _ambienceTarget = a == Ambience.Waves ? wavesVolume : ambienceVolume;
        }

        AudioClip ClipOf(Ambience a)
        {
            switch (a)
            {
                case Ambience.Waves: return wavesClip;
                // 酒楼/开车都不再用循环环境音（酒楼声已弃用，进大地图改成一次性点火声）
                default: return null;
            }
        }

        /// <summary>进大地图：点火发动，只响一次。</summary>
        public void PlayIgnition(float volumeScale = 0.3f) => Play(ignitionClip, volumeScale, 0f);

        /// <summary>商店 / 异能面板的点击反馈。</summary>
        public void PlayUiClick(float volumeScale = 0.35f) => PlayRandom(comboClips, volumeScale, 0.02f);

        /// <summary>马达开关：船在烧油且有横移输入时打开。</summary>
        public void SetEngine(bool on, float volumeScale = 1f)
        {
            _engineTarget = on ? engineVolume * Mathf.Clamp01(volumeScale) : 0f;
        }

        public void PlaySplash(float volumeScale = 1f) => PlayRandom(splashClips, volumeScale, 0.08f);

        /// <summary>本局结束（回港结算）的一声。</summary>
        public void PlayEndRun(float volumeScale = 1f) => Play(endRunClip, volumeScale, 0f);

        /// <summary>
        /// 连击音：每滚一条渔获消息响一次，连击越高音调越高（每级一个半音），到 comboPitchMax 封顶。
        /// </summary>
        public void PlayCombo(int combo, float volumeScale = 1f)
        {
            if (comboClips == null || comboClips.Length == 0) return;

            int level = Mathf.Clamp(combo, 1, comboPitchMax);
            float pitch = Mathf.Pow(2f, (level - 1) * comboPitchStep / 12f);

            var clip = comboClips[Random.Range(0, comboClips.Length)];
            if (clip == null) return;
            // 不加音调抖动：相邻两级只差一个半音，抖了会听不出高低
            Play(clip, volumeScale, 0f, pitch);
        }

        void PlayRandom(AudioClip[] clips, float volumeScale, float pitchJitter)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;
            Play(clip, volumeScale, pitchJitter);
        }

        public void Play(AudioClip clip, float volumeScale = 1f, float pitchJitter = 0f, float pitch = 1f)
        {
            if (clip == null || _voices == null || _voices.Length == 0) return;
            var src = _voices[_voice];
            _voice = (_voice + 1) % _voices.Length;
            src.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, sfxVolume * Mathf.Clamp01(volumeScale));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
