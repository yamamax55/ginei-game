using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊の射程と射角（扇形の攻撃範囲）を管理・可視化するクラス。
    /// </summary>
    public class WeaponArc : MonoBehaviour
    {
        [Header("射界設定")]
        [Tooltip("射程距離")]
        public float range = 10f;

        [Tooltip("射角の半分 (度)。正面を中心に左右にこの角度分だけ撃てる。")]
        public float halfAngle = 30f;

        [Tooltip("射界線の色")]
        public Color gizmoColor = Color.red;

        private LineRenderer runtimeArc;
        private Material arcMaterial; // 実行時生成。OnDestroyで破棄

        private void Start()
        {
            SetupRuntimeArc();
        }

        private void Update()
        {
            if (runtimeArc != null)
            {
                bool show = GameSettings.Instance.alwaysShowGizmos;
                runtimeArc.enabled = show;
                if (show)
                {
                    UpdateRuntimeArc();
                }
            }
        }

        private void SetupRuntimeArc()
        {
            // 射界線は専用の子オブジェクトに置く（FleetWeapon のビーム用 LineRenderer と衝突しないように）
            GameObject arcObj = new GameObject("WeaponArcLine");
            arcObj.transform.SetParent(transform, false);
            runtimeArc = arcObj.AddComponent<LineRenderer>();
            runtimeArc.startWidth = 0.05f;
            runtimeArc.endWidth = 0.05f;
            runtimeArc.useWorldSpace = true;
            runtimeArc.loop = true;
            arcMaterial = new Material(Shader.Find("Sprites/Default"));
            runtimeArc.material = arcMaterial;
            runtimeArc.startColor = gizmoColor;
            runtimeArc.endColor = gizmoColor;
            runtimeArc.enabled = false;
            // Sorting order
            runtimeArc.sortingOrder = 10;
        }

        private void OnDestroy()
        {
            // 実行時生成したマテリアルを破棄（リーク防止）
            if (arcMaterial != null) Destroy(arcMaterial);
        }

        private void UpdateRuntimeArc()
        {
            if (runtimeArc == null) return;
            runtimeArc.startColor = gizmoColor;
            runtimeArc.endColor = gizmoColor;

            int segments = 20;
runtimeArc.positionCount = segments + 2; // center + points along arc

            Vector3 pos = transform.position;
            Vector3 forward = transform.up;

            runtimeArc.SetPosition(0, pos);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float currentAngle = Mathf.Lerp(halfAngle, -halfAngle, t);
                Vector3 point = pos + (Quaternion.Euler(0, 0, currentAngle) * forward * range);
                runtimeArc.SetPosition(i + 1, point);
            }
        }

        /// <summary>
        /// 指定したターゲットが射界内に入っているかを判定します。
/// </summary>
        /// <param name="target">判定対象のTransform</param>
        /// <returns>射界内ならtrue</returns>
        public bool IsInArc(Transform target)
        {
            if (target == null) return false;

            Vector2 toTarget = target.position - transform.position;
            float distance = toTarget.magnitude;

            // 1. 射程距離チェック
            if (distance > range) return false;

            // 2. 射角チェック
            // 正面方向 (Transform.up) とターゲット方向の角度差を計算
            float angle = Vector2.Angle(transform.up, toTarget);

            return angle <= halfAngle;
        }

        /// <summary>
        /// Sceneビューで射界を可視化します。
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;

            Vector3 pos = transform.position;
            Vector3 forward = transform.up;

            // 左右の境界方向を計算
            Quaternion leftRotation = Quaternion.Euler(0, 0, halfAngle);
            Quaternion rightRotation = Quaternion.Euler(0, 0, -halfAngle);

            Vector3 leftRay = leftRotation * forward * range;
            Vector3 rightRay = rightRotation * forward * range;

            // 1. 半径方向の2辺を描画
            Gizmos.DrawLine(pos, pos + leftRay);
            Gizmos.DrawLine(pos, pos + rightRay);

            // 2. 円弧部分を描画 (簡易的に複数線分で構成)
            int segments = 20;
            Vector3 prevPoint = pos + leftRay;

            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                // leftからrightへ補間
                float currentAngle = Mathf.Lerp(halfAngle, -halfAngle, t);
                Vector3 currentPoint = pos + (Quaternion.Euler(0, 0, currentAngle) * forward * range);
                
                Gizmos.DrawLine(prevPoint, currentPoint);
                prevPoint = currentPoint;
            }
        }
    }
}
