using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 警備会社のロジック（業種細分化・サービス #2024 の労働集約サブ業種・#2025・純ロジック・唯一の窓口）：契約収益＝契約数×月額（SEC-1）／
    /// 警備員の人件費（SEC-2）／利益（SEC-3）／人件費率＝労働集約・低マージンの体質（SEC-4）。
    /// 機械警備（資本集約）と常駐警備（労働集約）があり、契約の積み上げと人件費率の管理が採算を決める。サービス#2024の業態特化。
    /// ※秘密警察 <see cref="SecurityRules"/>（政治）とは別物＝民間警備業。マクロ近似。test-first。
    /// </summary>
    public static class SecurityServiceRules
    {
        /// <summary>契約収益＝契約数×1契約あたり月額（ストック型の積み上げ収益）。</summary>
        public static float ContractRevenue(int contracts, float monthlyFeePerContract)
            => Mathf.Max(0, contracts) * Mathf.Max(0f, monthlyFeePerContract);

        /// <summary>警備員人件費＝警備員数×1人あたり賃金（労働集約ゆえ最大の費目）。</summary>
        public static float GuardLaborCost(int guards, float wagePerGuard)
            => Mathf.Max(0, guards) * Mathf.Max(0f, wagePerGuard);

        /// <summary>警備利益＝契約収益−人件費−固定費。</summary>
        public static float SecurityServiceProfit(float revenue, float laborCost, float fixedCost)
            => revenue - Mathf.Max(0f, laborCost) - Mathf.Max(0f, fixedCost);

        /// <summary>人件費率＝人件費/収益（労働集約・低マージン体質＝賃上げ#1969が直撃）。収益0以下は0。</summary>
        public static float LaborCostRatio(float laborCost, float revenue)
            => revenue <= 0f ? 0f : Mathf.Max(0f, laborCost) / revenue;
    }
}
