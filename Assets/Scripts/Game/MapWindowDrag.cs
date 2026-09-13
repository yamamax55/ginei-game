using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ginei
{
    /// <summary>
    /// タイトルバー／リサイズグリップにつけて、掴んでいる間の移動量（ピクセル）をコールバックする。
    /// <see cref="StrategyMapWindow"/> がマップ窓の正規化矩形を動かすのに使う（anchoredPosition は触らない）。
    ///
    /// <b>ドラッグ判定を EventSystem の drag イベントに頼らない</b>のが要点（#MAPドラッグが効かない）。
    /// 実機QAで「通常クリックは応答するのにタイトルバーと右下グリップのドラッグだけ応答しない」現象が出た。
    /// EventSystem の drag は「押下→しきい値を超える移動→drag イベント」という連鎖で作られるため、
    /// カーソルを瞬間移動させる自動入力ではしきい値や delta が期待どおりに積まれず、drag が発火しないことがある。
    /// そこで <b>押下だけを EventSystem から受け取り、以後は毎フレーム実際のポインタ座標の差分を自分で計算</b>する。
    /// こうすると人の手でも自動入力でも同じように動き、途中でカーソルが枠外へ出ても掴んだままになる。
    /// </summary>
    public class MapWindowDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        /// <summary>掴んでいる間、毎フレームの移動量（スクリーンピクセル）を渡す。</summary>
        public System.Action<Vector2> onDragDelta;

        /// <summary>いずれかの窓ハンドルを掴んでいるか（盤面が同時にスクロールしないよう GalaxyView が見る）。</summary>
        public static bool AnyGrabbing { get; private set; }

        private bool grabbing;
        private Vector2 lastPointer;

        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        /// <summary>
        /// 受信診断（既定 OFF）。ON にすると押下/ドラッグ/離しの受信と移動量を Console に出す。
        /// 「ドラッグが効かない」の原因が①イベントが来ていない ②来ているが移動量が0
        /// ③移動量はあるが窓側で打ち消されている、のどれかを実機で切り分けるためのもの。
        /// </summary>
        public static bool LogEvents;

        public void OnPointerDown(PointerEventData eventData)
        {
            grabbing = true;
            AnyGrabbing = true;
            lastPointer = CurrentPointer(eventData.position);
            if (LogEvents)
                Debug.Log($"[MapWindowDrag] {name} PointerDown at {eventData.position} " +
                          $"(mouse={(Mouse.current != null ? Mouse.current.position.ReadValue().ToString() : "なし")})");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 離す直前の移動を取りこぼさない（最後の1フレームぶんを反映してから解除）。
            FlushDelta(eventData.position);
            Release();
        }

        /// <summary>drag イベントが来る環境ではそれも受ける（Update と二重に動かさないよう位置だけ更新）。</summary>
        public void OnDrag(PointerEventData eventData)
        {
            // 位置追従は Update / FlushDelta に一本化する（ここでは受信の記録だけ）。
            if (LogEvents) Debug.Log($"[MapWindowDrag] {name} OnDrag delta={eventData.delta} pos={eventData.position}");
        }

        private void Update()
        {
            if (!grabbing) return;

            var mouse = Mouse.current;
            // ボタンが離れていたら、最後の移動を反映してから掴みを解除
            //（枠外で離した・フォーカスを失った場合に PointerUp が来ないことへの保険）。
            if (mouse != null && !mouse.leftButton.isPressed)
            {
                FlushDelta(mouse.position.ReadValue());
                Release();
                return;
            }

            FlushDelta(CurrentPointer(lastPointer));
        }

        /// <summary>前回位置からの差分を1回だけ通知して基準を進める（二重に動かさない）。</summary>
        private void FlushDelta(Vector2 pointer)
        {
            if (!grabbing) return;
            Vector2 delta = pointer - lastPointer;
            lastPointer = pointer;
            if (delta.sqrMagnitude > 0f)
            {
                if (LogEvents) Debug.Log($"[MapWindowDrag] {name} 反映 delta={delta}");
                onDragDelta?.Invoke(delta);
            }
        }

        /// <summary>実際のポインタ座標（新 Input System 優先・取れなければ引数のフォールバック）。</summary>
        private static Vector2 CurrentPointer(Vector2 fallback)
        {
            var mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : fallback;
        }

        private void Release()
        {
            if (!grabbing) return;
            grabbing = false;
            AnyGrabbing = false;
        }
    }
}
