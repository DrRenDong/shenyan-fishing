using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 加色混合精灵材质的共享实例（光柱、焦散这类「黑底亮部」的素材用）。
    /// SpriteRenderer 会把 sprite 贴图塞进 _MainTex，颜色走顶点色，
    /// 所以这里只需要一份共享材质，逐个对象的 sr.color 控制淡入淡出。
    /// </summary>
    public static class AdditiveMat
    {
        public const string ShaderName = "Shenyan/AdditiveSprite";
        static Material _shared;

        public static Material Get()
        {
            if (_shared != null) return _shared;
            Shader s = Shader.Find(ShaderName);
            if (s == null) return null;
            _shared = new Material(s) { name = "AdditiveSprite(shared)", hideFlags = HideFlags.DontSave };
            return _shared;
        }

        /// <summary>把渲染器切到加色材质。</summary>
        public static void Apply(SpriteRenderer sr)
        {
            Material m = Get();
            if (m != null) sr.sharedMaterial = m;
        }
    }
}
