using UnityEngine;
using UnityEngine.EventSystems;

namespace Ginei
{
    /// <summary>
    /// uGUI ウィンドウのタイトルバー用ドラッグ移動コンポーネント。
    /// このコンポーネントを「つかむ領域」（タイトルバー Image）に付け、<see cref="target"/> に
    /// 動かしたいウィンドウの RectTransform を割り当てる。ドラッグ量を Canvas のスケールで割って
    /// 参照解像度空間の移動に変換するため、CanvasScaler（ScaleWithScreenSize）でも正しく動く。
    /// HelpOverlay 等の実行時生成ウィンドウで共用する（並行実装を増やさない）。
    /// </summary>
    public class UIWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [Tooltip("ドラッグで移動させるウィンドウの RectTransform（中央アンカー想定）")]
        public RectTransform target;

        private Canvas canvas;
        private Vector2 startPointer;
        private Vector2 startAnchored;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (target == null) return;
            if (canvas == null) canvas = target.GetComponentInParent<Canvas>();
            startAnchored = target.anchoredPosition;
            startPointer = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;
            float scale = (canvas != null) ? canvas.scaleFactor : 1f;
            Vector2 delta = (eventData.position - startPointer) / Mathf.Max(scale, 0.0001f);
            target.anchoredPosition = startAnchored + delta;
        }
    }
}
