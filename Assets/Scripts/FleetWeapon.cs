using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 艦隊の武装制御および攻撃を管理するクラス。
    /// 射界内の敵を自動攻撃し、側背面からの攻撃にはボーナスを与えます。
    /// </summary>
    [RequireComponent(typeof(WeaponArc))]
    public class FleetWeapon : MonoBehaviour
    {
        [Header("攻撃設定")]
        public int damage = 100;
        public float fireInterval = 1.0f;
        [Tooltip("側背面攻撃時の最大ダメージ倍率 (真後ろで最大)")]
        public float flankMultiplier = 2.0f;

        [Tooltip("交戦中の機動性倍率 (0.0〜1.0)")]
        public float combatMobilityRatio = 0.3f;

        [Header("演出設定")]
        public float beamWidth = 0.2f;
        public float beamDuration = 0.1f;
        public Color beamColor = Color.cyan;

        public bool IsInCombat { get; private set; }

        private WeaponArc weaponArc;
        private FleetStrength myStrength;
        private LineRenderer beamLine;
        private float nextFireTime;
        private float lastFireTime = -100f;
        private FleetStrength manualTarget;
        private FleetMorale moraleComponent;

        private void Awake()
        {
            weaponArc = GetComponent<WeaponArc>();
            myStrength = GetComponent<FleetStrength>();
            moraleComponent = GetComponent<FleetMorale>();
            
            // ビーム表示用のLineRendererを設定
beamLine = gameObject.AddComponent<LineRenderer>();
            beamLine.startWidth = beamWidth;
            beamLine.endWidth = beamWidth;
            beamLine.useWorldSpace = true;
            beamLine.enabled = false;
            
            // マテリアル設定 (Unlit系を使用)
            Material beamMat = new Material(Shader.Find("Sprites/Default"));
            beamMat.color = beamColor;
            beamLine.material = beamMat;
        }

        private void Update()
        {
            // 交戦状態の判定
            // 1. 直近 fireInterval 秒以内に発砲したか
            // 2. 射界内に敵がいるか
            bool enemyInArc = CheckEnemyInArc();
            IsInCombat = (Time.time < lastFireTime + fireInterval) || enemyInArc;

            // デバッグ用: Zキーで強制発射
            if (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
            {
                FireBeam(transform.position + transform.up * weaponArc.range);
                lastFireTime = Time.time;
            }

            // 自動攻撃 または 指定ターゲット攻撃
            if (Time.time >= nextFireTime)
            {
                if (manualTarget != null)
                {
                    // 指定ターゲットが有効かつ射程内かチェック
                    if (weaponArc.IsInArc(manualTarget.transform))
                    {
                        PerformAttack(manualTarget);
                        nextFireTime = Time.time + fireInterval;
                    }
                    else
                    {
                        // 射程外なら自動攻撃に切り替え
                        AttackNearestEnemyInArc();
                    }
                }
                else
                {
                    AttackNearestEnemyInArc();
                }
            }
        }

        /// <summary>
        /// 射程・射角内に敵がいるかのみを判定します（毎フレーム呼ぶため軽量化に注意）
        /// </summary>
        private bool CheckEnemyInArc()
        {
            // FindObjectsByTypeは重いため、実運用ではマネージャー管理が望ましいが、規約に従い現状維持
            FleetStrength[] targets = Object.FindObjectsByType<FleetStrength>(FindObjectsSortMode.None);
            foreach (var target in targets)
            {
                if (target == myStrength || target.faction == myStrength.faction) continue;
                if (weaponArc.IsInArc(target.transform)) return true;
            }
            return false;
        }

        /// <summary>
        /// 攻撃ターゲットを手動で指定します。
        /// </summary>
        public void SetManualTarget(FleetStrength target)
        {
            manualTarget = target;
        }

        /// <summary>
        /// 射界内の最も近い敵を検索して攻撃します。
        /// </summary>
        private void AttackNearestEnemyInArc()
        {
            FleetStrength[] targets = Object.FindObjectsByType<FleetStrength>(FindObjectsSortMode.None);
            FleetStrength nearestEnemy = null;
            float minDistance = float.MaxValue;

            foreach (var target in targets)
            {
                // 自分自身または同陣営は無視
                if (target == myStrength || target.faction == myStrength.faction) continue;

                // 射界内かチェック
                if (weaponArc.IsInArc(target.transform))
                {
                    float dist = Vector2.Distance(transform.position, target.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearestEnemy = target;
                    }
                }
            }

            if (nearestEnemy != null)
            {
                PerformAttack(nearestEnemy);
                nextFireTime = Time.time + fireInterval;
            }
        }

        /// <summary>
        /// 指定したターゲットに攻撃を実行し、ダメージを計算します。
        /// </summary>
        private void PerformAttack(FleetStrength target)
        {
            lastFireTime = Time.time;

            // 提督の攻撃力による補正
            float attackBonus = 1.0f;
            if (myStrength != null && myStrength.admiralData != null)
            {
                // 攻撃50で1.0倍, 100で1.5倍, 0で0.5倍
                attackBonus = 1.0f + (myStrength.admiralData.attack - 50) / 100f;
            }

            // 士気による補正
            float moraleFactor = moraleComponent != null ? moraleComponent.GetMoraleFactor() : 1.0f;

            // 戦術ボーナス計算 (側背面)
            Vector2 toAttacker = (transform.position - target.transform.position).normalized;
            // 被弾側の正面(Transform.up)と、攻撃者へのベクトルの内積
            float dot = Vector2.Dot(target.transform.up, toAttacker);
            
            // dot=1(正面): 倍率1.0, dot=-1(背面): 倍率flankMultiplier
            float multiplier = Mathf.Lerp(flankMultiplier, 1.0f, (dot + 1.0f) / 2.0f);
            
            int finalDamage = Mathf.RoundToInt(damage * attackBonus * moraleFactor * multiplier);
            target.TakeDamage(finalDamage);

            // ビーム演出
            FireBeam(target.transform.position);
            
            Debug.Log($"{name} が {target.name} を攻撃！ ダメージ: {finalDamage} (基本:{damage} 補正:{attackBonus:F2} 背面:{multiplier:F2})");
        }

        private void FireBeam(Vector3 targetPos)
        {
            StopAllCoroutines();
            StartCoroutine(ShowBeamCoroutine(targetPos));
        }

        private IEnumerator ShowBeamCoroutine(Vector3 targetPos)
        {
            if (beamLine == null) yield break;

            beamLine.enabled = true;
            beamLine.SetPosition(0, transform.position);
            beamLine.SetPosition(1, targetPos);

            yield return new WaitForSeconds(beamDuration);

            beamLine.enabled = false;
        }
    }
}

