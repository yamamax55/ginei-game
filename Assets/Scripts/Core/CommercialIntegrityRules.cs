using UnityEngine;

namespace Ginei
{
    /// <summary>商業誠実性＝反復交易が育てる信頼の蓄積・崩壊・回復の調整係数。</summary>
    public readonly struct CommercialIntegrityParams
    {
        /// <summary>約束を守った取引が信頼を積む速さ（規模が大きいほど効く）。</summary>
        public readonly float buildRate;
        /// <summary>裏切りが信頼を崩す強さ（蓄積より速い＝崩すは一気・積むは徐々の非対称の核）。</summary>
        public readonly float breachSeverityScale;
        /// <summary>信頼が opinion を修正する最大幅（±これ＝中立0.5を基準に外交感情を上下）。</summary>
        public readonly float opinionSwing;
        /// <summary>裏切りの誘惑に対し蓄積信頼が抑止する効き（信頼が大きいほど失うものが大きく裏切りにくい）。</summary>
        public readonly float trustDeterrence;
        /// <summary>反復取引が取引コストを下げる最大割引（信頼の配当）。</summary>
        public readonly float repeatDiscount;
        /// <summary>裏切り後の信頼回復の遅さ（build より遥かに遅い＝徐々にしか戻らない）。</summary>
        public readonly float recoveryRate;
        /// <summary>信頼できる取引相手とみなす既定の信頼閾値。</summary>
        public readonly float trustedThreshold;

        public CommercialIntegrityParams(float buildRate, float breachSeverityScale, float opinionSwing,
            float trustDeterrence, float repeatDiscount, float recoveryRate, float trustedThreshold)
        {
            this.buildRate = Mathf.Max(0f, buildRate);
            this.breachSeverityScale = Mathf.Max(0f, breachSeverityScale);
            this.opinionSwing = Mathf.Clamp01(opinionSwing);
            this.trustDeterrence = Mathf.Max(0f, trustDeterrence);
            this.repeatDiscount = Mathf.Clamp01(repeatDiscount);
            this.recoveryRate = Mathf.Max(0f, recoveryRate);
            this.trustedThreshold = Mathf.Clamp01(trustedThreshold);
        }

        /// <summary>既定＝構築0.1/裏切り強度0.6（崩すは積むの6倍速）・opinion幅0.4・誘惑抑止1.0・反復割引0.3・回復0.02（構築の1/5＝遅い）・信頼相手閾値0.6。</summary>
        public static CommercialIntegrityParams Default =>
            new CommercialIntegrityParams(0.1f, 0.6f, 0.4f, 1f, 0.3f, 0.02f, 0.6f);
    }

