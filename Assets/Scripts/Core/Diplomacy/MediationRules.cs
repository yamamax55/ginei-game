using UnityEngine;

namespace Ginei
{
    /// <summary>第三者調停の調整係数。</summary>
    public readonly struct MediationParams
    {
        /// <summary>中立性×威信に掛ける信頼度の尺度。</summary>
        public readonly float credibilityScale;
        /// <summary>当事者の窮状が薄くても残る受容の下限（窮状0でも信頼ぶんは受け入れる素地）。</summary>
        public readonly float desperationFloor;
        /// <summary>立場の隔たりが歩み寄り案の質を削る強さ（1+gap×これで割る）。</summary>
        public readonly float gapWeight;
        /// <summary>双方の疲弊が薄くても残る収束の下限。</summary>
        public readonly float exhaustionFloor;
        /// <summary>調停者の私利（漁夫の利）が公平を曲げる強さ。</summary>
        public readonly float selfInterestWeight;
        /// <summary>成否で私利が成立を引き下げる強さ（私利のペナルティ）。</summary>
        public readonly float selfInterestPenalty;
        /// <summary>調停が機能すると見なす成否の既定閾値。</summary>
        public readonly float effectiveThreshold;

        public MediationParams(float credibilityScale, float desperationFloor, float gapWeight,
            float exhaustionFloor, float selfInterestWeight, float selfInterestPenalty, float effectiveThreshold)
        {
            this.credibilityScale = Mathf.Clamp01(credibilityScale);
            this.desperationFloor = Mathf.Clamp01(desperationFloor);
            this.gapWeight = Mathf.Max(0f, gapWeight);
            this.exhaustionFloor = Mathf.Clamp01(exhaustionFloor);
            this.selfInterestWeight = Mathf.Clamp01(selfInterestWeight);
            this.selfInterestPenalty = Mathf.Clamp01(selfInterestPenalty);
            this.effectiveThreshold = Mathf.Clamp(effectiveThreshold, -1f, 1f);
        }

        /// <summary>既定＝信頼尺度1.0・受容下限0.3・隔たり重み1.0・疲弊下限0.3・私利重み1.0・私利ペナルティ1.0・機能閾値0.25。</summary>
        public static MediationParams Default => new MediationParams(1f, 0.3f, 1f, 0.3f, 1f, 1f, 0.25f);
    }

