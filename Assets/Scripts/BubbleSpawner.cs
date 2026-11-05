using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 泡泡氛围：在水里随机位置的泡泡缓缓上浮，左右轻微摆动，
    /// 到水面就回到底下重新飘。两种大小随机混用。
    /// </summary>
    [DisallowMultipleComponent]
    public class BubbleSpawner : MonoBehaviour
    {
        [Header("泡泡")]
        [SerializeField] Sprite bubbleBig;      // 16x16
        [SerializeField] Sprite bubbleSmall;    // 8x8
        [SerializeField, Min(1)] int count = 24;
        [SerializeField] Vector2 speedRange = new Vector2(0.4f, 1.2f);
        [SerializeField] Vector2 swayRange = new Vector2(0.15f, 0.45f);

        [Header("范围")]
        [SerializeField] float surfaceY = 16f;
        [SerializeField] float depthY = -16f;
        [SerializeField] float sideMargin = 3f;

        [Header("外观")]
        [SerializeField] string sortingLayer = "Default";
        [SerializeField] int sortingOrder = 5;        // 海水(4)之上，否则会被水色压得太淡看不见
        [SerializeField] Color bubbleColor = new Color(1f, 1f, 1f, 0.9f);

        [Header("参考")]
        [SerializeField] SandbarTerrain terrain;

        class Bubble
        {
            public Transform node;
            public SpriteRenderer renderer;
            public float speed;
            public float swayAmp;
            public float swayPhase;
            public float baseX;
            public float bornY;
        }

        readonly List<Bubble> _bubbles = new List<Bubble>();
        Camera _camera;

        void Start()
        {
            _camera = Camera.main;
            if (bubbleBig == null) bubbleBig = Resources.Load<Sprite>("Image/道具/泡泡16bit");
            if (bubbleSmall == null) bubbleSmall = Resources.Load<Sprite>("Image/道具/泡泡8bit");
            Build();
        }

        void Build()
        {
            var parent = new GameObject("Bubbles").transform;
            parent.SetParent(transform, false);

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Bubble" + i);
                go.transform.SetParent(parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
                sr.color = bubbleColor;

                var b = new Bubble { node = go.transform, renderer = sr };
                _bubbles.Add(b);
                Respawn(b, true);
            }
        }

        void Respawn(Bubble b, bool anywhere)
        {
            bool big = Random.value < 0.35f;
            b.renderer.sprite = big ? bubbleBig : bubbleSmall;
            // 泡泡要比鱼小一圈：16bit 缩到 0.5 左右，8bit 更小
            float s = big ? Random.Range(0.45f, 0.65f) : Random.Range(0.6f, 0.85f);
            b.node.localScale = new Vector3(s, s, 1f);

            b.speed = Random.Range(speedRange.x, speedRange.y);
            b.swayAmp = Random.Range(swayRange.x, swayRange.y);
            b.swayPhase = Random.Range(0f, Mathf.PI * 2f);

            var cam = _camera != null ? _camera.transform.position : transform.position;
            float halfW = _camera != null ? _camera.orthographicSize * _camera.aspect : 20f;

            b.baseX = cam.x + Random.Range(-halfW, halfW);

            // 起点放在该列海床之上，别让泡泡从地形里冒出来
            float lo = depthY;
            if (terrain == null) terrain = FindObjectOfType<SandbarTerrain>();
            if (terrain != null && terrain.TryGetGroundTopY(b.baseX, out float ground))
                lo = Mathf.Max(depthY, ground + 0.3f);
            if (lo > surfaceY - 1f) lo = surfaceY - 1f;

            b.bornY = anywhere ? Random.Range(lo, surfaceY) : lo + Random.Range(0f, 1.5f);
            b.node.position = new Vector3(b.baseX, b.bornY, 0f);
        }

        void Update()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || _bubbles.Count == 0) return;

            float camX = _camera.transform.position.x;
            float halfW = _camera.orthographicSize * _camera.aspect;

            foreach (var b in _bubbles)
            {
                if (b.node == null) continue;

                var p = b.node.position;
                p.y += b.speed * Time.deltaTime;

                float sway = Mathf.Sin(Time.time * 1.2f + b.swayPhase) * b.swayAmp;
                float x = b.baseX + sway;

                // 跟着镜头走：泡泡基本固定在相机横向范围内
                if (Mathf.Abs(x - camX) > halfW + sideMargin)
                {
                    b.baseX = camX + Random.Range(-halfW, halfW);
                    x = b.baseX;
                }

                if (p.y >= surfaceY) { Respawn(b, false); continue; }

                b.node.position = new Vector3(x, p.y, 0f);
            }
        }

        public void SetTerrain(SandbarTerrain t) => terrain = t;
    }
}
