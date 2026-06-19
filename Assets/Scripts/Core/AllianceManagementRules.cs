using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 同盟管理の調整値（多国間軍事同盟の内部力学）。結束/ただ乗り/参戦/離脱の効きを束ねる。
    /// 全フィールドは ctor で Clamp 済み。<see cref="Default"/> が既定の重み。
    /// </summary>
    public readonly struct AllianceManagementParams
    {
        /// <summary>結束への共通脅威の寄与（脅威が結束を生む）。</summary>
        public readonly float threatWeight;
        /// <summary>結束への価値観共有の寄与。</summary>
        public readonly float valuesWeight;
        /// <summary>ただ乗りペナルティの強さ（応分=1.0からの不足を不満へ）。</summary>
        public readonly float freeRiderStrength;
        /// <summary>参戦意思における結束の寄与（残りは自国利害）。</summary>
        public readonly float commitmentCohesionWeight;
        /// <summary>主導力における国力比の寄与（残りは正統性）。</summary>
        public readonly float hegemonPowerWeight;
        /// <summary>離脱リスクの基準感度（結束低・不公平・外部誘いを増幅）。</summary>
        public readonly float defectionSensitivity;
        /// <summary>同盟実効戦力で結束が効く度合い（0＝結束無視/1＝結束に強依存）。</summary>
        public readonly float strengthCohesionInfluence;

        public AllianceManagementParams(
            float threatWeight,
            float valuesWeight,
            float freeRiderStrength,
            float commitmentCohesionWeight,
            float hegemonPowerWeight,
            float defectionSensitivity,
            float strengthCohesionInfluence)
        {
            this.threatWeight = Mathf.Clamp01(threatWeight);
            this.valuesWeight = Mathf.Clamp01(valuesWeight);
            this.freeRiderStrength = Mathf.Clamp01(freeRiderStrength);
            this.commitmentCohesionWeight = Mathf.Clamp01(commitmentCohesionWeight);
            this.hegemonPowerWeight = Mathf.Clamp01(hegemonPowerWeight);
            this.defectionSensitivity = Mathf.Clamp(defectionSensitivity, 0f, 2f);
            this.strengthCohesionInfluence = Mathf.Clamp01(strengthCohesionInfluence);
        }

        public const float DefaultThreatWeight = 0.6f;
        public const float DefaultValuesWeight = 0.4f;
        public const float DefaultFreeRiderStrength = 0.5f;
        public const float DefaultCommitmentCohesionWeight = 0.6f;
        public const float DefaultHegemonPowerWeight = 0.6f;
        public const float DefaultDefectionSensitivity = 1.0f;
        public const float DefaultStrengthCohesionInfluence = 0.5f;

        /// <summary>既定：脅威0.6/価値0.4、ただ乗り0.5、参戦結束0.6、盟主国力0.6、離脱感度1.0、戦力結束依存0.5。</summary>
        public static AllianceManagementParams Default => new AllianceManagementParams(
            DefaultThreatWeight,
            DefaultValuesWeight,
            DefaultFreeRiderStrength,
            DefaultCommitmentCohesionWeight,
            DefaultHegemonPowerWeight,
            DefaultDefectionSensitivity,
            DefaultStrengthCohesionInfluence);
    }

    /// <summary>
    /// 同盟管理＝結成済みの多国間軍事同盟ブロックの内部力学を数値化する純ロジック。
    /// 結束度・負担分担の公平さ・ただ乗り・有事の参戦意思・盟主の主導力・離脱リスク・同盟の実効戦力を扱う。
    /// 二国間の外交状態（宣戦/講和/同盟締結）は <see cref="DiplomacyRules"/> が担い、こちらは
    /// 「ひとつの同盟ブロックの内部」専門。決定論・実効値パターン（基準値非破壊）・LINQ不使用。test-first。
    /// </summary>
    public static class AllianceManagementRules
    {
        /// <summary>既定Paramsで同盟結束度を返す。</summary>
        public static float Cohesion(float commonThreat, float sharedValues)
            => Cohesion(commonThreat, sharedValues, AllianceManagementParams.Default);

        /// <summary>
        /// 共通の脅威（0..1）と価値観の共有（0..1）から同盟結束度（0..1）を返す。
        /// 重みは正規化＝`(threat*tw + values*vw)/(tw+vw)`。両重み0なら0.5（中立）。脅威が結束を生む。
        /// </summary>
        public static float Cohesion(float commonThreat, float sharedValues, AllianceManagementParams p)
        {
            float threat = Mathf.Clamp01(commonThreat);
            float values = Mathf.Clamp01(sharedValues);
            float wSum = p.threatWeight + p.valuesWeight;
            if (wSum <= 0f) return 0.5f;
            return Mathf.Clamp01((threat * p.threatWeight + values * p.valuesWeight) / wSum);
        }

        /// <summary>
        /// 拠出/国力 で負担分担率を返す。1.0で応分、&lt;1でただ乗り、&gt;1で過剰負担。
        /// 国力0なら0（測れない）。上限は2.0（過剰負担の暴走防止）。Params不要。
        /// </summary>
        public static float BurdenShare(float contribution, float capacity)
        {
            float contrib = Mathf.Max(0f, contribution);
            float cap = Mathf.Max(0f, capacity);
            if (cap <= 0f) return 0f;
            return Mathf.Clamp(contrib / cap, 0f, 2f);
        }

        /// <summary>既定Paramsでただ乗りペナルティを返す。</summary>
        public static float FreeRiderPenalty(float burdenShare)
            => FreeRiderPenalty(burdenShare, AllianceManagementParams.Default);

        /// <summary>
        /// 負担分担率が応分(1.0)を下回るほど他国の不満（0..1）を返す。`penalty = clamp01((1 - share) * strength)`。
        /// share&gt;=1（応分以上）は不満0。strength は不足の効き。
        /// </summary>
        public static float FreeRiderPenalty(float burdenShare, AllianceManagementParams p)
        {
            float share = Mathf.Clamp(burdenShare, 0f, 2f);
            float shortfall = Mathf.Max(0f, 1f - share);
            return Mathf.Clamp01(shortfall * p.freeRiderStrength);
        }

        /// <summary>既定Paramsで有事の参戦意思を返す。</summary>
        public static float CommitmentToWar(float cohesion, float selfInterest)
            => CommitmentToWar(cohesion, selfInterest, AllianceManagementParams.Default);

        /// <summary>
        /// 結束（0..1）と自国利害（0..1）から有事の参戦意思（0..1）を返す。
        /// `commit = cohesion*cw + selfInterest*(1-cw)`。結束は連帯、自国利害はそろばん勘定。
        /// </summary>
        public static float CommitmentToWar(float cohesion, float selfInterest, AllianceManagementParams p)
        {
            float coh = Mathf.Clamp01(cohesion);
            float self = Mathf.Clamp01(selfInterest);
            float cw = p.commitmentCohesionWeight;
            return Mathf.Clamp01(coh * cw + self * (1f - cw));
        }

        /// <summary>既定Paramsで盟主の主導力を返す。</summary>
        public static float HegemonLeadership(float powerShare, float legitimacy)
            => HegemonLeadership(powerShare, legitimacy, AllianceManagementParams.Default);

        /// <summary>
        /// 盟主の国力比（0..1＝同盟内シェア）と正統性（0..1）から主導力（0..1）を返す。
        /// `lead = powerShare*pw + legitimacy*(1-pw)`。力だけでも正統性だけでも主導は足りない。
        /// </summary>
        public static float HegemonLeadership(float powerShare, float legitimacy, AllianceManagementParams p)
        {
            float share = Mathf.Clamp01(powerShare);
            float legit = Mathf.Clamp01(legitimacy);
            float pw = p.hegemonPowerWeight;
            return Mathf.Clamp01(share * pw + legit * (1f - pw));
        }

        /// <summary>既定Paramsで離脱リスクを返す。</summary>
        public static float DefectionRisk(float cohesion, float burdenUnfairness, float externalTemptation)
            => DefectionRisk(cohesion, burdenUnfairness, externalTemptation, AllianceManagementParams.Default);

        /// <summary>
        /// 結束の低さ・負担の不公平・外部の誘いから離脱リスク（0..1）を返す。
        /// `pressure = ((1-cohesion) + unfairness + temptation)/3 * sensitivity`。結束が高いほどリスクは下がる。
        /// </summary>
        public static float DefectionRisk(float cohesion, float burdenUnfairness, float externalTemptation, AllianceManagementParams p)
        {
            float lowCoh = 1f - Mathf.Clamp01(cohesion);
            float unfair = Mathf.Clamp01(burdenUnfairness);
            float tempt = Mathf.Clamp01(externalTemptation);
            float pressure = (lowCoh + unfair + tempt) / 3f * p.defectionSensitivity;
            return Mathf.Clamp01(pressure);
        }

        /// <summary>既定Paramsで同盟の実効戦力を返す。</summary>
        public static float AllianceStrength(float memberPowerSum, float cohesion)
            => AllianceStrength(memberPowerSum, cohesion, AllianceManagementParams.Default);

        /// <summary>
        /// 総国力×結束で同盟の実効戦力を返す。結束が低いと烏合の衆で実効戦力が落ちる。
        /// 結束倍率 = `lerp(1-influence, 1, cohesion)`（influence=0で常に総国力／1で結束に完全比例）。
        /// </summary>
        public static float AllianceStrength(float memberPowerSum, float cohesion, AllianceManagementParams p)
        {
            float power = Mathf.Max(0f, memberPowerSum);
            float coh = Mathf.Clamp01(cohesion);
            float factor = Mathf.Lerp(1f - p.strengthCohesionInfluence, 1f, coh);
            return power * factor;
        }

        /// <summary>既定閾値(0.0)で同盟が持ちこたえるか判定する。</summary>
        public static bool WillHold(float cohesion, float defectionRisk)
            => WillHold(cohesion, defectionRisk, DefaultHoldThreshold);

        /// <summary>
        /// 同盟が持ちこたえるか（bool）。結束がリスク＋閾値を上回れば持つ＝`cohesion > defectionRisk + threshold`。
        /// 閾値を上げるほど「結束に余裕がないと崩れる」厳しい判定になる。
        /// </summary>
        public static bool WillHold(float cohesion, float defectionRisk, float threshold)
        {
            float coh = Mathf.Clamp01(cohesion);
            float risk = Mathf.Clamp01(defectionRisk);
            return coh > risk + threshold;
        }

        /// <summary>既定の持ちこたえ閾値（結束がリスクをわずかでも上回れば持つ）。</summary>
        public const float DefaultHoldThreshold = 0.0f;
    }
}
