using UnityEngine;
using UnityEngine.EventSystems;

namespace Ginei
{
    /// <summary>
    /// タイトルバー等につけてドラッグ量（ピクセル delta）をコールバックする小コンポーネント。
    /// <see cref="StrategyMapWindow"/> がマップ窓の正規化矩形を動かすのに使う（anchoredPosition は触らない）。
    /// </summary>
    public class MapWindowDrag : MonoBehaviour, IDragHandler
    {
        public System.Action<Vector2> onDragDelta;

        public void OnDrag(PointerEventData eventData)
        {
            onDragDelta?.Invoke(eventData.delta);
        }
    }
}
