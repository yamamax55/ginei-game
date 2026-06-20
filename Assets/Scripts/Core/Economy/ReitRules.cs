using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// REIT（不動産投資信託）のロジック（業種細分化・不動産 #2019 の証券化サブ業種・#2025・純ロジック・唯一の窓口）：分配可能利益＝NOI−支払利息（REIT-1）／
    /// 導管性＝利益の90%超を分配すれば法人税非課税（REIT-2）／分配金利回り（REIT-3）／LTV＝負債/総資産（REIT-4）。
    /// 不動産（#2019）の収益を投資家へ分配する器＝賃料が証券化されて市場（#185）に乗る。マクロ近似。test-first。
    /// </summary>
    public static class ReitRules
    {
        /// <summary>分配可能利益＝NOI（純営業収益 #2019）−支払利息。借入で物件を買うほど利息が利益を削る。</summary>
        public static float DistributableIncome(float noi, float interestExpense)
            => noi - Mathf.Max(0f, interestExpense);

        /// <summary>導管性（法人税非課税）＝分配額が分配可能利益の閾値（既定90%）以上か。利益を貯め込まず投資家へ流すのが条件。</summary>
        public static bool IsTaxExempt(float distributed, float distributableIncome, float payoutThreshold)
        {
            if (distributableIncome <= 0f) return false;
            return distributed >= distributableIncome * Mathf.Clamp01(payoutThreshold);
        }

        /// <summary>分配金利回り＝1口あたり分配金/投資口価格（債券利回りと比較される）。価格0以下は0。</summary>
        public static float DistributionYield(float distributionPerUnit, float unitPrice)
            => unitPrice <= 0f ? 0f : Mathf.Max(0f, distributionPerUnit) / unitPrice;

        /// <summary>LTV（ローン・トゥ・バリュー）＝負債/総資産（高いほどレバレッジが効くが金利上昇・地価下落に脆い）。資産0以下は0。</summary>
        public static float LoanToValue(float debt, float totalAssets)
            => totalAssets <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, debt) / totalAssets);
    }
}
