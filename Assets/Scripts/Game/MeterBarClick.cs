using UnityEngine;
using UnityEngine.EventSystems;

namespace Ginei
{
    /// <summary>
    /// 勝敗メーターの各バーをダブルクリックしたとき、関連数値ウィンドウを開くための小コンポーネント。
    /// 新 Input System の <see cref="PointerEventData.clickCount"/> は環境差で 2 に達しないことがあるため、
    /// clickCount＝2 に加えて<b>連続クリックの時間差</b>でもダブルクリックを判定する（どちらかで発火）。
    /// 親（バー列）に付け、子のトラック/ラベルのクリックも EventSystem の階層探索で受ける。
    /// </summary>
    public class MeterBarClick : MonoBehaviour, IPointerClickHandler
    {
        public System.Action onDoubleClick;

        [Tooltip("連続クリックをダブルクリックと見なす最大間隔（秒・実時間）")]
        public float doubleClickInterval = 0.35f;

        private float lastClickTime = -999f;

        public void OnPointerClick(PointerEventData eventData)
        {
            bool doubleByCount = eventData != null && eventData.clickCount >= 2;
            float now = Time.unscaledTime;
            bool doubleByTime = (now - lastClickTime) <= doubleClickInterval;

            if (doubleByCount || doubleByTime)
            {
                lastClickTime = -999f; // 次の単発から数え直す
                onDoubleClick?.Invoke();
            }
            else
            {
                lastClickTime = now;
            }
        }
    }
}
