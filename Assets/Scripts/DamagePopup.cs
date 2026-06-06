using System.Collections;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ダメージ数値をワールド空間に浮かせて表示するポップアップ。
    /// DamagePopup.Show() で生成し、自動消滅する。
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        [Tooltip("浮上速度 (ワールド単位/秒)")]
        public float riseSpeed = 1.2f;

        [Tooltip("表示時間 (秒)")]
        public float lifetime = 0.8f;

        [Tooltip("側背面ボーナスと判定する倍率の閾値")]
        public float flankThreshold = 1.3f;

        private TextMesh textMesh;
        private MeshRenderer meshRenderer;
        private Material mat;

        // 大量ヒット時の出しすぎ防止：同時表示数の上限。超過分は間引く（生成しない）。
        // 多数の配下艦が同時に撃つため、可読性優先で控えめにする。
        public const int MaxActive = 24;
        private static int activeCount = 0;

        // 縦方向の段積み：連続生成を一定段にずらして団子化を防ぐ（重なって読めないのを軽減）。
        private const int StackSlots = 4;
        private const float StackStep = 0.45f;
        private static int stackSlot = 0;

        // 文字サイズのズーム追従に使う基準ズーム（CameraController.startZoom と揃える）。
        private const float ReferenceOrthoSize = 16f;

        /// <summary>
        /// ダメージポップアップを生成します。
        /// </summary>
        /// <param name="worldPos">生成位置（被弾側の位置）</param>
        /// <param name="damage">表示するダメージ値</param>
        /// <param name="isFlank">側背面ボーナスが発動しているか</param>
        public static void Show(Vector3 worldPos, int damage, bool isFlank)
        {
            // 上限を超えていたら間引く（多数の配下艦が同時に撃つため）
            if (activeCount >= MaxActive) return;
            activeCount++;

            var go = new GameObject("DamagePopup");
            // 団子化を防ぐ：水平はわずかに、垂直はスロットで段階的にずらして縦に積む
            int slot = stackSlot % StackSlots;
            stackSlot++;
            Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), slot * StackStep, 0f);
            go.transform.position = worldPos + offset;
            var popup = go.AddComponent<DamagePopup>();
            popup.Init(damage, isFlank);
        }

        private void Init(int damage, bool isFlank)
        {
            // フォント読み込みは FontProvider に集約（日本語・Unity6 の Arial.ttf 禁止対応もそこで一元化）
            Font jaFont = FontProvider.JapaneseFont;

            textMesh = gameObject.AddComponent<TextMesh>();
            textMesh.font = jaFont;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;

            if (isFlank)
            {
                textMesh.text = $"{damage}\n側背面!";
                textMesh.fontSize = 80;
                textMesh.characterSize = 0.22f;
                textMesh.color = new Color(1f, 0.4f, 0.1f, 1f); // 赤橙
            }
            else
            {
                textMesh.text = damage.ToString();
                textMesh.fontSize = 60;
                textMesh.characterSize = 0.18f;
                textMesh.color = Color.white;
            }

            meshRenderer = GetComponent<MeshRenderer>();
            if (jaFont != null)
                meshRenderer.sharedMaterial = jaFont.material;

            // フェード用にマテリアルインスタンスを取得
            mat = meshRenderer.material;

            // 文字サイズをズームに追従させ、画面上での見かけ大きさを一定に保つ（重なり軽減・可読性）
            LabelZoomScaler scaler = gameObject.AddComponent<LabelZoomScaler>();
            scaler.Configure(Vector3.one, ReferenceOrthoSize);

            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            float elapsed = 0f;
            Color startColor = textMesh.color;

            while (elapsed < lifetime)
            {
                // timeScale に追従（倍速時は速く消え、発射ペースと同期して溜まらない）
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                // 上へ浮上
                transform.position += Vector3.up * riseSpeed * Time.deltaTime;

                // フェードアウト（後半から）
                float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.4f, 1f, t));
                Color c = startColor;
                c.a = alpha;
                if (mat != null) mat.color = c;

                yield return null;
            }

            if (mat != null) Destroy(mat);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            activeCount = Mathf.Max(0, activeCount - 1);
            if (mat != null) Destroy(mat);
        }
    }
}
