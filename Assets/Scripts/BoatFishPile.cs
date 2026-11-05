using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 船上的鱼堆：按船上渔获条数切换 6 档贴图（Assets/Resources/Image/角色/鱼/1..6.png），
    /// 鱼越多堆得越高。位置/缩放是主人手调好的数值，直接写在这张表里。
    /// 驱动的是船下面已有的 fish 节点（order 比船低，船帮会挡住鱼堆底部，看起来像装在船舱里）。
    /// </summary>
    [DisallowMultipleComponent]
    public class BoatFishPile : MonoBehaviour
    {
        [Header("数据")]
        [SerializeField] BoatStatus status;

        [Header("分级（船上鱼数 >= 该值就用这一档）")]
        [SerializeField] int[] levelCounts = { 1, 4, 10, 20, 35, 55 };

        [Header("鱼堆节点（留空自动找 PlayerBoat/fish，找不到就新建）")]
        [SerializeField] Transform pileNode;

        [Header("资源")]
        [SerializeField] string spriteFolder = "Image/角色/鱼/";       // 1.png ~ 6.png
        [SerializeField] string fishSpriteFolder = "Image/鱼_像素/";    // 单条大鱼用它的像素图

        [Header("新建节点时用的渲染设置（已有节点则不改）")]
        [SerializeField] string sortingLayer = "Default";
        [SerializeField] int sortingOrder = 1;      // 比船(2)低：船帮挡住底部
        [SerializeField] bool mirrorWithBoat = true; // 船朝左时鱼堆跟着镜像，保持和甲板对齐

        [Header("单条大鱼（空船一竿满仓时直接显示那条鱼）")]
        [SerializeField, Range(0.2f, 1.5f)] float singleWidthRatio = 0.95f;   // 最宽 = 船长 × 这个
        [SerializeField, Range(0.5f, 4f)] float singleHeightRatio = 2.2f;     // 最高 = 船高 × 这个
        [SerializeField, Range(-1f, 1.2f)] float singleBottomY = 0f;          // 鱼底边的高度（0=船中线，-1=船底）

        [Header("每档的局部位置 / 缩放（主人 2026-11-05 实测数值）")]
        [SerializeField] Vector3[] localPositions =
        {
            new Vector3(0f, -0.145f, 0f),
            new Vector3(0f, -0.08f, 0f),
            new Vector3(0f, 0.0273f, 0f),
            new Vector3(0f, 0.49f, 0f),
            new Vector3(0f, 0.85f, 0f),
            new Vector3(-0.27f, 1.0774f, 0f),
        };

        [SerializeField] Vector3[] localScales =
        {
            new Vector3(0.179367557f, 0.179367557f, 0.179367557f),
            new Vector3(0.179367557f, 0.179367557f, 0.179367557f),
            new Vector3(0.200317681f, 0.200317681f, 0.200317681f),
            new Vector3(0.317721456f, 0.317721456f, 0.317721456f),
            new Vector3(0.390930712f, 0.390930712f, 0.390930712f),
            new Vector3(0.441005141f, 0.441005141f, 0.441005141f),
        };

        SpriteRenderer _renderer;
        PlayerBoat _boat;
        Sprite[] _sprites;
        readonly System.Collections.Generic.Dictionary<string, Sprite> _fishCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();
        int _level = -1;
        bool _mirrored;
        bool _unloading;
        string _singleName;

        void Awake()
        {
            if (status == null) status = GetComponent<BoatStatus>();
            if (status == null) status = GetComponentInParent<BoatStatus>();
            _boat = GetComponent<PlayerBoat>();
            if (_boat == null) _boat = GetComponentInParent<PlayerBoat>();

            LoadSprites();
            ResolveNode();
        }

        void LoadSprites()
        {
            _sprites = new Sprite[6];
            for (int i = 0; i < _sprites.Length; i++)
            {
                _sprites[i] = Resources.Load<Sprite>(spriteFolder + (i + 1));
                if (_sprites[i] == null) Debug.LogWarning("[BoatFishPile] 找不到鱼堆贴图：" + spriteFolder + (i + 1));
            }
        }

        void ResolveNode()
        {
            if (pileNode == null) pileNode = transform.Find("fish");

            if (pileNode == null)
            {
                var go = new GameObject("fish");
                go.transform.SetParent(transform, false);
                pileNode = go.transform;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
            }

            _renderer = pileNode.GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = pileNode.gameObject.AddComponent<SpriteRenderer>();

            pileNode.gameObject.SetActive(false);   // 空船先藏起来，等有鱼再显示
            _level = -1;
        }

        void Update()
        {
            if (status == null || pileNode == null || _unloading) return;

            // 空船一竿就满仓（一条大鱼直接把船压满）：不显示鱼堆，直接显示那条鱼
            string single = status.ShowSingleCatch ? status.SingleCatchName : null;
            int level = single != null ? 0 : LevelFor(status.CargoCount);
            bool mirrored = mirrorWithBoat && _boat != null && _boat.Renderer != null && _boat.Renderer.flipX;
            if (level == _level && mirrored == _mirrored && single == _singleName) return;

            if (single != null && ApplySingle(single, mirrored)) return;

            Apply(level, mirrored);
        }

        /// <summary>
        /// 单条大鱼模式：把这条鱼的像素贴图贴在原来鱼堆的位置。
        /// 不用手摆——按船的大小自动缩放，底边压在船底（略高 0.05），绝不超出船底。
        /// </summary>
        bool ApplySingle(string fishName, bool mirrored)
        {
            var sprite = LoadFishSprite(fishName);
            if (sprite == null) return false;

            float w = sprite.rect.width / sprite.pixelsPerUnit;
            float h = sprite.rect.height / sprite.pixelsPerUnit;
            if (w <= 0.001f || h <= 0.001f) return false;

            float boatW = 8f, boatH = 2f, boatBottom = transform.position.y - 1f;
            if (_boat != null && _boat.Renderer != null && _boat.Renderer.sprite != null)
            {
                var bb = _boat.Renderer.bounds;
                boatW = bb.size.x;
                boatH = bb.size.y;
                boatBottom = bb.min.y;
            }

            float scale = Mathf.Min(1f, Mathf.Min(boatW * singleWidthRatio / w, boatH * singleHeightRatio / h));

            // 底边压在船的腰线上（和鱼堆"装在舱里"的感觉一致），
            // 兜底：无论如何不会低于船底
            float bottomLocal = singleBottomY;
            float boatBottomLocal = boatBottom - transform.position.y;
            if (bottomLocal < boatBottomLocal + 0.05f) bottomLocal = boatBottomLocal + 0.05f;
            float y = bottomLocal + h * scale * 0.5f;

            float sx = mirrored ? -1f : 1f;
            pileNode.localPosition = new Vector3(0f, y, 0f);
            pileNode.localScale = new Vector3(scale * sx, scale, scale);

            _renderer.sprite = sprite;
            _renderer.flipX = false;
            pileNode.gameObject.SetActive(true);

            _level = 0;
            _mirrored = mirrored;
            _singleName = fishName;
            return true;
        }

        Sprite LoadFishSprite(string fishName)
        {
            if (string.IsNullOrEmpty(fishName)) return null;
            if (_fishCache.TryGetValue(fishName, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(fishSpriteFolder + fishName);
            if (sprite == null) Debug.LogWarning("[BoatFishPile] 找不到鱼的像素图：" + fishSpriteFolder + fishName);
            _fishCache[fishName] = sprite;
            return sprite;
        }

        /// <summary>
        /// 码头卸货：把当前这堆鱼复制一份，平移缓动飞到冷链车上，然后回调。
        /// 船上的鱼堆先隐藏，动画期间 Update 不再驱动它。
        /// </summary>
        public void PlayUnload(Vector3 target, float duration, System.Action onDone)
        {
            if (pileNode == null || _renderer == null || _renderer.sprite == null || !pileNode.gameObject.activeSelf)
            {
                if (onDone != null) onDone();
                return;
            }

            var go = new GameObject("UnloadingPile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _renderer.sprite;
            sr.sortingLayerName = _renderer.sortingLayerName;
            sr.sortingOrder = _renderer.sortingOrder + 6;    // 飞的时候盖过船和海水
            go.transform.position = pileNode.position;
            go.transform.localScale = pileNode.localScale;

            pileNode.gameObject.SetActive(false);
            _unloading = true;
            StartCoroutine(UnloadRoutine(go.transform, sr, target, Mathf.Max(0.05f, duration), onDone));
        }

        System.Collections.IEnumerator UnloadRoutine(Transform copy, SpriteRenderer sr,
                                                      Vector3 target, float duration, System.Action onDone)
        {
            Vector3 start = copy.position;
            Vector3 startScale = copy.localScale;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = k * k * (3f - 2f * k);              // smoothstep，起步和落点都缓
                copy.position = Vector3.Lerp(start, target, e);
                copy.localScale = Vector3.Lerp(startScale, startScale * 0.72f, e);
                var c = sr.color;
                c.a = 1f - Mathf.Clamp01((k - 0.72f) / 0.28f); // 最后一段淡出
                sr.color = c;
                yield return null;
            }

            Destroy(copy.gameObject);
            if (onDone != null) onDone();
            _unloading = false;
        }

        /// <summary>船上鱼数 → 1~6 档，0 = 不显示。</summary>
        int LevelFor(int count)
        {
            if (count <= 0) return 0;
            if (levelCounts == null || levelCounts.Length == 0) return Mathf.Clamp(count, 1, 6);

            int level = 0;
            int max = Mathf.Min(levelCounts.Length, 6);
            for (int i = 0; i < max; i++)
                if (count >= levelCounts[i]) level = i + 1;
            return level;
        }

        void Apply(int level, bool mirrored)
        {
            _level = level;
            _mirrored = mirrored;
            _singleName = null;

            if (level <= 0 || _sprites == null)
            {
                pileNode.gameObject.SetActive(false);
                return;
            }

            int i = Mathf.Clamp(level - 1, 0, 5);
            var sprite = _sprites[i];
            if (sprite == null) { pileNode.gameObject.SetActive(false); return; }

            var pos = localPositions != null && i < localPositions.Length ? localPositions[i] : Vector3.zero;
            var scale = localScales != null && i < localScales.Length ? localScales[i] : Vector3.one;

            float sx = mirrored ? -1f : 1f;
            pileNode.localPosition = new Vector3(pos.x * sx, pos.y, pos.z);
            pileNode.localScale = new Vector3(scale.x * sx, scale.y, scale.z);

            _renderer.sprite = sprite;
            _renderer.flipX = false;   // 镜像靠 scale.x 取反，贴图本身不翻
            pileNode.gameObject.SetActive(true);
        }

        /// <summary>调试用：当前显示的档位（0 = 空）。</summary>
        public int CurrentLevel => _level;
    }
}
