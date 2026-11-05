using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    public enum TimeOfDay { Day, Dusk, Night }

    /// <summary>
    /// 海平面纯色蒙版 + 时段背景图。两者都跟着相机走：
    /// 水墙始终铺满视野，背景按自身宽度首尾相接地循环平铺。
    /// </summary>
    [DisallowMultipleComponent]
    public class SeaWaterLayer : MonoBehaviour
    {
        [Header("水位")]
        [SerializeField] float seaLevelY = 16f;      // 甲板底高度
        [SerializeField] float depth = 80f;          // 基准长度（视野更深时会自动拉长）
        [SerializeField] float waterBottomMargin = 6f; // 水墙底部在视野下方多留一截
        [SerializeField] float extraWidth = 40f;     // 视野之外多铺一点

        [Header("时段")]
        [SerializeField] TimeOfDay timeOfDay = TimeOfDay.Day;

        [Header("海水颜色（半透明，水下的鱼和地形仍要看得见）")]
        [SerializeField] Color waterDay = new Color(0.353f, 0.667f, 0.824f, 0.45f);
        [SerializeField] Color waterDusk = new Color(0.784f, 0.522f, 0.435f, 0.45f);
        [SerializeField] Color waterNight = new Color(0.106f, 0.196f, 0.361f, 0.55f);

        [Header("背景图（底边贴住海平面，按宽度循环）")]
        [SerializeField] Sprite backdropDay;
        [SerializeField] Sprite backdropDusk;
        [SerializeField] Sprite backdropNight;
        [SerializeField, Min(1)] int backdropCopies = 3;

        [Header("排序（都比码头小，压在最里面）")]
        [SerializeField] string sortingLayer = "Default";
        [SerializeField] int waterSortingOrder = 3;
        [SerializeField] int backdropSortingOrder = -20;

        SpriteRenderer _water;
        readonly List<SpriteRenderer> _backdrops = new List<SpriteRenderer>();
        Camera _camera;

        void Awake()
        {
            _camera = Camera.main;
            LoadBackdropsIfMissing();
            Build();
            Apply(timeOfDay);
        }

        void LateUpdate()
        {
            if (_camera == null) return;
            FollowCamera();
        }

        [ContextMenu("切到白天")]
        public void SetDay() => Apply(TimeOfDay.Day);

        [ContextMenu("切到黄昏")]
        public void SetDusk() => Apply(TimeOfDay.Dusk);

        [ContextMenu("切到夜晚")]
        public void SetNight() => Apply(TimeOfDay.Night);

        public void Apply(TimeOfDay time)
        {
            timeOfDay = time;

            if (_water != null)
            {
                _water.color = time switch
                {
                    TimeOfDay.Dusk => waterDusk,
                    TimeOfDay.Night => waterNight,
                    _ => waterDay
                };
            }

            var sprite = time switch
            {
                TimeOfDay.Dusk => backdropDusk,
                TimeOfDay.Night => backdropNight,
                _ => backdropDay
            };
            foreach (var b in _backdrops) b.sprite = sprite;
            FollowCamera();
        }

        void Build()
        {
            if (_water == null)
            {
                var go = new GameObject("SeaWaterMask");
                go.transform.SetParent(transform, false);
                _water = go.AddComponent<SpriteRenderer>();
                _water.sprite = MakeUnitSprite();
                _water.sortingLayerName = sortingLayer;
                _water.sortingOrder = waterSortingOrder;
            }

            while (_backdrops.Count < backdropCopies)
            {
                var go = new GameObject("Backdrop_" + _backdrops.Count);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = backdropSortingOrder;
                _backdrops.Add(sr);
            }
        }

        /// <summary>
        /// 水墙跟着相机铺满视野；背景按自身宽度做无缝循环。
        /// 水墙顶部固定在海平面，底部按视野动态延伸：相机往下潜超过原来的长度上限时
        /// 水墙跟着拉长（不会露底），缩回来又恢复到基准 depth。
        /// </summary>
        void FollowCamera()
        {
            if (_camera == null) return;
            float camX = _camera.transform.position.x;
            float halfWidth = _camera.orthographicSize * _camera.aspect;

            if (_water != null)
            {
                float w = halfWidth * 2f + extraWidth;

                float viewBottom = _camera.transform.position.y - _camera.orthographicSize;
                float needed = seaLevelY - viewBottom + waterBottomMargin;
                float d = Mathf.Max(depth, needed);

                _water.transform.localScale = new Vector3(w, d, 1f);
                _water.transform.position = new Vector3(camX, seaLevelY - d * 0.5f, 0f);
            }

            var sprite = _backdrops.Count > 0 ? _backdrops[0].sprite : null;
            if (sprite == null) return;

            float spriteW = sprite.rect.width / sprite.pixelsPerUnit;
            float spriteH = sprite.rect.height / sprite.pixelsPerUnit;
            // 从视野左边界再往左一整幅开始铺，保证左右两侧都不会露空
            float baseX = Mathf.Floor((camX - halfWidth) / spriteW) * spriteW - spriteW;

            for (int i = 0; i < _backdrops.Count; i++)
            {
                // 相邻副本水平镜像交替：图右边缘永远贴着自己的右边缘，接缝天然对齐
                _backdrops[i].flipX = (i % 2) == 1;
                _backdrops[i].transform.position =
                    new Vector3(baseX + spriteW * (i + 0.5f), seaLevelY + spriteH * 0.5f, 0f);
            }
        }

        static Sprite MakeUnitSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "WaterMaskUnit";
            return sprite;
        }

        void LoadBackdropsIfMissing()
        {
            if (backdropDay == null) backdropDay = Resources.Load<Sprite>("Image/背景/背景_白天");
            if (backdropDusk == null) backdropDusk = Resources.Load<Sprite>("Image/背景/背景_黄昏");
            if (backdropNight == null) backdropNight = Resources.Load<Sprite>("Image/背景/背景_夜晚");
        }
    }
}
