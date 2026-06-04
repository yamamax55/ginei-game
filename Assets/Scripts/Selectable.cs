using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// オブジェクトが選択可能であることを示すコンポーネント。
    /// 選択状態に応じて選択リングを表示・非表示にします。
    /// </summary>
    public class Selectable : MonoBehaviour
    {
        [Header("表示設定")]
        [Tooltip("選択時に表示するリング用オブジェクト")]
        public GameObject selectionRing;

        private bool isSelected = false;

        public bool IsSelected => isSelected;

        private void Start()
        {
            // 初期状態は非表示
            if (selectionRing != null)
            {
                selectionRing.SetActive(false);
            }
        }

        /// <summary>
        /// 選択状態を切り替えます。
        /// </summary>
        /// <param name="value">trueで選択、falseで解除</param>
        public void SetSelected(bool value)
        {
            isSelected = value;
            if (selectionRing != null)
            {
                selectionRing.SetActive(isSelected);
            }
        }
    }
}