    /// <summary>
    /// 第三者調停の純ロジック（フェザーン型の中立勢力による仲裁）。調停者の中立性と威信が信頼を生み（MediatorCredibility）、
    /// 当事者の窮状と相まって調停が受け入れられ（Acceptability）、調停者の技量が立場の隔たりを埋める歩み寄り案を出し
    /// （BridgingProposal）、双方の疲弊と相まって両者が収束する（PartyConvergence）。一方で調停者は紛争長期化から私利
    /// （漁夫の利）を得る誘惑を抱え（MediatorSelfInterest）、公平を曲げる。調停者の力と履行保証は保証人としての実効を生む
    /// （GuarantorRole）。受容×収束から私利を差し引いて成否を出し（MediationSuccess・-1..1）、閾値で機能を判定する。
    /// 実効値パターン・決定論・入力clamp。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MediationRules
    {
        /// <summary>調停者の信頼度（0..1）＝中立性×威信×信頼尺度。中立か威信が欠ければ低い。</summary>
        public static float MediatorCredibility(float neutrality, float prestige, MediationParams p)
        {
            float n = Mathf.Clamp01(neutrality);
            float pr = Mathf.Clamp01(prestige);
            return Mathf.Clamp01(n * pr * p.credibilityScale);
        }

        public static float MediatorCredibility(float neutrality, float prestige)
            => MediatorCredibility(neutrality, prestige, MediationParams.Default);

        /// <summary>
        /// 調停受け入れの容易さ（0..1）＝調停者の信頼×当事者の窮状。窮状0でも desperationFloor ぶんは受け入れる素地が残る。
        /// </summary>
        public static float Acceptability(float mediatorCredibility, float partyDesperation, MediationParams p)
        {
            float cred = Mathf.Clamp01(mediatorCredibility);
            float desp = Mathf.Clamp01(partyDesperation);
            float willingness = p.desperationFloor + (1f - p.desperationFloor) * desp;
            return Mathf.Clamp01(cred * willingness);
        }

        public static float Acceptability(float mediatorCredibility, float partyDesperation)
            => Acceptability(mediatorCredibility, partyDesperation, MediationParams.Default);

        /// <summary>
        /// 歩み寄り案の質（0..1）＝調停者の技量÷(1+立場の隔たり×隔たり重み)。隔たりが大きいほど良案は出しづらい。
        /// </summary>
        public static float BridgingProposal(float mediatorSkill, float positionGap, MediationParams p)
        {
            float skill = Mathf.Clamp01(mediatorSkill);
            float gap = Mathf.Clamp01(positionGap);
            return Mathf.Clamp01(skill / (1f + gap * p.gapWeight));
        }

        public static float BridgingProposal(float mediatorSkill, float positionGap)
            => BridgingProposal(mediatorSkill, positionGap, MediationParams.Default);

        /// <summary>
        /// 両者の収束（0..1）＝歩み寄り案×双方の疲弊。疲弊0でも exhaustionFloor ぶんは案が効く下地が残る。
        /// </summary>
        public static float PartyConvergence(float bridgingProposal, float mutualExhaustion, MediationParams p)
        {
            float bridge = Mathf.Clamp01(bridgingProposal);
            float exh = Mathf.Clamp01(mutualExhaustion);
            float readiness = p.exhaustionFloor + (1f - p.exhaustionFloor) * exh;
            return Mathf.Clamp01(bridge * readiness);
        }

        public static float PartyConvergence(float bridgingProposal, float mutualExhaustion)
            => PartyConvergence(bridgingProposal, mutualExhaustion, MediationParams.Default);

        /// <summary>
        /// 漁夫の利の誘惑（0..1）＝調停者の利害×紛争長期化×私利重み。長引くほど中立を装い私利を貪る動機が増す。
        /// </summary>
        public static float MediatorSelfInterest(float mediatorStake, float conflictProlongation, MediationParams p)
        {
            float stake = Mathf.Clamp01(mediatorStake);
            float prolong = Mathf.Clamp01(conflictProlongation);
            return Mathf.Clamp01(stake * prolong * p.selfInterestWeight);
        }

        public static float MediatorSelfInterest(float mediatorStake, float conflictProlongation)
            => MediatorSelfInterest(mediatorStake, conflictProlongation, MediationParams.Default);

        /// <summary>
        /// 保証人としての実効（0..1）＝調停者の力×履行保証の意志。力があり履行を担保できるほど合意の保証人になれる。
        /// </summary>
        public static float GuarantorRole(float mediatorPower, float commitmentToEnforce, MediationParams p)
        {
            float power = Mathf.Clamp01(mediatorPower);
            float commit = Mathf.Clamp01(commitmentToEnforce);
            return Mathf.Clamp01(power * commit);
        }

        public static float GuarantorRole(float mediatorPower, float commitmentToEnforce)
            => GuarantorRole(mediatorPower, commitmentToEnforce, MediationParams.Default);

        /// <summary>
        /// 調停の成否（-1..1）＝受容×収束−私利×私利ペナルティ。受け入れられて両者が収束すれば正、私利が勝てば負。
        /// </summary>
        public static float MediationSuccess(float acceptability, float partyConvergence, float mediatorSelfInterest, MediationParams p)
        {
            float acc = Mathf.Clamp01(acceptability);
            float conv = Mathf.Clamp01(partyConvergence);
            float self = Mathf.Clamp01(mediatorSelfInterest);
            float success = acc * conv - self * p.selfInterestPenalty;
            return Mathf.Clamp(success, -1f, 1f);
        }

        public static float MediationSuccess(float acceptability, float partyConvergence, float mediatorSelfInterest)
            => MediationSuccess(acceptability, partyConvergence, mediatorSelfInterest, MediationParams.Default);

        /// <summary>調停が機能するか＝成否が閾値以上。</summary>
        public static bool IsMediationEffective(float mediationSuccess, float threshold)
        {
            return Mathf.Clamp(mediationSuccess, -1f, 1f) >= Mathf.Clamp(threshold, -1f, 1f);
        }

        /// <summary>既定閾値（MediationParams.Default.effectiveThreshold＝0.25）委譲版。</summary>
        public static bool IsMediationEffective(float mediationSuccess)
            => IsMediationEffective(mediationSuccess, MediationParams.Default.effectiveThreshold);
    }
}
