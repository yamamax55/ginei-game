using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ワールド空間ラベル(TextMesh)の見かけサイズをカメラのズームに追従させ、
    /// 画面上での大きさをほぼ一定に保つ補助コンポーネント。
    /// ズームアウトで艦が密集しても文字が極端に小さく/大きくならず、重なりと可読性を改善する。
    /// 対象は頭上ラベル(StrengthDisplay)やダメージポップアップ(DamagePopup)など。
    /// 旗艦 root には付けないこと（陣形計算が狂う）。ラベル専用の子/単独オブジェクトに付ける。
    /// </summary>
    public class LabelZoomScaler : MonoBehaviour
    {
        [Tooltip("基準とするカメラの orthographicSize（この値のとき baseScale 等倍）")]
        public float referenceOrthoSize = 16f;

        [Tooltip("ズーム追従の基準スケール（referenceOrthoSize のとき localScale はこの値）")]
        public Vector3 baseScale = Vector3.one;

        [Tooltip("拡縮率の下限（近すぎ/遠すぎでの行き過ぎを防ぐ）")]
        public float minFactor = 0.6f;

        [Tooltip("拡縮率の上限")]
        public float maxFactor = 2.0f;

        private Camera cam;

        /// <summary>基準スケールと基準ズームを指定して初期化し、即時反映する。</summary>
        public void Configure(Vector3 baseScale, float referenceOrthoSize)
        {
            this.baseScale = baseScale;
            this.referenceOrthoSize = referenceOrthoSize;
            if (cam == null) cam = Camera.main;
            Apply();
        }

        private void Awake()
        {
            cam = Camera.main;
        }

        private void LateUpdate()
        {
            Apply();
        }

        /// <summary>現在のズームに応じて localScale を更新する。</summary>
        private void Apply()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            float factor = Mathf.Clamp(
                cam.orthographicSize / Mathf.Max(0.01f, referenceOrthoSize),
                minFactor, maxFactor);
            transform.localScale = baseScale * factor;
        }
    }
}
