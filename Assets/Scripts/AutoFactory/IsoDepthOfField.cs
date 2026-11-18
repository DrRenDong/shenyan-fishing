using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 镜头拉近时的伪景深。
    ///
    /// 场景里的精灵全部走 Sprites/Default（不写深度），真正的 Depth of Field 拿不到深度图，
    /// 所以这里用屏幕纵向的移轴模糊：画面中间一条带保持清晰，往上/往下逐渐模糊。
    /// 强度跟着 MapCameraController 的正交尺寸走——越拉近模糊越明显，拉远时完全关闭。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class IsoDepthOfField : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("触发区间（正交尺寸，越小=越拉近）")]
        [Tooltip("尺寸大于等于这个值时完全不模糊（默认视野）")]
        public float zoomOutSize = 7.2f;
        [Tooltip("尺寸小于等于这个值时模糊最强")]
        public float zoomInSize = 3.2f;

        [Header("模糊")]
        [Tooltip("1080p 下的最大模糊半径（像素），会按屏幕高度等比缩放")]
        public float maxRadius = 6f;
        [Range(0f, 1f)] public float focusCenter = 0.5f;
        [Tooltip("清晰带半高（屏幕高度比例）")]
        [Range(0.02f, 0.6f)] public float sharpBand = 0.26f;
        [Tooltip("往上（远处）从清晰过渡到全模糊的宽度")]
        [Range(0.02f, 0.8f)] public float falloffUp = 0.26f;
        [Tooltip("往下（近处）从清晰过渡到全模糊的宽度，一般比上方宽＝近处更清晰")]
        [Range(0.02f, 0.8f)] public float falloffDown = 0.60f;
        [Tooltip("最大叠加强度")]
        [Range(0f, 1f)] public float maxAmount = 1f;

        [Header("资源")]
        public Shader shader;

        public const string ShaderName = "Hidden/Shenyan/IsoDepthOfField";

        Material _material;
        float _amount;

        /// <summary>当前实际叠加强度（0 = 关，1 = 最强），调试/验证用。</summary>
        public float Amount => _amount;
        public float CurrentRadius => maxRadius * Mathf.Max(0.01f, Screen.height / 1080f) * _amount;

        void OnEnable()
        {
            EnsureMaterial();
        }

        void OnDisable()
        {
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
                _material = null;
            }
        }

        void EnsureMaterial()
        {
            if (_material != null) return;
            Shader s = shader != null ? shader : Shader.Find(ShaderName);
            if (s == null || !s.isSupported) return;
            _material = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        }

        Camera Cam
        {
            get
            {
                if (targetCamera == null) targetCamera = GetComponent<Camera>();
                return targetCamera;
            }
        }

        void Update()
        {
            float target = 0f;
            Camera cam = Cam;
            if (cam != null && cam.orthographic)
            {
                // zoomInSize → 1，zoomOutSize → 0
                float t = Mathf.InverseLerp(zoomOutSize, zoomInSize, cam.orthographicSize);
                target = Mathf.Clamp01(t) * Mathf.Clamp01(maxAmount);
            }

            // 缩放是连续的，这里做一点平滑，免得滚轮一格一格地跳
            float k = 1f - Mathf.Exp(-10f * Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            _amount = Mathf.Lerp(_amount, target, k);
            if (Mathf.Abs(_amount - target) < 0.002f) _amount = target;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_material == null) EnsureMaterial();

            if (_material == null || _amount <= 0.002f)
            {
                Graphics.Blit(source, destination);
                return;
            }

            float radius = maxRadius * Mathf.Max(0.01f, Screen.height / 1080f) * _amount;

            var tmp1 = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
            var tmp2 = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);

            _material.SetVector("_Dir", new Vector4(1f, 0f, 0f, 0f));
            _material.SetFloat("_Radius", radius);
            Graphics.Blit(source, tmp1, _material, 0);

            _material.SetVector("_Dir", new Vector4(0f, 1f, 0f, 0f));
            Graphics.Blit(tmp1, tmp2, _material, 0);

            _material.SetTexture("_BlurTex", tmp2);
            _material.SetFloat("_Amount", _amount);
            _material.SetFloat("_Focus", focusCenter);
            _material.SetFloat("_Band", sharpBand);
            _material.SetFloat("_FalloffUp", falloffUp);
            _material.SetFloat("_FalloffDown", falloffDown);
            Graphics.Blit(source, destination, _material, 1);

            RenderTexture.ReleaseTemporary(tmp1);
            RenderTexture.ReleaseTemporary(tmp2);
        }
    }
}
