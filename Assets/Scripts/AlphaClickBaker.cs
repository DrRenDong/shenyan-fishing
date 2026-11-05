using UnityEngine;
using UnityEngine.UI;

namespace Shenyan
{
    /// <summary>
    /// 运行时给所有带 Button 的 Image 开启「按透明像素判定点击」，
    /// 这样点击只落在图里有内容的地方，不会整个矩形框都能点。
    /// （Image.alphaHitTestMinimumThreshold 在场景里不落盘，所以必须运行时设。）
    /// 前提：贴图导入时 isReadable + SpriteMeshType.FullRect。
    /// </summary>
    [DisallowMultipleComponent]
    public class AlphaClickBaker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float threshold = 0.12f;

        void Awake()
        {
            Apply();
        }

        [ContextMenu("应用透明点击")]
        public void Apply()
        {
            int n = 0;
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img.sprite == null) continue;
                if (img.GetComponent<Button>() == null) continue;
                img.alphaHitTestMinimumThreshold = threshold;
                n++;
            }
            Debug.Log("[点击] 已对 " + n + " 个按钮开启透明像素判定");
        }
    }
}
