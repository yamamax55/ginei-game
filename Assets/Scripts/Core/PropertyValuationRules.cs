using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 不動産の評価・収益の純ロジック（NFIN-5・#2070・実効値パターン）。
    /// 権利証の価値＝惑星評価額×持分、地代＝価値×地代率。地価は年で変動（不動産#2019 連携）。test-first。
    /// </summary>
    public static class PropertyValuationRules
    {
        /// <summary>権利証の価値＝惑星評価額×持分。</summary>
        public static float DeedValue(PropertyDeed d)
            => d == null ? 0f : Mathf.Max(0f, d.baseValue) * Mathf.Clamp01(d.share);

        /// <summary>地代収益＝権利証価値×地代率（年）。</summary>
        public static float RentIncome(PropertyDeed d)
            => d == null ? 0f : DeedValue(d) * Mathf.Max(0f, d.rentRate);

        /// <summary>1年後の惑星評価額＝max(0, 評価額×(1+地価変動率))。バブル崩壊#2019 で負率なら下落。</summary>
        public static float ValueAfterYear(float baseValue, float appreciationRate)
            => Mathf.Max(0f, baseValue * (1f + appreciationRate));
    }
}
