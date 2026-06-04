using UnityEngine;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 艦隊の意思決定（AI）を行うクラス。
    /// 接近・交戦・撤退のシンプルなステートマシンで動作します。
    /// </summary>
    [RequireComponent(typeof(FleetMovement))]
    [RequireComponent(typeof(FleetWeapon))]
    [RequireComponent(typeof(FleetStrength))]
    public class FleetAI : MonoBehaviour
    {
        public enum AIState
        {
            接近, // Approach
            交戦, // Engage
            撤退  // Retreat
        }

        [Header("AI設定")]
        public AIState currentState = AIState.接近;
        
        [Tooltip("撤退を開始する兵力割合 (0.0〜1.0)")]
        public float retreatRatio = 0.3f;

        [Tooltip("敵を再探索する間隔 (秒)")]
        public float searchInterval = 2.0f;

        private FleetMovement movement;
        private FleetWeapon weapon;
        private FleetStrength strength;
        private WeaponArc weaponArc;

        private float nextSearchTime;
        private FleetStrength targetEnemy;
        private FleetMorale moraleComponent;

        private void Awake()
        {
            movement = GetComponent<FleetMovement>();
            weapon = GetComponent<FleetWeapon>();
            strength = GetComponent<FleetStrength>();
            weaponArc = GetComponent<WeaponArc>();
            moraleComponent = GetComponent<FleetMorale>();
        }

        private void Update()
        {
            // 敗走チェック (最優先)
            if (moraleComponent != null && moraleComponent.IsRouted)
            {
                currentState = AIState.撤退;
            }
            // 兵力チェックによる撤退判断
            else if (currentState != AIState.撤退 && (float)strength.strength / strength.maxStrength < retreatRatio)
            {
                currentState = AIState.撤退;
            }

            // 一定間隔で敵を再探索
if (Time.time >= nextSearchTime)
            {
                SearchNearestEnemy();
                nextSearchTime = Time.time + searchInterval;
            }

            // 状態別の行動
            UpdateStateBehavior();
        }

        /// <summary>
        /// 最も近い敵対艦隊を探します。
        /// </summary>
        private void SearchNearestEnemy()
        {
            // 敵旗艦のみをレジストリから取得（接近・交戦の目標は旗艦単位）
            IReadOnlyList<FleetStrength> enemies = FleetRegistry.GetEnemyFlagships(strength.faction);
            float minDistance = float.MaxValue;
            targetEnemy = null;

            for (int i = 0; i < enemies.Count; i++)
            {
                FleetStrength fleet = enemies[i];
                if (fleet == null || !fleet.IsAlive) continue;

                float dist = Vector2.Distance(transform.position, fleet.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    targetEnemy = fleet;
                }
            }
        }

        /// <summary>
        /// 現在の状態に応じた挙動を実行します。
        /// </summary>
        private void UpdateStateBehavior()
        {
            if (targetEnemy == null && currentState != AIState.撤退)
            {
                // 敵がいない場合は停止
                return;
            }

            switch (currentState)
            {
                case AIState.接近:
                    if (weaponArc.IsInArc(targetEnemy.transform))
                    {
                        // 射程に入ったら交戦状態へ
                        currentState = AIState.交戦;
                    }
                    else
                    {
                        // 敵に向かって移動
                        movement.SetDestination(targetEnemy.transform.position);
                    }
                    break;

                case AIState.交戦:
                    if (!weaponArc.IsInArc(targetEnemy.transform))
                    {
                        // 射程外に逃げられたら再び接近
                        currentState = AIState.接近;
                    }
                    else
                    {
                        // 射程内ならその場で停止し、敵の方向を向いて射界を維持
                        // （FleetWeaponが自動で撃つ。前進はしない）
                        movement.FaceTarget(targetEnemy.transform.position);
                    }
                    break;

                case AIState.撤退:
                    if (targetEnemy != null)
                    {
                        // 敵と反対方向へ逃げる
                        Vector3 awayDir = (transform.position - targetEnemy.transform.position).normalized;
                        // 十分遠い場所を目標に設定
                        movement.SetDestination(transform.position + awayDir * 20f);
                    }
                    break;
            }
        }
    }
}
