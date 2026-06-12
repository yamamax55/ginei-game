using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宇宙保険（船体・貨物保険）のロジック（業種細分化・保険 #1982 の宇宙設定派生サブ業種・#2025・純ロジック・唯一の窓口）：船体保険料（SINS-1）／
    /// 戦争危険割増＝前線・通商破壊海域の追加保険料（SINS-2＝通商破壊#94/#95連動）／保険金支払い（SINS-3）／引受損益（SINS-4）。
    /// 海上保険（ロイズ#1982）の宇宙版＝航宙リスク（ブラックホール・前線）と戦争危険を引き受ける。危険海域ほど保険料が跳ね、撃沈で保険金が出る。マクロ近似。test-first。
    /// </summary>
    public static class SpaceInsuranceRules
    {
        /// <summary>船体保険料＝保険価額×基準料率×危険海域倍率（前線・ブラックホール圏ほど倍率が高い）。</summary>
        public static float HullPremium(float insuredValue, float baseRate, float riskZoneMultiplier)
            => Mathf.Max(0f, insuredValue) * Mathf.Max(0f, baseRate) * Mathf.Max(0f, riskZoneMultiplier);

        /// <summary>戦争危険割増＝保険価額×戦争危険料率（前線・通商破壊#94/#95の海域に課す追加保険料）。</summary>
        public static float WarRiskSurcharge(float insuredValue, float warRiskRate)
            => Mathf.Max(0f, insuredValue) * Mathf.Max(0f, warRiskRate);

        /// <summary>保険金支払い＝損害程度×保険価額（全損=1.0/分損は割合＝撃沈・損傷で支払う）。</summary>
        public static float ClaimPayout(float lossSeverity, float insuredValue)
            => Mathf.Clamp01(lossSeverity) * Mathf.Max(0f, insuredValue);

        /// <summary>引受損益＝保険料収入−支払保険金−経費（危険海域の料率設定を誤ると赤字）。</summary>
        public static float UnderwritingResult(float premiums, float claims, float expenses)
            => premiums - Mathf.Max(0f, claims) - Mathf.Max(0f, expenses);
    }
}
