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

        [Header("旗艦の取り囲み参加 #旗艦参加")]
        [Tooltip("交戦中、旗艦も配下艦の取り囲みに少し後ろから加わる（射程の一定割合まで前進して射界維持）")]
        public bool joinEncirclement = true;
        [Tooltip("旗艦が前進して保つ間合い＝実効射程に対する割合（1.0=最大射程で待機／小さいほど前へ。少し後ろ＝0.7）")]
        [Range(0.3f, 1f)] public float flagshipEngageRangeRatio = 0.7f;

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
        public float battlefieldRadius = 135f;

        [Tooltip("戦況（兵力比・敗走）に応じて有利な陣形へ自動切替する（AI艦隊のみ）")]
        public bool autoFormation = true;

        [Tooltip("プレイヤーの隷下艦隊か（true＝選択・手動指示の対象。AIは標準で動き、手動指示で上書きする）")]
        public bool playerCommanded;

        [Header("軍団の持ち場（#持ち場・隷下は安易に離れない）")]
        [Tooltip("軍団隷下の艦隊が持ち場（軍団長旗艦の位置）から離れてよい最大距離。これを超えて敵を深追いしない。0=無制限（従来動作）")]
        public float corpsLeashRange = 35f;

        [Header("隊列整流（前方の味方を追い越さない・#隊列整流）")]
        [Tooltip("前進時、前方にいる味方艦隊を追い越さないよう移動先の前進成分を手前で止める")]
        public bool avoidOvertake = true;
        [Tooltip("前方の味方の手前で確保する間隔（占有半径とこの値の大きい方を使う）")]
        public float overtakeKeepBehind = 4f;

        // 軍団指揮（BattlefieldCommandManager が毎間隔で設定・解除）。隷下は陣形を軍団長に委ね、持ち場（アンカー）から深追いしない。
        /// <summary>軍団長の指揮下にあり陣形を自己判断で切り替えない（軍団長が主導）。</summary>
        [System.NonSerialized] public bool corpsControlled;
        /// <summary>持ち場のアンカー（軍団長旗艦の位置）が有効か。軍団旗艦自身は false（隊を率いるため拘束しない）。</summary>
        [System.NonSerialized] public bool hasCorpsAnchor;
        /// <summary>持ち場のアンカー座標（軍団長旗艦の位置）。<see cref="hasCorpsAnchor"/> が true のとき有効。</summary>
        [System.NonSerialized] public Vector2 corpsAnchor;

        // 軍団陣形スロット（#軍団集結）：会戦開始時や非接敵時、隷下は敵を独走せず軍団長基準のスロットに就いて「軍団ごとにまとまる」。
        /// <summary>軍団陣形スロットが割り当てられているか（隷下のみ true・軍団長旗艦は false）。</summary>
        [System.NonSerialized] public bool hasCorpsSlot;
        /// <summary>スロットの基準＝軍団長旗艦（移動に追従する）。</summary>
        [System.NonSerialized] public Transform corpsCommanderTf;
        /// <summary>軍団長基準のスロット局所座標（回転前・軍団長スロットを原点とする）。</summary>
        [System.NonSerialized] public Vector2 corpsSlotLocal;
        /// <summary>軍団の正面（度・+Y 基準）。スロットの回転と正対方向に使う。</summary>
        [System.NonSerialized] public float corpsFacingDeg;
        /// <summary>軍団長専用：軍団が隊形を整えるまで前進を保留する（集結優先・#軍団集結）。隷下には使わない。
        /// 整い次第／敵接近で <see cref="BattlefieldCommandManager"/> が解除し、軍団ごと前進・交戦へ移る。</summary>
        [System.NonSerialized] public bool corpsHold;

        // ── 会戦フロー②回り込み（包囲・#戦闘ドクトリン Stage3）：軍団長が後衛に与える側面回り込み命令。
        // 数値判断は CorpsBattleFlowRules（純ロジック）が担い、ここは命令の受け皿（FleetAI が移動で消費）。
        /// <summary>後背への回り込み（包囲）命令を受けているか（軍団長が後衛へ付与）。true の間は通常スロットでなく <see cref="flankTarget"/> へ広く回る。</summary>
        [System.NonSerialized] public bool enveloping;
        /// <summary>回り込み目標（敵軍団の側面・後背の広い点）。<see cref="enveloping"/> が true のとき有効。BattlefieldCommandManager が毎間隔更新。</summary>
        [System.NonSerialized] public Vector2 flankTarget;

        // ── 会戦フロー②カウンター（横腹を脅かされた側の遮蔽・#戦闘ドクトリン Stage3）。
        /// <summary>味方軍団の横腹を突く敵へ後衛を回して遮蔽するカウンター命令を受けているか。</summary>
        [System.NonSerialized] public bool counterScreening;
        /// <summary>カウンター遮蔽の目標（脅威と軍団の間へ詰める点）。<see cref="counterScreening"/> が true のとき有効。</summary>
        [System.NonSerialized] public Vector2 counterScreenTarget;

        // ── 会戦フロー③決戦の投入（commit・#戦闘ドクトリン Stage3）。
        /// <summary>軍団長が決戦を仕掛けたか＝遠距離砲撃をやめ全軍前進で間合いを詰める（BattlefieldCommandManager が設定）。</summary>
        [System.NonSerialized] public bool decisiveCommit;

        private bool manualOverride;
        /// <summary>手動指示で AI 操舵を一時上書き中か（指示完了で自動的に AI へ復帰）。</summary>
        public bool ManualOverride => manualOverride;

        private FleetMovement movement;
        private FleetWeapon weapon;
        private FleetStrength strength;
        private WeaponArc weaponArc;
        private FleetStandardOrder standardOrder;

        private float nextSearchTime;
        private FleetStrength targetEnemy;
        private FleetMorale moraleComponent;
        private Squadron squadron;

        /// <summary>手動指示でAI操舵を上書きする（プレイヤーが移動/攻撃/保持を発令したときに呼ぶ）。</summary>
        public void BeginManualOverride()
        {
            manualOverride = true;
            // FleetStandardOrder は命令時に動的 AddComponent されるため参照を取り直す
            if (standardOrder == null) standardOrder = GetComponent<FleetStandardOrder>();
        }
        /// <summary>手動上書きを解除してAI操舵へ戻す。</summary>
        public void EndManualOverride() { manualOverride = false; }

        private void Awake()
        {
            movement = GetComponent<FleetMovement>();
            weapon = GetComponent<FleetWeapon>();
            strength = GetComponent<FleetStrength>();
            weaponArc = GetComponent<WeaponArc>();
            moraleComponent = GetComponent<FleetMorale>();
            squadron = GetComponent<Squadron>();
            standardOrder = GetComponent<FleetStandardOrder>();
        }

        private void Update()
        {
            // 既に戦場から離脱（恒久退却）した艦は何もしない。
            if (strength != null && !strength.IsAlive) return;

            // 手動指示の上書き：プレイヤーの命令が生きている間は AI 操舵を譲る（基本AI＋手動で上書き）。
            // 命令が完了（移動停止＝到達 かつ 手動標的なし かつ 標準命令なし）したら自動的に AI へ復帰する。
            if (manualOverride)
            {
                bool busy = (movement != null && movement.IsMoving)
                    || (weapon != null && weapon.HasManualTarget)
                    || (standardOrder != null && standardOrder.stance != FleetStandardOrder.Stance.なし);
                if (busy) return;          // 手動操作を優先（AIは口を出さない）
                manualOverride = false;    // 指示完了＝AIへ復帰
            }

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
            // 軍団指揮下（#持ち場）は陣形を軍団長が主導する（BattlefieldCommandManager が放送）＝隷下は自己判断で切り替えない。
            if (corpsControlled) return;
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
            // 陣形変更は指揮スキルポイントを消費（#陣形コスト）＝AIも多用できない（戦闘中は特に重い）。窓口は Squadron に集約。
            if (squadron.currentFormation != rec) squadron.TryChangeFormation(rec);
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
            {
                if (ActiveCommandState.Issue(strength, cmd) && cmd == ActiveCommand.突撃)
                    TryInflictChargeConfusion(); // 正面同士の突撃なら相手を混乱させる（#突撃）
            }
        }

        /// <summary>
        /// 突撃の発令に成功したとき、正面同士（互いに正対）かつ突撃間合いなら標的を混乱させる（#突撃）。
        /// 突撃側は既存の自己バフ（攻撃/速度↑）を得て、受けた側は <see cref="ConfusionState"/> で混乱（被ダメ↑・与ダメ↓・機動↓）。
        /// 仕掛けるかの判断は <see cref="BattleAiRules.TryChooseCommand"/>＝艦隊指揮官（軍団長能力を反映した AiSkill）による。
        /// </summary>
        private void TryInflictChargeConfusion()
        {
            if (targetEnemy == null || !targetEnemy.IsAlive) return;
            Vector2 selfPos = transform.position;
            Vector2 enemyPos = targetEnemy.transform.position;
            if (!ChargeRules.InChargeRange(selfPos, enemyPos)) return;
            if (!ChargeRules.IsHeadOn(selfPos, transform.up, enemyPos, targetEnemy.transform.up)) return;
            ConfusionState.Inflict(targetEnemy);
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
                    // ── 会戦フロー②（#戦闘ドクトリン Stage3）：軍団長の命令で側面回り込み／カウンター遮蔽を最優先する。
                    // 通常の接近・スロット集結より先に評価し、後衛が広く側背面へ回る／脅威を遮蔽する軌道を取る
                    // （持ち場・ブラックホール・ZOC・追い越し制限は尊重）。
                    if (TryEnvelopOrCounter(pos)) break;

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

                        // 軍団長は軍団が隊形を整えるまで前進を待つ（#軍団集結）。敵方向へ正対して待機し、
                        // 整い次第／敵接近で BattlefieldCommandManager が corpsHold を解除＝軍団ごと前進・交戦へ移る。
                        if (corpsHold)
                        {
                            movement.FaceTarget(targetEnemy.transform.position);
                            break;
                        }

                        // 軍団集結（#軍団集結）：敵が軍団（持ち場）から遠い間は独走せず、軍団長基準のスロットに就いて
                        // 軍団としてまとまって前進する（軍団長が前を率い、隷下は隊形を保って追従）。敵が持ち場へ迫れば交戦へ移る。
                        if (hasCorpsSlot && corpsCommanderTf != null && !EnemyNearCorps())
                        {
                            movement.SetDestination(CorpsSlotWorld(), corpsFacingDeg);
                            break;
                        }

                        // 敵に向かって移動（進路上のブラックホールは迂回）。
                        // 進路上の「交戦対象以外」の敵ZOCは避ける（対象のZOCは意図して踏み込むので無視）。
                        Vector2 dest = SteerAroundBlackHoles(pos, targetEnemy.transform.position);
                        if (avoidEnemyZoc)
                            dest = ZoneOfControl.SteerAround(strength, pos, dest, zocAvoidStrength, targetEnemy);
                        dest = ApplyCorpsLeash(dest); // 軍団隷下は持ち場から深追いしない（#持ち場）
                        dest = ApplyOvertakeLimit(dest); // 前方の味方を追い越さない（#隊列整流）
                        movement.SetDestination(dest);
                    }
                    break;

                case AIState.交戦:
                    // ── 会戦フロー②（#戦闘ドクトリン Stage3）：交戦中でも回り込み命令中は射程保持でなく側面へ回り続ける。
                    if (TryEnvelopOrCounter(transform.position)) break;

                    if (!weaponArc.IsInArc(targetEnemy.transform))
                    {
                        // 射程外に逃げられたら再び接近
                        currentState = AIState.接近;
                    }
                    else
                    {
                        // ── 基本＝遠距離砲撃（#突撃）：preferredBand（既定=遠）の理想間合いを保って撃ち合う。──
                        // 突撃中（速度バフ＝指揮官が突撃を決めた）／軍団の決戦投入中（③ commit）は間合いを詰めて押し込む。
                        // それ以外は近すぎれば射界を保って下がり、遠ければ理想間合いまで寄り、適正なら停止して砲撃する。
                        Vector2 pos2d = transform.position;
                        Vector2 enemyPos2d = targetEnemy.transform.position;
                        float dist = Vector2.Distance(pos2d, enemyPos2d);
                        // 突撃発令中、または軍団長が決戦を仕掛けた（③ decisiveCommit）なら距離を詰める。
                        bool charging = (strength != null && strength.activeSpeedFactor > 1.05f) || decisiveCommit;

                        if (charging)
                        {
                            // 突撃：間合いを詰めて点射界へ踏み込む（持ち場内・前方の味方は追い越さない）。
                            Vector2 cdest = ApplyOvertakeLimit(ApplyCorpsLeash(enemyPos2d));
                            movement.SetDestination(cdest);
                        }
                        else if (weaponArc != null)
                        {
                            float idealRange = RangeBandRules.IdealRange(weaponArc.preferredBand, weaponArc.range);
                            int direction = RangeBandRules.ApproachOrWithdraw(dist, idealRange, keetingDeadzone);
                            if (direction > 0)
                            {
                                // 遠すぎ→理想間合いまで寄る（突っ込みすぎない・持ち場/追い越し制限）。
                                Vector2 adest = ApplyOvertakeLimit(ApplyCorpsLeash(enemyPos2d));
                                movement.SetDestination(adest);
                            }
                            else if (direction < 0)
                            {
                                // 近すぎ→射界を保って遠距離砲撃の間合いへ後退。
                                Vector2 awayDir = (pos2d - enemyPos2d).normalized;
                                movement.SetReverseDestination(pos2d + awayDir * (idealRange - dist + keetingDeadzone));
                            }
                            else
                            {
                                movement.FaceTarget(enemyPos2d); // 適正間合い＝射界維持で砲撃
                            }
                        }
                        else
                        {
                            movement.FaceTarget(enemyPos2d);
                        }
                    }
                    break;

                case AIState.撤退:
                    {
                        // 戦場中心（会戦ごとの遠方オフセット追従・フルスクリーンは原点）を基準に離脱端を判定する。
                        Vector2 center = BattleField.OriginFor(gameObject.scene);
                        // 敵不明（敗走で目標を見失う等）なら中心と反対＝外周方向を「自勢力端」とみなして目指す（#会戦改善 #3）。
                        Vector2 enemyPos = targetEnemy != null ? (Vector2)targetEnemy.transform.position : center;

                        // 自勢力側の画面端に到達したら戦場から離脱（恒久退却＝終了処理を締める #会戦改善 #1/#2）。
                        if (BattleWithdrawalRules.IsAtWithdrawalEdge(pos - center, enemyPos - center, battlefieldRadius))
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
        /// 軍団の持ち場（#持ち場）：移動目標が軍団長旗艦（アンカー）から <see cref="corpsLeashRange"/> を超える場合、
        /// アンカーからその距離の境界へ引き戻す＝隷下艦隊が敵を深追いして持ち場を離れないようにする。
        /// アンカー未設定（単独・軍団旗艦自身・プレイヤー指揮）では素通し（従来動作）。撤退状態には適用しない。
        /// </summary>
        private Vector2 ApplyCorpsLeash(Vector2 dest)
        {
            if (!hasCorpsAnchor || corpsLeashRange <= 0f) return dest;
            Vector2 off = dest - corpsAnchor;
            float r = corpsLeashRange;
            if (off.sqrMagnitude > r * r) return corpsAnchor + off.normalized * r;
            return dest;
        }

        private readonly List<Vector2> overtakeBuffer = new List<Vector2>();

        /// <summary>
        /// 前方の味方を追い越さないよう移動先を制限する（#隊列整流）。前進方向＝現在の目標（敵）方向。
        /// 前方にいる味方の手前（占有半径 or overtakeKeepBehind の大きい方）で前進成分を止める＝横移動（回り込み）は妨げない。
        /// </summary>
        private Vector2 ApplyOvertakeLimit(Vector2 dest)
        {
            if (!avoidOvertake || strength == null || targetEnemy == null) return dest;
            Vector2 pos = transform.position;
            Vector2 fwd = (Vector2)targetEnemy.transform.position - pos;
            if (fwd.sqrMagnitude < 1e-4f) return dest;

            overtakeBuffer.Clear();
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            if (flagships == null) return dest;
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength fs = flagships[i];
                if (fs == null || fs == strength || !fs.IsAlive) continue;
                if (FactionRelations.IsHostile(strength, fs)) continue; // 味方のみ
                overtakeBuffer.Add((Vector2)fs.transform.position);
            }

            float keep = overtakeKeepBehind;
            if (squadron != null) keep = Mathf.Max(keep, squadron.FootprintRadius());
            return FleetSpacingRules.LimitOvertake(pos, dest, fwd, overtakeBuffer, keep);
        }

        /// <summary>
        /// 会戦フロー②（#戦闘ドクトリン Stage3）：軍団長から付与された回り込み命令（<see cref="enveloping"/>）か
        /// カウンター遮蔽命令（<see cref="counterScreening"/>）があれば、その目標へ移動して true を返す。
        /// 回り込みは敵の側背面への広い軌道、遮蔽は脅威と軍団の間へ詰める軌道。どちらも持ち場（leash）・
        /// ブラックホール・追い越し制限を尊重する（横移動＝回り込みなので avoidOvertake は前進成分のみ抑制）。
        /// 命令が無ければ false（呼び側は通常の接近/交戦へ）。撤退中・手動中は呼ばれない（上流でガード済み）。
        /// </summary>
        private bool TryEnvelopOrCounter(Vector2 pos)
        {
            // カウンター遮蔽を優先（横腹を突かれる脅威への対処は喫緊）。
            if (counterScreening)
            {
                Vector2 dest = SteerAroundBlackHoles(pos, counterScreenTarget);
                dest = ApplyCorpsLeash(dest);
                movement.SetDestination(dest);
                return true;
            }
            if (enveloping)
            {
                // 敵の正面火力を避けつつ側背面へ抜ける広い軌道。回り込み中は深追い抑制を少し緩めるため
                // leash はかけるが追い越し制限は外す（横移動が主＝前方の味方を追い越す概念が薄い）。
                Vector2 dest = SteerAroundBlackHoles(pos, flankTarget);
                if (avoidEnemyZoc)
                    dest = ZoneOfControl.SteerAround(strength, pos, dest, zocAvoidStrength, targetEnemy);
                dest = ApplyCorpsLeash(dest);
                movement.SetDestination(dest);
                return true;
            }
            return false;
        }

        /// <summary>軍団スロットの世界座標＝軍団長旗艦の現在位置＋軍団正面に回した局所スロット（軍団長の移動に追従）。</summary>
        private Vector2 CorpsSlotWorld()
        {
            Vector2 baseP = corpsCommanderTf != null ? (Vector2)corpsCommanderTf.position : corpsAnchor;
            return baseP + RotateVec(corpsSlotLocal, corpsFacingDeg);
        }

        /// <summary>敵（現在の目標）が軍団の持ち場（軍団長旗艦）から交戦圏（corpsLeashRange）内にいるか。内なら交戦・外なら集結。</summary>
        private bool EnemyNearCorps()
        {
            if (targetEnemy == null || !targetEnemy.IsAlive) return false;
            if (corpsLeashRange <= 0f) return true; // リーシュ無効＝常に交戦（従来寄り）
            Vector2 anchor = corpsCommanderTf != null ? (Vector2)corpsCommanderTf.position
                : (hasCorpsAnchor ? corpsAnchor : (Vector2)transform.position);
            return ((Vector2)targetEnemy.transform.position - anchor).sqrMagnitude <= corpsLeashRange * corpsLeashRange;
        }

        /// <summary>ベクトルを Z 角(度・+Y 基準)で回す（CorpsFormation と同一規約）。</summary>
        private static Vector2 RotateVec(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
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
