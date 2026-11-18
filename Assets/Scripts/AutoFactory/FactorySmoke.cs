using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 工厂炊烟：每隔几秒挑一座在画面里的工厂，从它屋顶冒出一团烟，
    /// 一边上升一边放大淡出。纯装饰，不影响玩法。
    /// 精灵来自 Assets/Resources/Image/特效/炊烟_1..4.png（烟雾消散 4 帧）。
    /// </summary>
    [DefaultExecutionOrder(530)]
    public class FactorySmoke : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("素材")]
        public string resourceFolder = "Image/特效";
        public string spritePrefix = "炊烟_";

        [Header("节奏")]
        public float minGap = 1.6f;
        public float maxGap = 3.4f;
        public int maxAlive = 8;

        [Header("外观")]
        public float startHeight = 0.95f;
        public float riseHeight = 1.15f;
        public float life = 2.8f;
        public float scale = 0.7f;
        public float growTo = 1.5f;

        Sprite[] _frames;
        readonly List<GameObject> _alive = new List<GameObject>();
        float _gap;

        void Start()
        {
            var all = Resources.LoadAll<Sprite>(resourceFolder);
            var list = new List<Sprite>();
            foreach (var s in all)
                if (s != null && s.name.StartsWith(spritePrefix)) list.Add(s);
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _frames = list.ToArray();
            _gap = Random.Range(0.8f, 2f);
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0) return;

            for (int i = _alive.Count - 1; i >= 0; i--)
                if (_alive[i] == null) _alive.RemoveAt(i);

            _gap -= Time.deltaTime;
            if (_gap > 0f) return;
            _gap = Random.Range(minGap, maxGap);
            if (_alive.Count >= maxAlive) return;

            SpawnOne();
        }

        void SpawnOne()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            var factories = FindObjectsOfType<FactoryBuilding>();
            if (factories == null || factories.Length == 0) return;

            // 只在画面里的工厂冒烟，屏幕外的没必要生成
            var visible = new List<FactoryBuilding>();
            for (int i = 0; i < factories.Length; i++)
            {
                var f = factories[i];
                if (f == null || !f.isActiveAndEnabled) continue;
                if (f.UnderConstruction) continue;
                Vector3 vp = cam.WorldToViewportPoint(f.transform.position);
                if (vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.02f && vp.y < 0.98f) visible.Add(f);
            }
            if (visible.Count == 0) return;

            FactoryBuilding target = visible[Random.Range(0, visible.Count)];
            SpriteRenderer tr = target.Renderer;
            int order = tr != null ? tr.sortingOrder + 2 : 500;

            Vector3 basePos = target.transform.position;
            var go = new GameObject("smoke");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(
                basePos.x + Random.Range(-0.12f, 0.12f),
                basePos.y + startHeight,
                -0.005f);
            go.transform.localScale = Vector3.one * scale * Random.Range(0.85f, 1.15f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _frames[0];
            sr.sortingOrder = order;
            sr.flipX = Random.value > 0.5f;
            sr.color = new Color(0.88f, 0.9f, 0.93f, 0.75f);

            var fx = go.AddComponent<SpriteFadeFx>();
            fx.sr = sr;
            fx.frames = _frames;
            fx.fps = 7f;
            fx.life = life;
            fx.startAlpha = 0.75f;
            fx.drift = new Vector3(0.12f, riseHeight / Mathf.Max(0.1f, life), 0f);
            fx.growTo = growTo;

            _alive.Add(go);
        }
    }
}
