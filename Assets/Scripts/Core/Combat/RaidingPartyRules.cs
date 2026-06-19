using UnityEngine;

namespace Ginei
{
    /// <summary>襲撃隊（快速部隊の一撃離脱襲撃）の調整係数。</summary>
    public readonly struct RaidingPartyParams
    {
        /// <summary>速度1あたりの到達深度。</summary>
        public readonly float reachPerSpeed;
        /// <summary>継戦力が到達深度に寄与する重み。</summary>
        public readonly float enduranceWeight;
        /// <summary>到達深度1あたりの帰還リスク係数。</summary>
        public readonly float riskPerDepth;
        /// <summary>後方守備に縛り付ける戦力のスケール。</summary>
        public readonly float tyingDownScale;
        /// <summary>一撃離脱の成功とみなす既定閾値（0..1）。</summary>
        public readonly float successThreshold;

        public RaidingPartyParams(float reachPerSpeed, float enduranceWeight, float riskPerDepth,
            float tyingDownScale, float successThreshold)
        {
            this.reachPerSpeed = Mathf.Max(0f, reachPerSpeed);
            this.enduranceWeight = Mathf.Max(0f, enduranceWeight);
            this.riskPerDepth = Mathf.Max(0f, riskPerDepth);
            this.tyingDownScale = Mathf.Max(0f, tyingDownScale);
            this.successThreshold = Mathf.Clamp01(successThreshold);
        }

        /// <summary>既定＝到達1.0/継戦重み0.5/深度リスク0.02/縛り0.5/成功閾値0.5。</summary>
        public static RaidingPartyParams Default => new RaidingPartyParams(1f, 0.5f, 0.02f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 襲撃隊（快速部隊で敵後方を荒らす一撃離脱）の純ロジック。小規模で速い部隊が補給拠点・通信網・
    /// 無防備な軍事目標を叩き、敵が反応する前に離脱する（ヒット・アンド・アウェイ）。深く入るほど戦果は
    /// 大きいが帰還が難しい＝到達深度と帰還リスクのトレードオフ。襲撃の脅威そのものが敵を後方守備へ
    /// 縛り付ける（戦力分散の強要）。
    /// <para>分担：<see cref="CommerceRaidingRules"/>（通商破壊・補給線そのものの攻撃）とは別＝軍事拠点への
    /// 一撃離脱襲撃。DeepStrike（縦深打撃＝大規模な突破）とは別＝小規模快速による撹乱。</para>
    /// 盤面非依存の plain 引数・乱数なし（決定論）・入力クランプ・実効値パターン。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RaidingPartyRules
    {
        /// <summary>
        /// 襲撃隊が到達できる敵後方の深さ＝(速度×到達係数)×(1＋継戦力×継戦重み)。
        /// 速いほど・継戦できるほど深く入れる。負入力は0クランプ。
        /// </summary>
        public static float RaidReach(float raiderSpeed, float raiderEndurance, RaidingPartyParams p)
        {
            float speed = Mathf.Max(0f, raiderSpeed);
            float endurance = Mathf.Max(0f, raiderEndurance);
            return speed * p.reachPerSpeed * (1f + endurance * p.enduranceWeight);
        }

        public static float RaidReach(float raiderSpeed, float raiderEndurance)
            => RaidReach(raiderSpeed, raiderEndurance, RaidingPartyParams.Default);

        /// <summary>
        /// 襲撃目標への打撃＝襲撃戦力×(1−防御による軽減)。防御は targetDefense/200 を上限0.9でクランプ
        /// （無防備な目標は満額、要塞化された目標でも最低1割は通る）。
        /// </summary>
        public static float TargetDamage(float raiderStrength, float targetDefense)
        {
            float strength = Mathf.Max(0f, raiderStrength);
            float mitigation = Mathf.Clamp(Mathf.Max(0f, targetDefense) / 200f, 0f, 0.9f);
            return strength * (1f - mitigation);
        }

        /// <summary>
        /// 叩いて離脱する成功度（0..1）＝自速度/(自速度＋敵反応速度)。敵より速いほど1へ近づき、
        /// 敵の反応が速いほど0へ。両者0なら逃げる必要なし＝1（捕捉者なし）。
        /// </summary>
        public static float HitAndRunSuccess(float raiderSpeed, float enemyReactionSpeed)
        {
            float speed = Mathf.Max(0f, raiderSpeed);
            float reaction = Mathf.Max(0f, enemyReactionSpeed);
            float total = speed + reaction;
            if (total <= 0f) return 1f; // 誰も追ってこない
            return Mathf.Clamp01(speed / total);
        }

        /// <summary>
        /// 後方撹乱の価値＝目標打撃×(1＋目標重要度)。重要度(0..1)が高い拠点ほど混乱が大きく波及する。
        /// </summary>
        public static float DisruptionValue(float targetDamage, float targetImportance)
        {
            float damage = Mathf.Max(0f, targetDamage);
            float importance = Mathf.Clamp01(targetImportance);
            return damage * (1f + importance);
        }

        /// <summary>
        /// 深く入った襲撃隊の帰還リスク（0..1）＝到達深度×深度リスク係数×(1＋敵追撃力)。
        /// 深入り＋追撃が激しいほど帰れない。
        /// </summary>
        public static float ExtractionRisk(float raidReach, float enemyPursuit, RaidingPartyParams p)
        {
            float reach = Mathf.Max(0f, raidReach);
            float pursuit = Mathf.Max(0f, enemyPursuit);
            return Mathf.Clamp01(reach * p.riskPerDepth * (1f + pursuit));
        }

        public static float ExtractionRisk(float raidReach, float enemyPursuit)
            => ExtractionRisk(raidReach, enemyPursuit, RaidingPartyParams.Default);

        /// <summary>
        /// 襲撃の脅威で敵が後方守備に割く戦力＝後方守備規模×脅威度(0..1)×縛りスケール。
        /// 襲撃を恐れるほど前線から戦力が抜ける（戦力分散の強要）。
        /// </summary>
        public static float TyingDownEffect(float raidThreat, float enemyRearGarrison, RaidingPartyParams p)
        {
            float threat = Mathf.Clamp01(raidThreat);
            float garrison = Mathf.Max(0f, enemyRearGarrison);
            return garrison * threat * p.tyingDownScale;
        }

        public static float TyingDownEffect(float raidThreat, float enemyRearGarrison)
            => TyingDownEffect(raidThreat, enemyRearGarrison, RaidingPartyParams.Default);

        /// <summary>
        /// 襲撃の正味価値＝撹乱価値×(1−帰還リスク)。帰れない襲撃は戦果を持ち帰れない＝価値が目減りする。
        /// </summary>
        public static float RaidNetValue(float disruptionValue, float extractionRisk)
        {
            float value = Mathf.Max(0f, disruptionValue);
            float risk = Mathf.Clamp01(extractionRisk);
            return value * (1f - risk);
        }

        /// <summary>一撃離脱に成功したか＝成功度が閾値以上なら true。</summary>
        public static bool IsRaidSuccessful(float hitAndRunSuccess, float threshold)
        {
            return Mathf.Clamp01(hitAndRunSuccess) >= Mathf.Clamp01(threshold);
        }

        public static bool IsRaidSuccessful(float hitAndRunSuccess)
            => IsRaidSuccessful(hitAndRunSuccess, RaidingPartyParams.Default.successThreshold);
    }
}