    /// <summary>
    /// 商業誠実性の信頼基盤の純ロジック（#1590・TMS-4・アダム・スミス参考＝商業社会は誠実さ・約束遵守という徳を育てる）。
    /// 約束を守った<b>反復</b>の交易が<b>信頼</b>を徐々に積み（規模が大きいほど効く）、一度の<b>裏切り</b>はその蓄積を
    /// 一気に崩す＝「積むは徐々・崩すは一気・回復は遅い」という<b>非対称</b>。蓄積した信頼は <see cref="OpinionModifier"/> で
    /// 外交感情(opinion)を修正し（<see cref="DiplomacyRules"/> への入力・基準非破壊）、反復取引は取引コストを下げる配当を生む。
    /// 目先の利得は裏切りを誘惑するが、積んだ信頼が大きいほど失うものが大きく裏切りにくい＝商業が誠実さを育てる仕組み。
    /// <see cref="TradeRules"/>（交易の利得分配＝1取引のフロー）／<see cref="DiplomacyRules"/>（opinion の状態遷移）／
    /// <see cref="MerchantCreditRules"/>（商人<i>個人</i>の信用＝与信・レバレッジ破産）とは別＝こちらは<b>反復交易が育てる
    /// 信頼の蓄積と opinion 修正</b>。全入力クランプ・乱数なし・決定論・基準非破壊。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CommercialIntegrityRules
    {
        /// <summary>
        /// 約束を守った取引が信頼を積んだ後の値（0..1）。守れば dealSize(0..1) に比例して buildRate ぶん増え、
        /// 守らなければ増えない（裏切りの崩壊は <see cref="TrustBreach"/> で別途）。規模の大きい取引ほど信頼を積む。
        /// </summary>
        public static float TrustAccumulation(float currentTrust, bool dealHonored, float dealSize, float dt, CommercialIntegrityParams p)
        {
            float t = Mathf.Clamp01(currentTrust);
            if (!dealHonored) return t;
            float size = Mathf.Clamp01(dealSize);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(t + p.buildRate * size * step);
        }

        public static float TrustAccumulation(float currentTrust, bool dealHonored, float dealSize, float dt)
            => TrustAccumulation(currentTrust, dealHonored, dealSize, dt, CommercialIntegrityParams.Default);

        /// <summary>
        /// 裏切り後の信頼（0..1）＝severity(0..1)×breachSeverityScale ぶん一気に削る。dt 非依存の即時崩落＝積むより速い
        /// （非対称の核：構築は <see cref="TrustAccumulation"/> が dt×buildRate で徐々、崩壊はここで一撃）。
        /// </summary>
        public static float TrustBreach(float currentTrust, float severity, CommercialIntegrityParams p)
        {
            float t = Mathf.Clamp01(currentTrust);
            float sev = Mathf.Clamp01(severity);
            return Mathf.Clamp01(t - sev * p.breachSeverityScale);
        }

        public static float TrustBreach(float currentTrust, float severity)
            => TrustBreach(currentTrust, severity, CommercialIntegrityParams.Default);

        /// <summary>
        /// 商業上の誠実評判（0..1）＝信頼×取引実績(dealCount 0..1)。信頼が高くても実績が浅ければ評判は低く、
        /// 反復の積み重ね（実績）と守った信頼の<b>両方</b>が揃って初めて誠実商人と見なされる（積）。
        /// </summary>
        public static float IntegrityReputation(float trust, float dealCount)
        {
            return Mathf.Clamp01(trust) * Mathf.Clamp01(dealCount);
        }

        /// <summary>
        /// 信頼が opinion を修正する量（-opinionSwing..+opinionSwing）。信頼0.5を中立として、高信頼は opinion を上げ
        /// 低信頼は下げる＝<see cref="DiplomacyRules"/> の opinion へ加える入力（基準非破壊＝opinion 本体は別管理）。
        /// </summary>
        public static float OpinionModifier(float trust, CommercialIntegrityParams p)
        {
            float t = Mathf.Clamp01(trust);
            // 0.5を中立に -1..+1 へ写し、振れ幅で掛ける（信頼1.0→+swing・0.0→-swing）。
            return (t - 0.5f) * 2f * p.opinionSwing;
        }

        public static float OpinionModifier(float trust)
            => OpinionModifier(trust, CommercialIntegrityParams.Default);

        /// <summary>
        /// 裏切りの誘惑（0..1）＝目先の利得 shortTermGain(0..1) が信頼を裏切る引力。蓄積した信頼が大きいほど失うものが
        /// 大きく誘惑は弱まる（trustDeterrence で減衰）＝信頼が裏切りの抑止になる。利得が無ければ誘惑も無い。
        /// </summary>
        public static float DefaultTemptation(float shortTermGain, float trust, CommercialIntegrityParams p)
        {
            float gain = Mathf.Clamp01(shortTermGain);
            float t = Mathf.Clamp01(trust);
            // 信頼が抑止：失うもの(信頼×抑止係数)ぶん誘惑を割り引く（下限0）。
            float deterred = 1f - t * p.trustDeterrence;
            return Mathf.Clamp01(gain * Mathf.Max(0f, deterred));
        }

        public static float DefaultTemptation(float shortTermGain, float trust)
            => DefaultTemptation(shortTermGain, trust, CommercialIntegrityParams.Default);

        /// <summary>
        /// 反復取引の配当（0..repeatDiscount）＝取引実績 dealCount(0..1) が取引コストを下げる割引率。何度も誠実に
        /// 取引した相手とは交渉・確認のコストが下がる＝信頼の経済的見返り。実績に線形比例。
        /// </summary>
        public static float RepeatDealingBonus(float dealCount, CommercialIntegrityParams p)
        {
            return Mathf.Clamp01(dealCount) * p.repeatDiscount;
        }

        public static float RepeatDealingBonus(float dealCount)
            => RepeatDealingBonus(dealCount, CommercialIntegrityParams.Default);

        /// <summary>
        /// 裏切り後の信頼回復（0..1）＝recoveryRate×dt ぶん徐々に戻る。buildRate より遥かに遅い回復速度（既定で1/5）＝
        /// 一度崩した信頼は徐々にしか戻らない非対称（<see cref="TrustBreach"/> の一撃崩壊と対）。
        /// </summary>
        public static float RecoveryAsymmetry(float trust, float dt, CommercialIntegrityParams p)
        {
            float t = Mathf.Clamp01(trust);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(t + p.recoveryRate * step);
        }

        public static float RecoveryAsymmetry(float trust, float dt)
            => RecoveryAsymmetry(trust, dt, CommercialIntegrityParams.Default);

        /// <summary>信頼できる取引相手か＝信頼が閾値以上（反復交易で誠実評判を積んだ相手）。</summary>
        public static bool IsTrustedPartner(float trust, float threshold)
        {
            return Mathf.Clamp01(trust) >= Mathf.Clamp01(threshold);
        }

        public static bool IsTrustedPartner(float trust, CommercialIntegrityParams p)
            => IsTrustedPartner(trust, p.trustedThreshold);
    }
}
