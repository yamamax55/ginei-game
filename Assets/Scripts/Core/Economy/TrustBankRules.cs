using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 信託銀行のロジック（#2003 TRST・純ロジック・唯一の窓口）。受託者として他人の資産を運用し信託報酬を得る金融＝銀行
    /// （自分の金 #186/#1976）でも証券（仲介 #1963）でもない archetype：信託の基礎と分別管理（TRST-1）／金銭信託の元本保証
    /// vs 実績配当（TRST-2）／アセットマネジメント AUM×報酬（TRST-3）／年金信託と積立不足（TRST-4・高齢化 #153）／投資信託の
    /// 基準価額（TRST-5）／受託者責任・併営（TRST-6）。倒産隔離（#1939 でも信託資産は無傷）が核。マクロ近似。test-first。
    /// </summary>
    public static class TrustBankRules
    {
        /// <summary>既定の運用報酬率（AUM に対する信託報酬）。</summary>
        public const float DefaultTrustFeeRate = 0.01f;

        /// <summary>既定の成功報酬率（運用益に対する取り分）。</summary>
        public const float DefaultPerformanceFeeRate = 0.1f;

        // ===== TRST-1 信託の基礎・分別管理 =====

        /// <summary>信託報酬＝信託財産×報酬率（受託者が運用の対価として得る）。</summary>
        public static float TrustFee(float trustAssets, float feeRate)
            => Mathf.Max(0f, trustAssets) * Mathf.Max(0f, feeRate);

        /// <summary>
        /// 受益者が取り戻せる額＝信託財産の全額（分別管理＝倒産隔離。信託銀行が倒産しても信託財産は丸ごと守られる）。
        /// bankInsolvent に関わらず全額返る＝これが信託の保護機能（#1939 でも無傷）。
        /// </summary>
        public static float RecoverableByBeneficiary(float trustAssets, bool bankInsolvent)
            => Mathf.Max(0f, trustAssets);

        /// <summary>信託財産は倒産隔離されるか＝常に true（分別管理＝受託者の固有財産と分離）。</summary>
        public static bool IsBankruptcyRemote() => true;

        // ===== TRST-2 金銭信託 =====

        /// <summary>運用収益＝元本×運用利回り（負もありうる）。</summary>
        public static float TrustReturn(float principal, float investmentYield)
            => Mathf.Max(0f, principal) * investmentYield;

        /// <summary>
        /// 受益者への分配＝元本保証型は元本×保証利回り（固定。信託銀行が差額を負う）、実績配当型は元本×実際利回り（受益者がリスク）。
        /// </summary>
        public static float BeneficiaryDistribution(float principal, float investmentYield, bool isPrincipalGuaranteed, float guaranteedRate)
        {
            float p = Mathf.Max(0f, principal);
            return isPrincipalGuaranteed ? p * Mathf.Max(0f, guaranteedRate) : p * investmentYield;
        }

        /// <summary>元本保証の補填＝保証利回りが実際利回りを上回ったぶん×元本（信託銀行が固有財産から穴埋め）。非負。</summary>
        public static float PrincipalGuaranteeShortfall(float principal, float guaranteedRate, float actualYield)
            => Mathf.Max(0f, guaranteedRate - actualYield) * Mathf.Max(0f, principal);

        // ===== TRST-3 アセットマネジメント =====

        /// <summary>運用報酬＝AUM×報酬率（残高に応じた安定収益＝規模の経済）。</summary>
        public static float ManagementFee(float aum, float feeRate)
            => Mathf.Max(0f, aum) * Mathf.Max(0f, feeRate);

        /// <summary>成功報酬＝運用益×成功報酬率（利益が出たときだけ。損失時は0）。</summary>
        public static float PerformanceFee(float profit, float performanceRate)
            => Mathf.Max(0f, profit) * Mathf.Max(0f, performanceRate);

        /// <summary>資金流出入と運用損益後のAUM＝AUM＋流入＋運用損益。非負。</summary>
        public static float AumAfterFlows(float aum, float inflows, float returns)
            => Mathf.Max(0f, Mathf.Max(0f, aum) + inflows + returns);

        /// <summary>アセットマネジメント総収益＝運用報酬＋成功報酬。</summary>
        public static float AssetManagementRevenue(float aum, float feeRate, float profit, float performanceRate)
            => ManagementFee(aum, feeRate) + PerformanceFee(profit, performanceRate);

        // ===== TRST-4 年金信託 =====

        /// <summary>積立比率＝年金資産/給付債務（1未満で積立不足）。債務0以下は健全＝大きい値。</summary>
        public static float FundingRatio(float assets, float liability)
            => liability <= 0f ? 999f : Mathf.Max(0f, assets) / liability;

        /// <summary>積立不足か＝年金資産が給付債務を下回る（高齢化 #153 で給付増・運用不振だと陥る）。</summary>
        public static bool IsUnderfunded(float assets, float liability)
            => assets < liability;

        /// <summary>必要追加拠出＝積立不足額/償却年数（不足を何年で埋めるか）。年数≤0は一括＝不足全額。</summary>
        public static float RequiredContribution(float liability, float assets, int amortYears)
        {
            float shortfall = Mathf.Max(0f, liability - Mathf.Max(0f, assets));
            return amortYears <= 0 ? shortfall : shortfall / amortYears;
        }

        /// <summary>年金給付額＝年金資産×給付率（退職者へ支払う）。</summary>
        public static float PensionBenefit(float assets, float payoutRate)
            => Mathf.Max(0f, assets) * Mathf.Max(0f, payoutRate);

        // ===== TRST-5 投資信託 =====

        /// <summary>基準価額（NAV）＝純資産/口数（1口あたりの価値）。口数0以下は0。</summary>
        public static float NetAssetValue(float totalAssets, float units)
            => units <= 0f ? 0f : Mathf.Max(0f, totalAssets) / units;

        /// <summary>購入口数＝投資額/基準価額。基準価額0以下は0。</summary>
        public static float UnitsIssued(float investment, float nav)
            => nav <= 0f ? 0f : Mathf.Max(0f, investment) / nav;

        /// <summary>信託財産から差し引く信託報酬＝純資産×報酬率（日々控除されるので基準価額に反映）。</summary>
        public static float FundTrustFee(float totalAssets, float feeRate)
            => Mathf.Max(0f, totalAssets) * Mathf.Max(0f, feeRate);

        // ===== TRST-6 受託者責任・併営 =====

        /// <summary>善管注意義務を満たすか＝実績がベンチマーク−許容差以上（著しく劣後しなければOK）。</summary>
        public static bool DueCareCompliant(float actualReturn, float benchmark, float tolerance)
            => actualReturn >= benchmark - Mathf.Max(0f, tolerance);

        /// <summary>善管注意義務違反の賠償＝信託財産×劣後幅（ベンチマークへの著しい劣後で受益者へ賠償）。非負。</summary>
        public static float FiduciaryBreachLoss(float trustAssets, float underperformance)
            => Mathf.Max(0f, trustAssets) * Mathf.Max(0f, underperformance);

        /// <summary>併営収益＝銀行業務の利益＋信託業務の収益（信託銀行は両方を営む＝業務範囲が広い）。</summary>
        public static float CombinedRevenue(float bankingProfit, float trustRevenue)
            => bankingProfit + trustRevenue;
    }
}
