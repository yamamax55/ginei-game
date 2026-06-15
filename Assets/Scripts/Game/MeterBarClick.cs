using UnityEngine;
using UnityEngine.EventSystems;

namespace Ginei
{
    /// <summary>
    /// 勝敗メーターの各バーをダブルクリックしたとき、関連数値ウィンドウを開くための小コンポーネント。
    /// クリック判定は EventSystem に委譲（<see cref="PointerEventData.clickCount"/>＝2 でダブルクリック）。
    /// 親（バー列）に付け、子のトラック/ラベルのクリックも階層伝播で受ける。
    /// </summary>
    public class MeterBarClick : MonoBehaviour, IPointerClickHandler
    {
        public System.Action onDoubleClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.clickCount >= 2)
                onDoubleClick?.Invoke();
        }
    }
}
