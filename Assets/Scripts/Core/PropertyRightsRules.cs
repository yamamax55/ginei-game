using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 私有財産の保護の数値解決（#1036 民法・保護強度→投資意欲・純ロジック test-first）。
    /// 「財産権は成長の土台＝守られない財産は築かれない」をモデル化する：法の支配×契約履行×(1−没収リスク)
    /// で保護強度を出し、保護が強いほど投資・蓄財が進み、弱いほど資本が逃げ取引が闇に潜る。
    /// 乱数は持たない（決定論）。調整値は <see cref="PropertyRightsParams"/> に集約（基準非破壊・実効値パターン）。
    /// 分担：<see cref="ConfiscationRules"/>(個別の没収＝一回性の収奪)・<see cref="MagnaCartaRules"/>(王権制約＝課税同意/抵抗権)
    /// ・<see cref="ConstitutionRules"/>(権力の制約範囲)とは別＝こちらは財産権という制度の保護強度が経済行動に効く側を扱う。
    /// </summary>
    public static class PropertyRightsRules
    {
        /// <summary>
        /// 財産権の保護強度（0..1）＝法の支配×契約履行×(1−没収リスク)。
        /// 三要素のいずれかが欠ければ全体が崩れる（積＝法が機能しても契約が守られねば／恣意的没収が横行すれば財産権は無に近づく）。
        /// </summary>
        public static float ProtectionStrength(float ruleOfLaw, float contractEnforcement, float expropriationRisk)
        {
            float law = Mathf.Clamp01(ruleOfLaw);
            float contract = Mathf.Clamp01(contractEnforcement);
            float risk = Mathf.Clamp01(expropriationRisk);
            return law * contract * (1f - risk);
        }

        /// <summary>
        /// 投資意欲（0..1）＝保護が強いほど人は安心して投資する＝財産権は成長の土台。
        /// 保護ゼロでも生存的な最低投資 <see cref="PropertyRightsParams.InvestmentFloor"/> は残り、保護1で最大1.0。
        /// </summary>
        public static float InvestmentIncentive(float protectionStrength, PropertyRightsParams p)
        {
            float s = Mathf.Clamp01(protectionStrength);
            return Mathf.Clamp01(p.InvestmentFloor + (1f - p.InvestmentFloor) * s);
        }

        /// <summary>投資意欲（既定パラメータ）。</summary>
        public static float InvestmentIncentive(float protectionStrength)
            => InvestmentIncentive(protectionStrength, PropertyRightsParams.Default);

        /// <summary>
        /// 資本逃避（0..1）＝保護が弱いほど資本が安全な場所へ逃げる。
        /// 逃避圧＝(1−保護強度)×資本の機動性（資本が動けなければ弱保護でも逃げられない）。
        /// </summary>
        public static float CapitalFlight(float protectionStrength, float capitalMobility)
        {
            float s = Mathf.Clamp01(protectionStrength);
            float mobility = Mathf.Clamp01(capitalMobility);
            return Mathf.Clamp01((1f - s) * mobility);
        }

        /// <summary>
        /// 蓄財の速度＝保護が富の形成を促す。基準成長率に保護由来の倍率（<see cref="PropertyRightsParams.AccumulationFloor"/>..1）を掛ける
        /// ＝守られない財産は築かれない（弱保護では基準成長の一部しか実らない）。基準値（baseGrowth）は非破壊。
        /// </summary>
        public static float WealthAccumulationRate(float protectionStrength, float baseGrowth, PropertyRightsParams p)
        {
            float s = Mathf.Clamp01(protectionStrength);
            float factor = Mathf.Lerp(p.AccumulationFloor, 1f, s);
            return baseGrowth * factor;
        }

        /// <summary>蓄財速度（既定パラメータ）。</summary>
        public static float WealthAccumulationRate(float protectionStrength, float baseGrowth)
            => WealthAccumulationRate(protectionStrength, baseGrowth, PropertyRightsParams.Default);

        /// <summary>
        /// 地下経済の割合（0..1）＝財産権が弱いと取引が闇に潜る＝公式経済の縮小。
        /// 保護が強いほど取引は公の場へ出る（最大 <see cref="PropertyRightsParams.MaxInformalShare"/> まで、保護1で底＝ほぼ公式化）。
        /// </summary>
        public static float InformalEconomyShare(float protectionStrength, PropertyRightsParams p)
        {
            float s = Mathf.Clamp01(protectionStrength);
            return Mathf.Clamp01(p.MaxInformalShare * (1f - s));
        }

        /// <summary>地下経済割合（既定パラメータ）。</summary>
        public static float InformalEconomyShare(float protectionStrength)
            => InformalEconomyShare(protectionStrength, PropertyRightsParams.Default);

        /// <summary>
        /// 没収ショック後の実効保護強度（0..保護強度）＝一度の恣意的没収が信頼を長く損なう。
        /// 没収が起きた時だけ <see cref="PropertyRightsParams.ExpropriationShockFactor"/> ぶん保護強度が即座に削られる
        /// ＝<see cref="ConfiscationRules"/> の長期帰結（制度としての信頼は一回の収奪で毀損する）。起きなければ非破壊で素通し。
        /// </summary>
        public static float ExpropriationShock(float protectionStrength, bool expropriationEvent, PropertyRightsParams p)
        {
            float s = Mathf.Clamp01(protectionStrength);
            if (!expropriationEvent) return s;
            return Mathf.Clamp01(s * (1f - p.ExpropriationShockFactor));
        }

        /// <summary>没収ショック（既定パラメータ）。</summary>
        public static float ExpropriationShock(float protectionStrength, bool expropriationEvent)
            => ExpropriationShock(protectionStrength, expropriationEvent, PropertyRightsParams.Default);
    }

    /// <summary>
    /// PropertyRightsRules の調整値（マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// ctor で全値をクランプ（投資/蓄財の床は 0..1、没収ショックは 0..1）。
    /// </summary>
    public readonly struct PropertyRightsParams
    {
        /// <summary>保護ゼロでも残る最低投資意欲（生存的投資・0..1）。</summary>
        public readonly float InvestmentFloor;
        /// <summary>保護ゼロでの蓄財倍率の床（弱保護で実る基準成長の割合・0..1）。</summary>
        public readonly float AccumulationFloor;
        /// <summary>保護ゼロでの地下経済割合の上限（0..1）。</summary>
        public readonly float MaxInformalShare;
        /// <summary>一度の恣意的没収が保護強度を削る割合（信頼毀損の深さ・0..1）。</summary>
        public readonly float ExpropriationShockFactor;

        public PropertyRightsParams(
            float investmentFloor, float accumulationFloor, float maxInformalShare, float expropriationShockFactor)
        {
            InvestmentFloor = Mathf.Clamp01(investmentFloor);
            AccumulationFloor = Mathf.Clamp01(accumulationFloor);
            MaxInformalShare = Mathf.Clamp01(maxInformalShare);
            ExpropriationShockFactor = Mathf.Clamp01(expropriationShockFactor);
        }

        /// <summary>
        /// 既定（投資の床0.1／蓄財の床0.2／地下経済上限0.6／没収ショック0.4）。
        /// 没収ショックは大きい＝一度の恣意的没収が保護の4割を即座に削り、信頼を長く損なう。
        /// </summary>
        public static PropertyRightsParams Default => new PropertyRightsParams(
            investmentFloor: 0.1f, accumulationFloor: 0.2f, maxInformalShare: 0.6f, expropriationShockFactor: 0.4f);
    }
}
