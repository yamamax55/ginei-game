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

        [Header("キーティング（間合い調整）#2254")]
        [Tooltip("速度優位がある交戦時、IdealRange に対する許容誤差（デッドゾーン）。射程の約10%を既定とする")]
        public float keetingDeadzone = 1.0f;

        [Header("非戦闘艦の回避 #128")]
        [Tooltip("非戦闘艦（偵察/入植/輸送）がこの距離以内の敵から逃げる（戦線を張らず交戦を避ける）")]
        public float nonCombatEvadeRange = 22f;

        [Header("ZOC回避 #81")]
        [Tooltip("交戦意図のない移動（接近の通過・撤退）で、敵ZOCを横切らないよう進路を補正する")]
        public bool avoidEnemyZoc = true;

        [Tooltip("ZOC回避ステアリングの強さ（大きいほど強く進路を曲げて避ける）")]
        public float zocAvoidStrength = 1.5f;

        [Header("会戦改善")]
        [Tooltip("撤退中、原点からこの距離（自勢力側の画面端）に達したら戦場から離脱（恒久退却）")]
        public float battlefieldRadius = 45f;

        [Tooltip("戦況（兵力比・敗走）に応じて有利な陣形へ自動切替する（AI艦隊のみ）")]
        public bool autoFormation = true;

        private FleetMovement movement;
        private FleetWeapon weapon;
        private FleetStrength strength;
        private WeaponArc weaponArc;

        private float nextSearchTime;
        private FleetStrength targetEnemy;
        private FleetMorale moraleComponent;
        private Squadron squadron;

        private void Awake()
        {
            movement = GetComponent<FleetMovement>();
            weapon = GetComponent<FleetWeapon>();
            strength = GetComponent<FleetStrength>();
            weaponArc = GetComponent<WeaponArc>();
            moraleComponent = GetComponent<FleetMorale>();
            squadron = GetComponent<Squadron>();
        }

        private void Update()
        {
            // 既に戦場から離脱（恒久退却）した艦は何もしない。
            if (strength != null && !strength.IsAlive) return;

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
                UpdateFormationDoctrine(); // 戦況に応じて有利な陣形へ自動切替（#会戦改善）
                ConsiderActiveCommand();   // 状況に応じて特殊指揮を発動（#2253）
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
        /// 戦況（自/敵の兵力比・敗走）と提督の得意陣形から有利な陣形へ自動切替（AI艦隊のみ・#会戦改善 #5）。
        /// 判断は <see cref="FormationDoctrineRules"/> へ委譲し、結果を自部隊の陣形へ反映する。
        /// </summary>
        private void UpdateFormationDoctrine()
        {
            if (!autoFormation || squadron == null || strength == null || !strength.IsCombatant) return;
            float own = strength.strength;
            float enemy = (targetEnemy != null && targetEnemy.IsAlive) ? targetEnemy.strength : own; // 敵不明は等倍扱い
            bool routed = moraleComponent != null && moraleComponent.IsRouted;
            Formation rec = FormationDoctrineRules.RecommendFormation(own, enemy, routed, strength.admiralData);

            // #2253：有能なAIは敵陣形をカウンターする陣形に切り替える（三すくみ）。弱AIは取りこぼす。
            if (!routed && targetEnemy != null && targetEnemy.IsAlive
                && BattleAiRules.ShouldAct(AiSkill(), UnityEngine.Random.value))
            {
                Squadron enemySq = targetEnemy.GetComponent<Squadron>();
                if (enemySq != null)
                {
                    Formation counter = BattleAiRules.CounterFormation(enemySq.currentFormation);
                    if (FormationMatchupRules.AttackFactor(counter, enemySq.currentFormation) > 1f) rec = counter;
                }
            }
            if (squadron.currentFormation != rec) squadron.currentFormation = rec;
        }

        /// <summary>会戦AIの目利き（0..1）＝提督の実効統率＋情報を正規化。提督不在は中庸0.5。</summary>
        private float AiSkill()
        {
            AdmiralData ad = strength != null ? strength.admiralData : null;
            if (ad == null) return 0.5f;
            return Mathf.Clamp01((ad.EffectiveLeadership + ad.EffectiveIntelligence) / 200f);
        }

        /// <summary>状況に応じて特殊指揮を発動する（#2253・難易度ゲート）。</summary>
        private void ConsiderActiveCommand()
        {
            if (strength == null || !strength.IsCombatant || !strength.IsAlive) return;
            if (!BattleAiRules.ShouldAct(AiSkill(), UnityEngine.Random.value)) return;

            bool engaged = currentState == AIState.交戦;
            float moraleRatio = (moraleComponent != null && moraleComponent.maxMorale > 0f)
                ? moraleComponent.morale / moraleComponent.maxMorale : 1f;
            float enemyStr = (targetEnemy != null && targetEnemy.IsAlive) ? targetEnemy.strength : strength.strength;
            float advantage = enemyStr > 0f ? strength.strength / enemyStr : 1f;

            if (BattleAiRules.TryChooseCommand(engaged, moraleRatio, advantage, out ActiveCommand cmd))
                ActiveCommandState.Issue(strength, cmd);
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

            // ── 非戦闘艦（#128）：戦線を張らず、近い敵からは逃げる（接近・交戦はしない）──
            if (strength != null && !strength.IsCombatant)
            {
                UpdateNonCombatEvade(pos);
                return;
            }

            // ── 静観（#817 関ヶ原型）：山上で動かない＝接近も交戦もしない（発砲停止は FleetWeapon 側）──
            if (strength != null && !strength.IsFighting) return;

            // 交戦規定（ROE・#2258）：退避スタンスは前進停止（撤退相当の行動に委ねる）。
            // AdvanceFactor は接近中の前進判定で参照（後述の 接近 case）。
            if (strength != null && RoeRules.AdvanceFactor(strength.stance) <= 0f
                && currentState != AIState.撤退)
                return;

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
                        // 交戦規定（ROE・#2258）：攻撃的以外は追尾（深追い）しない。
                        // 防御的/射撃管制では前進を抑制（AdvanceFactor が0より大きければ接近するが距離は縮めすぎない）。
                        if (strength != null && !RoeRules.CanPursue(strength.stance)) break;

                        // 敵に向かって移動（進路上のブラックホールは迂回）。
                        // 進路上の「交戦対象以外」の敵ZOCは避ける（対象のZOCは意図して踏み込むので無視）。
                        Vector2 dest = SteerAroundBlackHoles(pos, targetEnemy.transform.position);
                        if (avoidEnemyZoc)
                            dest = ZoneOfControl.SteerAround(strength, pos, dest, zocAvoidStrength, targetEnemy);
                        movement.SetDestination(dest);
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
                        // ── #2254 キーティング（間合い調整）──
                        // 自部隊が速度優位を持つ場合のみ射程帯に基づいて間合いを調整する。
                        // 速度劣位・速度情報不明のときは従来どおり FaceTarget に留める（基準値は非破壊）。
                        bool kiting = false;
                        FleetMovement enemyMovement = targetEnemy.GetComponent<FleetMovement>();
                        if (enemyMovement != null && weaponArc != null && movement.maxSpeed > enemyMovement.maxSpeed)
                        {
                            Vector2 pos2d = transform.position;
                            Vector2 enemyPos2d = targetEnemy.transform.position;
                            float currentDist = Vector2.Distance(pos2d, enemyPos2d);

                            float idealRange = RangeBandRules.IdealRange(weaponArc.preferredBand, weaponArc.range);
                            int direction = RangeBandRules.ApproachOrWithdraw(currentDist, idealRange, keetingDeadzone);

                            if (direction > 0)
                            {
                                movement.SetDestination(enemyPos2d);       // 遠すぎ→接近
                                kiting = true;
                            }
                            else if (direction < 0)
                            {
                                Vector2 awayDir = (pos2d - enemyPos2d).normalized; // 近すぎ→射界を保って後退
                                movement.SetReverseDestination(pos2d + awayDir * (idealRange - currentDist + keetingDeadzone));
                                kiting = true;
                            }
                        }
                        if (!kiting)
                        {
                            // 速度優位なし・デッドゾーン内・速度情報不明＝従来動作（射界維持・停止）
                            movement.FaceTarget(targetEnemy.transform.position);
                        }
                    }
                    break;

                case AIState.撤退:
                    {
                        // 敵不明（敗走で目標を見失う等）なら原点と反対＝外周方向を「自勢力端」とみなして目指す（#会戦改善 #3）。
                        Vector2 enemyPos = targetEnemy != null ? (Vector2)targetEnemy.transform.position : Vector2.zero;

                        // 自勢力側の画面端に到達したら戦場から離脱（恒久退却＝終了処理を締める #会戦改善 #1/#2）。
                        if (BattleWithdrawalRules.IsAtWithdrawalEdge(pos, enemyPos, battlefieldRadius))
                        {
                            if (strength != null && strength.IsAlive) strength.BeginRetreat();
                            return;
                        }

                        // 自勢力端へ向かう逃走目標（敵と反対／敵不明なら外周方向）。
                        Vector2 fleeTarget = BattleWithdrawalRules.WithdrawalTarget(pos, enemyPos, 20f);
                        fleeTarget = SteerAroundBlackHoles(pos, fleeTarget);
                        if (avoidEnemyZoc)
                            fleeTarget = ZoneOfControl.SteerAround(strength, pos, fleeTarget, zocAvoidStrength, null);

                        // 敵が近い間は後退移動（向き＝射界を保ち背中を見せない）、遠ければ通常移動で素早く離脱。
                        if (targetEnemy != null && useReverseRetreat
                            && Vector2.Distance(pos, enemyPos) <= reverseRetreatRange)
                            movement.SetReverseDestination(fleeTarget);
                        else
                            movement.SetDestination(fleeTarget);
                    }
                    break;
            }
        }

        /// <summary>
        /// 非戦闘艦の回避挙動（#128）。近い敵からのみ逃げ、脅威が無ければその場で待機する
        /// （任務移動はプレイヤー/専用Issueが指示）。撤退と同じ逃走（近ければ後退・遠ければ通常移動）を流用。
        /// </summary>
        private void UpdateNonCombatEvade(Vector2 pos)
        {
            if (targetEnemy == null) return; // 脅威なし＝待機

            float distToEnemy = Vector2.Distance(pos, targetEnemy.transform.position);
            if (distToEnemy > nonCombatEvadeRange) return; // 遠い敵は無視（無駄に逃げ回らない）

            Vector2 awayDir = ((Vector2)transform.position - (Vector2)targetEnemy.transform.position).normalized;
            Vector2 fleeTarget = SteerAroundBlackHoles(pos, pos + awayDir * 20f);
            if (avoidEnemyZoc)
                fleeTarget = ZoneOfControl.SteerAround(strength, pos, fleeTarget, zocAvoidStrength, null);

            if (useReverseRetreat && distToEnemy <= reverseRetreatRange)
                movement.SetReverseDestination(fleeTarget);
            else
                movement.SetDestination(fleeTarget);
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
