using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 回廊サボタージュの純データ（占領せず回廊を一時遮断する破壊工作の状態）。
    /// disruptionLevel=工作による遮断度（0..1・高いほど通れない）／detectionLevel=工作の発覚度（0..1）／
    /// repairProgress=守備側の復旧進捗（0..1・1で原状回復）。可変フィールド（Tick で書き換える）。
    /// </summary>
    public struct CorridorSabotageState
    {
        /// <summary>遮断度（0..1）＝工作で回廊がどれだけ通れなくなっているか。</summary>
        public float disruptionLevel;
        /// <summary>発覚度（0..1）＝工作が露見し誰の仕業か判明していく度合い。</summary>
        public float detectionLevel;
        /// <summary>復旧進捗（0..1）＝守備側の掃海・修復がどれだけ進んだか。</summary>
        public float repairProgress;

        public CorridorSabotageState(float disruptionLevel, float detectionLevel, float repairProgress)
        {
            this.disruptionLevel = Mathf.Clamp01(disruptionLevel);
            this.detectionLevel = Mathf.Clamp01(detectionLevel);
            this.repairProgress = Mathf.Clamp01(repairProgress);
        }
    }

    /// <summary>回廊サボタージュの調整係数（占領なしの一時遮断工作）。</summary>
    public readonly struct CorridorSabotageParams
    {
        /// <summary>工作の効果係数（技量×脆弱性に掛ける遮断度の伸び）。</summary>
        public readonly float sabotageGain;
        /// <summary>遮断度1のとき通行容量がどれだけ下がるか（最大遮断率）。</summary>
        public readonly float maxThroughputCut;
        /// <summary>復旧（掃海・修復）の速度（per dt・復旧能力1のとき遮断度を削る）。</summary>
        public readonly float repairRate;
        /// <summary>回廊が一時通行不能とみなす遮断度の閾値。</summary>
        public readonly float blockThreshold;

        public CorridorSabotageParams(float sabotageGain, float maxThroughputCut, float repairRate, float blockThreshold)
        {
            this.sabotageGain = Mathf.Max(0f, sabotageGain);
            this.maxThroughputCut = Mathf.Clamp01(maxThroughputCut);
            this.repairRate = Mathf.Max(0f, repairRate);
            this.blockThreshold = Mathf.Clamp01(blockThreshold);
        }

        /// <summary>既定＝工作効果1.0・最大遮断率0.9・復旧速度0.15・通行不能閾値0.6。</summary>
        public static CorridorSabotageParams Default => new CorridorSabotageParams(1f, 0.9f, 0.15f, 0.6f);
    }

    /// <summary>
    /// 回廊サボタージュの純ロジック（占領せず回廊を一時遮断する破壊工作・#1390 スペイン内戦）。
    /// 機雷敷設・航路標識破壊・中継施設の妨害で、敵の通行を「一時的に」止める＝時間が経つか掃討されれば復旧する。
    /// 物理的に占領・封鎖して面を支配する <see cref="BlockadeRules"/>（戦力比で通過率を決める面の封鎖）や、
    /// 敷設密度で損害を与える <see cref="MinefieldRules"/>（機雷原）とは別系統で、回廊（点）の一時遮断工作を扱う。
    /// 否認可能性は <see cref="GreyZoneRules"/>（グレーゾーン・否認可能）と、敵補給への波及は <see cref="SupplyRules"/>
    /// （補給線）と接続する。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CorridorSabotageRules
    {
        /// <summary>
        /// 工作の効果（0..1）＝工作員の技量 saboteurSkill ×回廊の脆弱性 corridorVulnerability。
        /// 守りの薄い（脆弱な）回廊ほど工作が効き、技量が高いほど深く刺さる。sabotageGain でスケール。
        /// </summary>
        public static float SabotageEffect(float saboteurSkill, float corridorVulnerability, CorridorSabotageParams p)
        {
            float skill = Mathf.Clamp01(saboteurSkill);
            float vuln = Mathf.Clamp01(corridorVulnerability);
            return Mathf.Clamp01(skill * vuln * p.sabotageGain);
        }

        public static float SabotageEffect(float saboteurSkill, float corridorVulnerability)
            => SabotageEffect(saboteurSkill, corridorVulnerability, CorridorSabotageParams.Default);

        /// <summary>
        /// 遮断度に応じた回廊の通行容量の低下（0..maxThroughputCut）。遮断度が高いほど通せなくなる
        /// ＝通過率に (1−本値) を掛けて使う。占領せず「一時的に」通行容量を削るのが面の封鎖との違い。
        /// </summary>
        public static float ThroughputReduction(float disruptionLevel, CorridorSabotageParams p)
        {
            return Mathf.Clamp01(disruptionLevel) * p.maxThroughputCut;
        }

        public static float ThroughputReduction(float disruptionLevel)
            => ThroughputReduction(disruptionLevel, CorridorSabotageParams.Default);

        /// <summary>
        /// 工作と復旧のせめぎ合いで遮断度を1tick進める（0..1）。工作努力 sabotageEffort(0..1) で上がり、
        /// 復旧努力 repairEffort(0..1) で下がる＝同時に走れば綱引き。時間が経てば（復旧が勝てば）通行が戻る。
        /// </summary>
        public static float DisruptionTick(float disruptionLevel, float sabotageEffort, float repairEffort, float dt, CorridorSabotageParams p)
        {
            float d = Mathf.Max(0f, dt);
            float next = Mathf.Clamp01(disruptionLevel)
                       + p.sabotageGain * Mathf.Clamp01(sabotageEffort) * d
                       - p.repairRate * Mathf.Clamp01(repairEffort) * d;
            return Mathf.Clamp01(next);
        }

        public static float DisruptionTick(float disruptionLevel, float sabotageEffort, float repairEffort, float dt)
            => DisruptionTick(disruptionLevel, sabotageEffort, repairEffort, dt, CorridorSabotageParams.Default);

        /// <summary>
        /// 守備側の復旧進捗を1tick進める（0..1）。復旧能力 repairCapacity(0..1)×復旧速度×dt で進む
        /// ＝掃海・標識修復・施設復旧で時間とともに通行が戻る。1 に達すれば原状回復。
        /// </summary>
        public static float RepairTick(float repairProgress, float repairCapacity, float dt, CorridorSabotageParams p)
        {
            float d = Mathf.Max(0f, dt);
            float next = Mathf.Clamp01(repairProgress) + p.repairRate * Mathf.Clamp01(repairCapacity) * d;
            return Mathf.Clamp01(next);
        }

        public static float RepairTick(float repairProgress, float repairCapacity, float dt)
            => RepairTick(repairProgress, repairCapacity, dt, CorridorSabotageParams.Default);

        /// <summary>
        /// 工作が発覚するリスク（0..1）＝工作員の露出 saboteurExposure ×哨戒密度 patrolDensity。
        /// 哨戒が密なほど工作員が捕まりやすく、露出の高い手荒な工作ほど足がつく。
        /// </summary>
        public static float DetectionRisk(float saboteurExposure, float patrolDensity)
        {
            return Mathf.Clamp01(Mathf.Clamp01(saboteurExposure) * Mathf.Clamp01(patrolDensity));
        }

        /// <summary>
        /// 否認可能なまま敵の通行を妨げる実効度（0..1）。遮断度が高いほど妨害が効くが、attribution
        /// （帰属＝誰の仕業か判明した度合い 0..1）が上がるほど否認可能性が失われ、政治的に使いづらくなる。
        /// 占領せず旗を立てない＝<see cref="GreyZoneRules"/> と接続する点の妨害。
        /// </summary>
        public static float DeniableInterdiction(float disruptionLevel, float attribution, CorridorSabotageParams p)
        {
            float disrupt = ThroughputReduction(disruptionLevel, p);
            float deniability = 1f - Mathf.Clamp01(attribution);
            return Mathf.Clamp01(disrupt * deniability);
        }

        public static float DeniableInterdiction(float disruptionLevel, float attribution)
            => DeniableInterdiction(disruptionLevel, attribution, CorridorSabotageParams.Default);

        /// <summary>
        /// 回廊遮断が敵の補給・移動に強いる遅延（0..1）。通行容量の低下 throughputReduction ×敵の当該回廊への
        /// 依存 enemyReliance(0..1)。敵がその回廊に頼るほど迂回を強いられ遅延が大きい＝戦わずに足を引っ張る。
        /// </summary>
        public static float SupplyDelayImposed(float throughputReduction, float enemyReliance)
        {
            return Mathf.Clamp01(Mathf.Clamp01(throughputReduction) * Mathf.Clamp01(enemyReliance));
        }

        /// <summary>
        /// 回廊が工作で一時的に通行不能になったか＝遮断度が閾値を超える。占領・封鎖（面の支配）ではなく
        /// 工作（点の妨害）による一時遮断＝復旧すれば再び通れる。
        /// </summary>
        public static bool IsCorridorBlocked(float disruptionLevel, float threshold)
        {
            return Mathf.Clamp01(disruptionLevel) > Mathf.Clamp01(threshold);
        }

        public static bool IsCorridorBlocked(float disruptionLevel)
            => IsCorridorBlocked(disruptionLevel, CorridorSabotageParams.Default.blockThreshold);
    }
}
