using UnityEngine;

namespace Ginei
{
    /// <summary>クーデターの型（#215-219）。誰が政権を奪うか＝主体で分類。</summary>
    public enum CoupType
    {
        軍部,   // 軍部クーデター＝軍が政府を倒す（軍の忠誠が低いほど起きやすい）
        宮廷,   // 宮廷クーデター＝側近/閣僚の政変（支持の薄さに乗じる）
        革命    // 革命＝下からの蜂起（支持崩壊が主因・軍は中立要因）
    }

    /// <summary>クーデターの帰結（#215-219）。発火後の決着＝成功/粛清（未遂鎮圧）/内戦（決着つかず）。</summary>
    public enum CoupOutcome
    {
        成功,   // 政権奪取に成功
        粛清,   // 未遂＝首謀者が粛清される（体制存続）
        内戦    // 決着つかず＝内戦へ（分裂）
    }

    /// <summary>
    /// クーデターの純ロジック（#215-219・唯一の窓口）。軍の忠誠（militaryLoyalty）・政治的支持（politicalSupport）と
    /// 主体（<see cref="CoupType"/>）から<b>発生確率・帰結・事後の正統性</b>を導く。軍政関係の素地は
    /// <see cref="CivilianControlRules.CoupRisk"/> を read-only で参照できる（並行システムを作らない）。
    /// 乱数は呼び出し側が roll(0..1) を渡す＝決定論的にテストできる。test-first。
    /// </summary>
    public static class CoupRules
    {
        /// <summary>粛清（鎮圧）時に既存支持を引き締めで底上げする割合（残余支持の何割を回収するか）。</summary>
        private const float PurgeTighteningRatio = 0.5f;

        /// <summary>内戦（分裂）時に残る正統性の割合（大きく毀損）。</summary>
        private const float CivilWarLegitimacyRatio = 0.3f;

        /// <summary>クーデター解決の調整値。</summary>
        public readonly struct CoupParams
        {
            /// <summary>この確率以上で「成功」する閾値（roll をこの割合へ写像）。</summary>
            public readonly float successCutoff;

            /// <summary>未遂のうち「内戦」へ転ぶ割合（残りは粛清）。</summary>
            public readonly float civilWarShare;

            /// <summary>成功クーデターの事後正統性の基礎値（0..1）。</summary>
            public readonly float postCoupBaseLegitimacy;

            public CoupParams(float successCutoff, float civilWarShare, float postCoupBaseLegitimacy)
            {
                this.successCutoff = Mathf.Clamp01(successCutoff);
                this.civilWarShare = Mathf.Clamp01(civilWarShare);
                this.postCoupBaseLegitimacy = Mathf.Clamp01(postCoupBaseLegitimacy);
            }

            /// <summary>既定＝成功境界0.5／内戦割合0.4／事後正統性0.4。</summary>
            public static CoupParams Default => new CoupParams(0.5f, 0.4f, 0.4f);
        }

        /// <summary>主体ごとの主因の重み（軍の忠誠と支持のどちらに駆動されるか）。</summary>
        private static void DriverWeights(CoupType type, out float loyaltyWeight, out float supportWeight)
        {
            switch (type)
            {
                case CoupType.軍部: loyaltyWeight = 0.7f; supportWeight = 0.3f; break; // 軍の離反が主因
                case CoupType.宮廷: loyaltyWeight = 0.4f; supportWeight = 0.6f; break; // 支持の薄さに乗じる
                case CoupType.革命: loyaltyWeight = 0.2f; supportWeight = 0.8f; break; // 支持崩壊が主因
                default:            loyaltyWeight = 0.5f; supportWeight = 0.5f; break;
            }
        }

        /// <summary>
        /// クーデターの発生確率（0..1）。軍の忠誠（militaryLoyalty）・政治的支持（politicalSupport）が低いほど高い。
        /// 主体ごとに主因の重みが変わる（軍部＝忠誠主導／宮廷・革命＝支持主導）。係数 #106 想定。
        /// </summary>
        public static float CoupSuccessChance(float militaryLoyalty, float politicalSupport, CoupType type, CoupParams prm)
        {
            float loyalty = Mathf.Clamp01(militaryLoyalty);
            float support = Mathf.Clamp01(politicalSupport);
            DriverWeights(type, out float lw, out float sw);
            // 不満（=1-値）の加重和。重みの総和で正規化して 0..1 を保証
            float discontent = ((1f - loyalty) * lw + (1f - support) * sw) / (lw + sw);
            return Mathf.Clamp01(discontent);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float CoupSuccessChance(float militaryLoyalty, float politicalSupport, CoupType type)
            => CoupSuccessChance(militaryLoyalty, politicalSupport, type, CoupParams.Default);

        /// <summary>
        /// 発生確率（chance）と roll(0..1) から帰結を解決する。roll が chance を下回れば「成功」、
        /// 上回る（＝未遂）うち <see cref="CoupParams.civilWarShare"/> 相当の上振れは「内戦」、残りは「粛清」。
        /// </summary>
        public static CoupOutcome Resolve(float chance, float roll, CoupParams prm)
        {
            float c = Mathf.Clamp01(chance);
            float r = Mathf.Clamp01(roll);
            if (r < c) return CoupOutcome.成功; // 蜂起が押し切る
            // 未遂域 [c,1] のうち上端寄り civilWarShare ぶんは決着つかず内戦
            float civilWarBand = (1f - c) * prm.civilWarShare;
            if (r >= 1f - civilWarBand) return CoupOutcome.内戦;
            return CoupOutcome.粛清; // 体制が鎮圧
        }

        /// <summary>既定パラメータ版。</summary>
        public static CoupOutcome Resolve(float chance, float roll)
            => Resolve(chance, roll, CoupParams.Default);

        /// <summary>
        /// クーデター後の体制正統性（0..1）。成功＝奪取側が新体制の正統性を主体支持で築く（基礎＋支持ぶん）。
        /// 粛清＝既存体制が引き締まり正統性を回復（最大値寄り）。内戦＝分裂で正統性は大きく毀損（最小値寄り）。
        /// </summary>
        public static float PostCoupLegitimacy(CoupOutcome outcome, float politicalSupport, CoupParams prm)
        {
            float support = Mathf.Clamp01(politicalSupport);
            switch (outcome)
            {
                case CoupOutcome.成功:
                    // 新体制＝基礎正統性＋掌握後に残る支持ぶん（上振れ）
                    return Mathf.Clamp01(prm.postCoupBaseLegitimacy + support * (1f - prm.postCoupBaseLegitimacy));
                case CoupOutcome.粛清:
                    // 鎮圧で引き締め＝既存支持を底上げ（下限は既存支持を割らない）
                    return Mathf.Clamp01(support + (1f - support) * PurgeTighteningRatio);
                case CoupOutcome.内戦:
                    // 分裂＝正統性の崩壊
                    return Mathf.Clamp01(support * CivilWarLegitimacyRatio);
                default:
                    return support;
            }
        }

        /// <summary>既定パラメータ版。</summary>
        public static float PostCoupLegitimacy(CoupOutcome outcome, float politicalSupport)
            => PostCoupLegitimacy(outcome, politicalSupport, CoupParams.Default);
    }
}
