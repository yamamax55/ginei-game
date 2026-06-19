using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 破壊工作（サボタージュ）の純ロジック。工作員が敵後方の造船所・補給庫・通信網・
    /// 要塞設備に潜入し、密かに破壊して戦わずに敵戦力を削ぐ。成功すれば大きな効果だが、
    /// 警備に阻まれ、失敗すると工作員を失い報復・警備強化を招く。
    ///
    /// 分担（重複実装しない）：
    /// - <see cref="CommerceRaidingRules"/>（艦隊による通商破壊＝船団狩り）とは別＝
    ///   こちらは工作員による潜入破壊（前線でなく敵後方の施設そのものを叩く）。
    /// - <see cref="InterdictionRules"/>（回廊の面的遮断）とは別＝
    ///   こちらは要所の流量制限でなく特定施設への直接破壊工作。
    /// - <see cref="EspionageRules"/>（諜報＝情報窃取が主目的）の破壊工作専門版＝
    ///   施設破壊・復旧遅延・報復までを解決する。
    /// 盤面非依存の plain 引数・乱数なし・実効値パターン（基準値非破壊）・値の clamp 徹底。test-first。
    /// </summary>
    public static class SabotageOpsRules
    {
        /// <summary>破壊工作の係数。トップレベル readonly struct・全 Clamp。</summary>
        public readonly struct SabotageOpsParams
        {
            /// <summary>工作員技量の潜入への利き（高いほど警備を抜けやすい）。</summary>
            public readonly float infiltrationScale;
            /// <summary>警備が緩くても残る発見リスクの下限係数（警戒度比例の下駄）。</summary>
            public readonly float securityFloor;
            /// <summary>破壊が運用混乱に波及する利き（重要施設ほど効く）。</summary>
            public readonly float disruptionScale;
            /// <summary>作戦中発見リスクの基準値（警戒度の効き）。</summary>
            public readonly float detectionBase;
            /// <summary>脱出計画による工作員喪失の軽減効率（0..1）。</summary>
            public readonly float extractionMitigation;
            /// <summary>破壊1単位あたりの基準復旧時間（修復能力で割る前の分子）。</summary>
            public readonly float repairScale;
            /// <summary>破壊規模が報復・警備強化を招く利き。</summary>
            public readonly float escalationScale;
            /// <summary>破壊工作が成功とみなされる打撃の閾値（0..1）。</summary>
            public readonly float successThreshold;

            public SabotageOpsParams(
                float infiltrationScale,
                float securityFloor,
                float disruptionScale,
                float detectionBase,
                float extractionMitigation,
                float repairScale,
                float escalationScale,
                float successThreshold)
            {
                this.infiltrationScale = Mathf.Max(0f, infiltrationScale);
                this.securityFloor = Mathf.Clamp01(securityFloor);
                this.disruptionScale = Mathf.Max(0f, disruptionScale);
                this.detectionBase = Mathf.Clamp01(detectionBase);
                this.extractionMitigation = Mathf.Clamp01(extractionMitigation);
                this.repairScale = Mathf.Max(0f, repairScale);
                this.escalationScale = Mathf.Max(0f, escalationScale);
                this.successThreshold = Mathf.Clamp01(successThreshold);
            }

            /// <summary>
            /// 既定＝潜入利き1.5・発見下限0.1・混乱利き1.2・発見基準0.5・脱出軽減0.8・
            /// 復旧基準12・報復利き0.6・成功閾値0.4。
            /// </summary>
            public static SabotageOpsParams Default => new SabotageOpsParams(
                infiltrationScale: 1.5f,
                securityFloor: 0.1f,
                disruptionScale: 1.2f,
                detectionBase: 0.5f,
                extractionMitigation: 0.8f,
                repairScale: 12f,
                escalationScale: 0.6f,
                successThreshold: 0.4f);
        }

        /// <summary>
        /// 標的への潜入成功度（0..1）。工作員技量（×利き）と標的警備の比＝
        /// 技量が高いほど抜けやすく、警備が固いほど阻まれる。
        /// </summary>
        public static float InfiltrationSuccess(float agentSkill, float targetSecurity, SabotageOpsParams p)
        {
            float s = Mathf.Clamp01(agentSkill) * p.infiltrationScale;
            float sec = Mathf.Clamp01(targetSecurity);
            float denom = s + sec;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(s / denom);
        }

        public static float InfiltrationSuccess(float agentSkill, float targetSecurity)
            => InfiltrationSuccess(agentSkill, targetSecurity, SabotageOpsParams.Default);

        /// <summary>
        /// 破壊の打撃（0..1＝施設への損害割合）。潜入成功度×標的の脆弱性×仕掛けた爆薬量（平方根で逓減）。
        /// 深く潜り込み、脆い箇所に多く仕掛けるほど大きく壊せる。
        /// </summary>
        public static float SabotageDamage(float infiltrationSuccess, float targetVulnerability, float charges)
        {
            float inf = Mathf.Clamp01(infiltrationSuccess);
            float vuln = Mathf.Clamp01(targetVulnerability);
            float c = Mathf.Max(0f, charges);
            return Mathf.Clamp01(inf * vuln * Mathf.Sqrt(c));
        }

        /// <summary>
        /// 敵運用に与える混乱（0..1）。破壊の打撃×施設の重要度×利き＝
        /// 同じ破壊でも重要施設ほど運用全体を麻痺させる。
        /// </summary>
        public static float OperationalDisruption(float sabotageDamage, float targetImportance, SabotageOpsParams p)
        {
            float d = Mathf.Clamp01(sabotageDamage);
            float imp = Mathf.Clamp01(targetImportance);
            return Mathf.Clamp01(d * imp * p.disruptionScale);
        }

        public static float OperationalDisruption(float sabotageDamage, float targetImportance)
            => OperationalDisruption(sabotageDamage, targetImportance, SabotageOpsParams.Default);

        /// <summary>
        /// 作戦中に発見されるリスク（0..1）。警備の警戒度が高く・技量が低いほど高い。
        /// 警戒度比例の下限（securityFloor）を必ず残す＝足音は完全には消せない。
        /// </summary>
        public static float DetectionDuringOp(float agentSkill, float securityAlertness, SabotageOpsParams p)
        {
            float s = Mathf.Clamp01(agentSkill);
            float a = Mathf.Clamp01(securityAlertness);
            float raw = p.detectionBase * a * (1f - s);
            float floor = p.securityFloor * a;
            return Mathf.Clamp01(Mathf.Max(raw, floor));
        }

        public static float DetectionDuringOp(float agentSkill, float securityAlertness)
            => DetectionDuringOp(agentSkill, securityAlertness, SabotageOpsParams.Default);

        /// <summary>
        /// 発見されて工作員を失う確率（0..1）。発見リスクに比例するが、
        /// 練られた脱出計画ぶんだけ軽減される（首尾よく逃げ延びる）。
        /// </summary>
        public static float AgentLoss(float detectionDuringOp, float extractionPlan, SabotageOpsParams p)
        {
            float det = Mathf.Clamp01(detectionDuringOp);
            float ext = Mathf.Clamp01(extractionPlan);
            return Mathf.Clamp01(det * (1f - ext * p.extractionMitigation));
        }

        public static float AgentLoss(float detectionDuringOp, float extractionPlan)
            => AgentLoss(detectionDuringOp, extractionPlan, SabotageOpsParams.Default);

        /// <summary>
        /// 破壊した施設の復旧遅延（時間）。破壊が大きいほど長く、敵の修復能力が高いほど短い。
        /// 修復能力で割って逓減（能力0でも repairScale 分は最低かかる）。
        /// </summary>
        public static float RepairDelay(float sabotageDamage, float enemyRepairCapacity, SabotageOpsParams p)
        {
            float d = Mathf.Clamp01(sabotageDamage);
            float cap = Mathf.Max(0f, enemyRepairCapacity);
            return d * p.repairScale / (1f + cap);
        }

        public static float RepairDelay(float sabotageDamage, float enemyRepairCapacity)
            => RepairDelay(sabotageDamage, enemyRepairCapacity, SabotageOpsParams.Default);

        /// <summary>
        /// 破壊工作が招く報復・警備強化の度合い（0..1）。破壊が大きいほど敵の反発が強い。
        /// 以後の作戦の警備（securityAlertness）上昇圧として消費する想定。
        /// </summary>
        public static float EscalationRetaliation(float sabotageDamage, SabotageOpsParams p)
        {
            float d = Mathf.Clamp01(sabotageDamage);
            return Mathf.Clamp01(d * p.escalationScale);
        }

        public static float EscalationRetaliation(float sabotageDamage)
            => EscalationRetaliation(sabotageDamage, SabotageOpsParams.Default);

        /// <summary>破壊工作が成功したか（打撃が閾値以上）。</summary>
        public static bool IsSabotageSuccessful(float sabotageDamage, float threshold)
            => Mathf.Clamp01(sabotageDamage) >= Mathf.Clamp01(threshold);

        /// <summary>既定の成功閾値での破壊工作成功判定。</summary>
        public static bool IsSabotageSuccessful(float sabotageDamage)
            => IsSabotageSuccessful(sabotageDamage, SabotageOpsParams.Default.successThreshold);
    }
}
