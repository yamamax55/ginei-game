using UnityEngine;
using UnityEngine.EventSystems;

namespace Ginei
{
    /// <summary>
    /// ドラッグでターゲット RectTransform を移動させる小さな uGUI 部品（決裁デスクのドラッグハンドル用）。
    /// タイトルバー等（Graphic を持つ＝raycastTarget）に付け、<see cref="target"/>（未指定なら親）を動かす。
    /// Canvas の scaleFactor で補正して見た目どおりに移動する。
    /// </summary>
    public class UIDragMove : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        /// <summary>動かす対象（未指定なら自身の親 RectTransform）。</summary>
        public RectTransform target;

        private Canvas canvas;

        public void OnBeginDrag(PointerEventData e)
        {
            if (target == null) target = transform.parent as RectTransform;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (target != null) target.SetAsLastSibling(); // 掴んだものを前面へ
        }

        public void OnDrag(PointerEventData e)
        {
            if (target == null) return;
            float sf = (canvas != null && canvas.scaleFactor > 0f) ? canvas.scaleFactor : 1f;
            target.anchoredPosition += e.delta / sf;
        }
    }
}
