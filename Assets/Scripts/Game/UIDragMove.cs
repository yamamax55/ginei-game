using UnityEngine;
using UnityEngine.EventSystems;

namespace Ginei
{
    /// <summary>
    /// ドラッグでターゲット RectTransform を移動させる小さな uGUI 部品（決裁デスクのドラッグハンドル用）。
    /// タイトルバー等（Graphic を持つ＝raycastTarget）に付け、<see cref="target"/>（未指定なら親）を動かす。
    /// Canvas の scaleFactor で補正して見た目どおりに移動する。
    /// ドラッグ結果は画面内＋<see cref="TopReservedPx"/>（上メニュー帯）より下にクランプする。
    /// </summary>
    public class UIDragMove : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>動かす対象（未指定なら自身の親 RectTransform）。</summary>
        public RectTransform target;

        /// <summary>進行中のドラッグ数。盤面（<see cref="GalaxyView"/>）がこの間マウス操作を窓へ譲る＝二重反応しない。</summary>
        public static int ActiveDrags;
        /// <summary>いずれかの窓をドラッグ中か。</summary>
        public static bool AnyDragging => ActiveDrags > 0;
        /// <summary>画面上部に確保する帯（上メニューの高さ・ピクセル）。ドラッグ窓はこの帯より上へ行けない。</summary>
        public static float TopReservedPx;

        private Canvas canvas;
        private bool dragging;

        public void OnBeginDrag(PointerEventData e)
        {
            if (target == null) target = transform.parent as RectTransform;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (target != null) target.SetAsLastSibling(); // 掴んだものを前面へ
            if (!dragging) { dragging = true; ActiveDrags++; }
        }

        public void OnDrag(PointerEventData e)
        {
            if (target == null) return;
            float sf = (canvas != null && canvas.scaleFactor > 0f) ? canvas.scaleFactor : 1f;
            target.anchoredPosition += e.delta / sf;
            ClampWithinScreen();
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (dragging) { dragging = false; ActiveDrags = Mathf.Max(0, ActiveDrags - 1); }
        }

        private void LateUpdate()
        {
            // 初期位置を含め、窓が画面外・上メニュー帯より上に出ないよう毎フレーム是正（収まっていれば移動量0＝no-op）。
            if (target == null) target = transform.parent as RectTransform;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            ClampWithinScreen();
        }

        private void OnDisable()
        {
            // ドラッグ中に無効化（窓を閉じる等）されても件数を取りこぼさない。
            if (dragging) { dragging = false; ActiveDrags = Mathf.Max(0, ActiveDrags - 1); }
        }

        /// <summary>
        /// 対象を画面内＆上メニュー帯（<see cref="TopReservedPx"/>）より下に収める（ScreenSpaceOverlay 前提）。
        /// 窓が利用領域より大きい場合は<b>上端／左端を優先</b>して合わせ、反対側のはみ出しは許容する（毎フレーム振動しない）。
        /// </summary>
        private void ClampWithinScreen()
        {
            if (target == null) return;
            float sf = (canvas != null && canvas.scaleFactor > 0f) ? canvas.scaleFactor : 1f;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners); // overlay キャンバス＝スクリーン px（0=左下 1=左上 2=右上 3=右下）
            float left = corners[0].x, bottom = corners[0].y, right = corners[2].x, top = corners[1].y;

            Vector2 push = Vector2.zero;

            // 縦：上メニュー帯の下端を上限に。窓が利用高より高ければ上端合わせ（下のはみ出しは許容）。
            float topLimit = Screen.height - Mathf.Max(0f, TopReservedPx);
            float availH = topLimit; // 0（画面下端）〜 topLimit
            if ((top - bottom) >= availH) push.y -= (top - topLimit);     // 高すぎ→上端を topLimit に合わせる
            else if (top > topLimit) push.y -= (top - topLimit);
            else if (bottom < 0f) push.y += -bottom;

            // 横：窓が画面幅より広ければ左端合わせ（右のはみ出しは許容）。
            if ((right - left) >= Screen.width) push.x += -left;          // 広すぎ→左端を 0 に合わせる
            else if (right > Screen.width) push.x -= (right - Screen.width);
            else if (left < 0f) push.x += -left;

            if (push != Vector2.zero) target.anchoredPosition += push / sf;
        }
    }
}
