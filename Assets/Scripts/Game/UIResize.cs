using UnityEngine;
using UnityEngine.EventSystems;

namespace Ginei
{
    /// <summary>
    /// ドラッグでターゲット RectTransform のサイズを変える小さな uGUI 部品（ウィンドウの右下リサイズグリップ用）。
    /// <see cref="UIDragMove"/> の対（移動ではなく拡縮）。グリップ（右下隅・Graphic を持つ＝raycastTarget）に付け、
    /// <see cref="target"/>（未指定なら親）の <c>sizeDelta</c> を <see cref="minSize"/> 以上で増減する。
    /// Canvas の scaleFactor で補正して見た目どおりに拡縮する。ピボット中央のウィンドウ前提（右へ＋下へ広がる）。
    /// </summary>
    public class UIResize : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>拡縮する対象（未指定なら自身の親 RectTransform）。</summary>
        public RectTransform target;
        /// <summary>最小サイズ（px・これ以下に縮めない）。</summary>
        public Vector2 minSize = new Vector2(480f, 320f);

        /// <summary>進行中のリサイズ数（盤面が入力を譲るのに使える）。</summary>
        public static int ActiveResizes;
        public static bool AnyResizing => ActiveResizes > 0;

        private Canvas canvas;
        private bool resizing;

        public void OnBeginDrag(PointerEventData e)
        {
            if (target == null) target = transform.parent as RectTransform;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (!resizing) { resizing = true; ActiveResizes++; }
        }

        public void OnDrag(PointerEventData e)
        {
            if (target == null) return;
            float sf = (canvas != null && canvas.scaleFactor > 0f) ? canvas.scaleFactor : 1f;
            Vector2 d = e.delta / sf;
            Vector2 size = target.sizeDelta;
            size.x = Mathf.Max(minSize.x, size.x + d.x); // 右へ広げる
            size.y = Mathf.Max(minSize.y, size.y - d.y); // 下へ広げる（y は上方向が正）
            target.sizeDelta = size;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (resizing) { resizing = false; ActiveResizes = Mathf.Max(0, ActiveResizes - 1); }
        }

        private void OnDisable()
        {
            if (resizing) { resizing = false; ActiveResizes = Mathf.Max(0, ActiveResizes - 1); }
        }
    }
}
