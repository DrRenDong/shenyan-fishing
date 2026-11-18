using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 海鸥：隔一阵子从画面一侧飞过一只，三帧扇翅膀，飞过去就销毁。
    /// 纯装饰，不影响玩法；只在地图上方掠过，不参与点选（没有碰撞体）。
    /// </summary>
    [DefaultExecutionOrder(520)]
    public class SeaGulls : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("素材")]
        public string resourceFolder = "Image/特效";
        public string spritePrefix = "海鸥_";

        [Header("节奏")]
        public float minGap = 9f;
        public float maxGap = 22f;
        public int maxAlive = 2;

        [Header("飞行")]
        public float minSpeed = 1.6f;
        public float maxSpeed = 2.8f;
        public float bobAmplitude = 0.25f;
        public float bobSpeed = 1.4f;
        public float scale = 0.55f;
        [Tooltip("排序号要压在地块（±500 时最高约 4000）之上")]
        public int sortingOrder = 4500;
        [Tooltip("翅膀每秒换帧数")]
        public float flapFps = 7f;

        readonly List<Gull> _alive = new List<Gull>();
        Sprite[] _frames;
        float _gap;

        class Gull
        {
            public GameObject go;
            public SpriteRenderer sr;
            public float dir;
            public float speed;
            public float baseY;
            public float t;
            public float endX;
        }

        void Start()
        {
            var all = Resources.LoadAll<Sprite>(resourceFolder);
            var list = new List<Sprite>();
            foreach (var s in all)
                if (s != null && s.name.StartsWith(spritePrefix)) list.Add(s);
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _frames = list.ToArray();
            _gap = Random.Range(1.5f, 4f);   // 进场先来一只，别等太久
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0) return;
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            _gap -= Time.deltaTime;
            if (_gap <= 0f)
            {
                _gap = Random.Range(minGap, maxGap);
                if (_alive.Count < maxAlive) Spawn(cam);
            }

            float viewH = cam.orthographic ? cam.orthographicSize * 2f : 10f;
            float viewW = viewH * Mathf.Max(0.1f, cam.aspect);
            Vector3 camPos = cam.transform.position;

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                Gull g = _alive[i];
                g.t += Time.deltaTime;

                float x = g.go.transform.position.x + g.dir * g.speed * Time.deltaTime;
                float y = g.baseY + Mathf.Sin(g.t * bobSpeed) * bobAmplitude;
                g.go.transform.position = new Vector3(x, y, 0f);

                int f = Mathf.FloorToInt(g.t * flapFps) % _frames.Length;
                g.sr.sprite = _frames[f];

                bool past = g.dir > 0f ? x > camPos.x + viewW * 0.62f
                                       : x < camPos.x - viewW * 0.62f;
                if (past)
                {
                    Destroy(g.go);
                    _alive.RemoveAt(i);
                }
            }
        }

        void Spawn(Camera cam)
        {
            float viewH = cam.orthographic ? cam.orthographicSize * 2f : 10f;
            float viewW = viewH * Mathf.Max(0.1f, cam.aspect);
            Vector3 camPos = cam.transform.position;

            float dir = Random.value > 0.5f ? 1f : -1f;
            float startX = camPos.x - dir * viewW * 0.6f;
            float y = camPos.y + Random.Range(-viewH * 0.35f, viewH * 0.35f);

            var go = new GameObject("gull");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(startX, y, 0f);
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _frames[0];
            sr.sortingOrder = sortingOrder;
            sr.flipX = dir < 0f;
            sr.color = new Color(1f, 1f, 1f, 0.92f);

            _alive.Add(new Gull
            {
                go = go,
                sr = sr,
                dir = dir,
                speed = Random.Range(minSpeed, maxSpeed),
                baseY = y,
                t = 0f,
            });
        }
    }
}
