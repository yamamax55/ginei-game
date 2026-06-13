using UnityEngine;
using UnityEngine.EventSystems;

namespace Ginei
{
    /// <summary>
    /// 通知行に付け、ダブルクリックで <see cref="NotificationActionRegistry"/> の登録アクションを実行する小部品。
    /// 接敵通知の「ダブルクリックで潜行」を成立させる（通常の行はクリックスルーだが、アクション付きの行だけ反応する）。
    /// 行の Graphic（ラベル）が raycastTarget=true のときだけポインタを受ける。
    /// </summary>
    public class NotificationRowClick : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>この行が表す通知の seq（NotificationActionRegistry の引き当てキー）。</summary>
        public long seq;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.clickCount >= 2) NotificationActionRegistry.TryInvoke(seq);
        }
    }
}
