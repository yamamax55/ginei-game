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

        [Tooltip("親が回転しても文字を水平に保ち、定位置に置く（旗艦の子ラベルが艦と一緒に回って読めなくなるのを防ぐ）")]
        public bool keepUpright = true;

        private Camera cam;
        private Transform anchorParent;   // 追従する親（旗艦など）
        private Vector3 worldOffset;      // 親からの見かけの相対位置（回転に巻き込まれない）
        private bool offsetCaptured;

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
            CaptureOffset();
        }

        /// <summary>
        /// 親からの相対位置を「親の回転を含まない」形で覚える。艦は Transform.up が正面＝Z 回転するため、
        /// 子ラベルは既定だと艦と一緒に回り、文字が傾いて読めず位置も回り込んでいた（実機報告）。
        /// </summary>
        private void CaptureOffset()
        {
            if (offsetCaptured) return;
            anchorParent = transform.parent;
            if (anchorParent == null) return;
            // 生成直後の localPosition（親のローカル基準）を、親の回転を打ち消したワールド相対量として保持。
            worldOffset = Vector3.Scale(transform.localPosition, anchorParent.lossyScale);
            offsetCaptured = true;
        }

        private void LateUpdate()
        {
            Apply();
            ApplyUpright();
        }

        /// <summary>親が回転しても文字を水平・定位置に保つ（親が無い＝単独ラベルなら何もしない）。</summary>
        private void ApplyUpright()
        {
            if (!keepUpright) return;
            if (!offsetCaptured) CaptureOffset();
            if (anchorParent == null) return;
            transform.rotation = Quaternion.identity;                 // 文字は常に水平
            transform.position = anchorParent.position + worldOffset; // 艦の回転で回り込まない
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
