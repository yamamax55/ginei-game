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

        [Header("ブラックホール回避")]
        [Tooltip("AI制御艦がブラックホールの引力圏を避けて移動するか")]
        public bool avoidBlackHoles = true;

        [Tooltip("引力圏(pullRadius)に加える安全マージン。この距離だけ余分に離れて避ける")]
        public float blackHoleSafeMargin = 3f;

        [Tooltip("回避ステアリングの強さ（大きいほど強く進路を曲げて避ける）")]
        public float blackHoleAvoidStrength = 2.0f;

        [Header("撤退")]
        [Tooltip("撤退時、敵が近い間は後退移動で下がる（側背面＝背中を見せない退却）")]
        public bool useReverseRetreat = true;

        [Tooltip("この距離以内に敵がいる撤退では後退移動を使う（遠ければ通常移動で素早く離脱）")]
        public float reverseRetreatRange = 14f;

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
            // 全旗艦から敵対する旗艦のみを対象に最寄りを探す（接近・交戦の目標は旗艦単位）
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            float minDistance = float.MaxValue;
            targetEnemy = null;

            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength fleet = flagships[i];
                if (fleet == null || !fleet.IsAlive) continue;
                if (fleet == strength) continue;                       // 自分は除外
                if (!FactionRelations.IsHostile(strength, fleet)) continue; // 敵対勢力のみ

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
            Vector2 pos = transform.position;

            // ── ブラックホール緊急離脱（全状態に優先）──
            // 引力圏に入っていたら、交戦・接近より離脱を最優先する。
            if (avoidBlackHoles && TryGetBlackHoleEscape(pos, out Vector2 escape))
            {
                movement.SetDestination(escape);
                return;
            }

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
                        // 敵に向かって移動（進路上のブラックホールは迂回）
                        movement.SetDestination(SteerAroundBlackHoles(pos, targetEnemy.transform.position));
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
                        // 敵と反対方向へ逃げる（逃走先のブラックホールも迂回）
                        Vector2 awayDir = ((Vector2)transform.position - (Vector2)targetEnemy.transform.position).normalized;
                        Vector2 fleeTarget = SteerAroundBlackHoles(pos, pos + awayDir * 20f);

                        // 敵が近い間は後退移動で下がる（向き＝射界を保ち背中を見せない）。
                        // 遠ければ通常移動（回頭して素早く離脱）。
                        float distToEnemy = Vector2.Distance(pos, targetEnemy.transform.position);
                        if (useReverseRetreat && distToEnemy <= reverseRetreatRange)
                            movement.SetReverseDestination(fleeTarget);
                        else
                            movement.SetDestination(fleeTarget);
                    }
                    break;
            }
        }

        // ────────────────────────────────────────────────
        // ブラックホール回避
        // ────────────────────────────────────────────────

        /// <summary>
        /// 引力圏(pullRadius＋安全マージン)に入っている場合、最も危険なブラックホールから
        /// 圏外へ脱出する目標座標を返す。圏内でなければ false。
        /// </summary>
        private bool TryGetBlackHoleEscape(Vector2 pos, out Vector2 escapeTarget)
        {
            escapeTarget = pos;
            IReadOnlyList<BlackHole> holes = BlackHole.All;
            if (holes == null || holes.Count == 0) return false;

            BlackHole worst = null;
            float worstPenetration = 0f;
            float worstDanger = 0f;
            Vector2 worstCenter = Vector2.zero;
            float worstDist = 0f;

            for (int i = 0; i < holes.Count; i++)
            {
                BlackHole h = holes[i];
                if (h == null) continue;

                Vector2 center = h.transform.position;
                float danger = h.pullRadius + blackHoleSafeMargin;
                float dist = Vector2.Distance(pos, center);
                if (dist >= danger) continue;

                float penetration = danger - dist; // 圏内へどれだけ食い込んでいるか
                if (penetration > worstPenetration)
                {
                    worstPenetration = penetration;
                    worst = h;
                    worstDanger = danger;
                    worstCenter = center;
                    worstDist = dist;
                }
            }

            if (worst == null) return false;

            // 中心と反対方向へ、圏外（danger ＋ 少し）まで離れる地点を目標にする。
            Vector2 away = worstDist > 0.001f
                ? (pos - worstCenter) / worstDist
                : new Vector2(1f, 0f); // 中心に重なっている場合の保険
            escapeTarget = worstCenter + away * (worstDanger + 2f);
            return true;
        }

        /// <summary>
        /// 目標へ向かう進路上にブラックホールの引力圏があれば、横へ回り込むよう
        /// 目標方向を曲げた「ステアリング済みの目標座標」を返す。
        /// 距離は元の目標までと同程度に保ち、毎フレーム呼ばれて滑らかに迂回する。
        /// </summary>
        private Vector2 SteerAroundBlackHoles(Vector2 pos, Vector2 desiredTarget)
        {
            if (!avoidBlackHoles) return desiredTarget;
            IReadOnlyList<BlackHole> holes = BlackHole.All;
            if (holes == null || holes.Count == 0) return desiredTarget;

            Vector2 toTarget = desiredTarget - pos;
            float targetDist = toTarget.magnitude;
            if (targetDist < 0.001f) return desiredTarget;
            Vector2 dir = toTarget / targetDist;

            Vector2 steer = Vector2.zero;
            for (int i = 0; i < holes.Count; i++)
            {
                BlackHole h = holes[i];
                if (h == null) continue;

                Vector2 center = h.transform.position;
                float danger = h.pullRadius + blackHoleSafeMargin;

                // 進路（pos→目標）に対するブラックホール中心の最近接点を求める
                float along = Vector2.Dot(center - pos, dir);
                // 自分より後ろ、または目標よりかなり先のブラックホールは無視
                if (along <= 0f || along > targetDist + danger) continue;

                Vector2 closest = pos + dir * Mathf.Clamp(along, 0f, targetDist);
                float perpDist = Vector2.Distance(closest, center);
                if (perpDist >= danger) continue; // 進路から十分離れていれば曲げ不要

                // 進路に対して中心の反対側へ押し出す垂直ベクトル
                Vector2 perp = closest - center;
                if (perp.sqrMagnitude < 0.0001f)
                {
                    // 中心に真っ直ぐ突っ込む構図：進路の左右どちらかへ確実に逃がす
                    perp = new Vector2(-dir.y, dir.x);
                }
                perp.Normalize();

                // 近いほど・手前にあるほど強く曲げる
                float push = (danger - perpDist) / danger;
                steer += perp * (push * blackHoleAvoidStrength);
            }

            if (steer == Vector2.zero) return desiredTarget;

            Vector2 steeredDir = (dir + steer).normalized;
            return pos + steeredDir * targetDist;
        }
    }
}
