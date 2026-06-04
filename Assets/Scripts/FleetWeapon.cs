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
        private IShipTarget manualTarget;
        private FleetMorale moraleComponent;
        private Material beamMaterial; // 実行時生成。OnDestroyで破棄

        private void Awake()
        {
            weaponArc = GetComponent<WeaponArc>();
            myStrength = GetComponent<FleetStrength>();
            moraleComponent = GetComponent<FleetMorale>();
            
            // ビーム表示用のLineRenderer（既にあれば再利用。WeaponArcとの二重追加・プレハブ焼き込み対策）
            beamLine = GetComponent<LineRenderer>();
            if (beamLine == null) beamLine = gameObject.AddComponent<LineRenderer>();
            beamLine.positionCount = 2;        // 焼き込みで0になっていると描画されないため明示
            beamLine.startWidth = beamWidth;
            beamLine.endWidth = beamWidth;
            beamLine.useWorldSpace = true;
            beamLine.numCapVertices = 2;
            beamLine.alignment = LineAlignment.View;
            beamLine.sortingOrder = 20;        // 背景(-100)や艦より手前に描画
            beamLine.enabled = false;

            // マテリアル設定 (Unlit系を使用)
            beamMaterial = new Material(Shader.Find("Sprites/Default"));
            beamMaterial.color = beamColor;
            beamLine.material = beamMaterial;
        }

        private void OnDestroy()
        {
            // 実行時生成したマテリアルを破棄（リーク防止）
            if (beamMaterial != null) Destroy(beamMaterial);
        }

        private void Update()
        {
            // 旗艦喪失で退却中は発砲停止（交戦状態も解除して機動低下を起こさない）
            if (myStrength != null && myStrength.IsRetreating)
            {
                IsInCombat = false;
                return;
            }

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
                // 指定ターゲットが有効（生存）かつ射程内ならそれを撃つ
                if (ShipCombat.IsValidTarget(manualTarget) && weaponArc.IsInArc(manualTarget.Transform))
                {
                    // 先にクールダウンを進める（PerformAttackが例外でも毎フレーム暴走しない）
                    nextFireTime = Time.time + fireInterval;
                    PerformAttack(manualTarget);
                }
                else
                {
                    // 射程外・無効なら自動攻撃に切り替え
                    AttackNearestEnemyInArc();
                }
            }
        }

        /// <summary>
        /// 射程・射角内に敵（旗艦＋配下艦）がいるかのみを判定します（毎フレーム呼ぶため軽量化に注意）
        /// </summary>
        private bool CheckEnemyInArc()
        {
            return ShipCombat.AnyEnemyInArc(transform.position, transform.up, myStrength.faction,
                weaponArc.range, weaponArc.halfAngle);
        }

        /// <summary>
        /// 攻撃ターゲットを手動で指定します（旗艦・配下艦のどちらも指定可）。
        /// </summary>
        public void SetManualTarget(IShipTarget target)
        {
            manualTarget = target;
        }

        /// <summary>
        /// 射界内の最も近い敵個艦（旗艦 or 配下艦）を検索して攻撃します。
        /// </summary>
        private void AttackNearestEnemyInArc()
        {
            IShipTarget nearest = ShipCombat.FindNearestEnemyInArc(transform.position, transform.up,
                myStrength.faction, weaponArc.range, weaponArc.halfAngle);

            if (nearest != null)
            {
                // 先にクールダウンを進める（PerformAttackが例外でも毎フレーム暴走しない）
                nextFireTime = Time.time + fireInterval;
                PerformAttack(nearest);
            }
        }

        /// <summary>
        /// 指定した個艦に攻撃を実行し、ダメージを計算します。
        /// </summary>
        private void PerformAttack(IShipTarget target)
        {
            lastFireTime = Time.time;

            // 士気による補正
            float moraleFactor = moraleComponent != null ? moraleComponent.GetMoraleFactor() : 1.0f;

            // ダメージ計算（提督攻撃・士気・側背面を集約ヘルパーで算出）
            bool isFlank;
            int finalDamage = ShipCombat.ComputeDamage(damage,
                myStrength != null ? myStrength.admiralData : null,
                moraleFactor, transform.position, target.Transform, flankMultiplier, out isFlank);

            Vector3 targetPos = target.Transform.position; // TakeDamage前に取得
            target.TakeDamage(finalDamage);

            // ダメージポップアップ（target が生死問わず position は有効）
            DamagePopup.Show(targetPos, finalDamage, isFlank);

            // ビーム演出
            FireBeam(targetPos);
            AudioManager.Instance.PlayBeam();
        }

        private void FireBeam(Vector3 targetPos)
        {
            StopAllCoroutines();
            StartCoroutine(ShowBeamCoroutine(targetPos));
        }

        private IEnumerator ShowBeamCoroutine(Vector3 targetPos)
        {
            if (beamLine == null) yield break;

            // 発射時に現在の beamColor を反映（FactionColor 等による実行時の色変更に追従）
            beamLine.material.color = beamColor;

            beamLine.enabled = true;
            beamLine.SetPosition(0, transform.position);
            beamLine.SetPosition(1, targetPos);

            yield return new WaitForSeconds(beamDuration);

            beamLine.enabled = false;
        }
    }
}

