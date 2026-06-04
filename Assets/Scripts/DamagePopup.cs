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
        public const int MaxActive = 48;
        private static int activeCount = 0;

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
            // 同一座標への重なり（団子化）を防ぐため、水平に小さくばらつかせる
            Vector3 jitter = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0f, 0.2f), 0f);
            go.transform.position = worldPos + jitter;
            var popup = go.AddComponent<DamagePopup>();
            popup.Init(damage, isFlank);
        }

        private void Init(int damage, bool isFlank)
        {
            // フォント読み込み（FleetStrength と同じパターン）
            // Unity 6 では "Arial.ttf" は廃止され例外を投げるため "LegacyRuntime.ttf" を使う
            Font jaFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if UNITY_EDITOR
            Font customFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/msgothic.ttc");
            if (customFont != null) jaFont = customFont;
#endif

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
