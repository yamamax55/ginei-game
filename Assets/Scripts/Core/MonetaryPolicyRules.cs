using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 中央銀行／FRB の金融政策ロジック（#1945 CBNK・純ロジック・唯一の窓口）。中央銀行が政策金利を決め（テイラー則 CB-1）、
    /// マネーサプライがインフレ/デフレを生み（貨幣数量説 CB-2）、公開市場操作で市場と国債利回りを動かし（CB-3）、
    /// 危機時に最後の貸し手として流動性を供給し（CB-4）、独立性が政治圧力との綱引きを決める（CB-5）。
    /// 既存の <see cref="FiscalRules"/>(#163)・債券市場(#161/#185)・<see cref="BankRules"/>(#186)・<see cref="FinancialCrisisRules"/>(#1939)
    /// へ接続（read-only/接続のみ）。実効値パターン（基準非破壊）。マクロ近似。test-first。
    /// </summary>
    public static class MonetaryPolicyRules
    {
        /// <summary>中立金利（自然利子率＝景気にも物価にも中立な実質金利の近似）。</summary>
        public const float NeutralRate = 0.02f;

        /// <summary>テイラー則：インフレ・ギャップの重み（>1＝テイラー原則＝物価安定に強く反応）。</summary>
        public const float TaylorInflationWeight = 1.5f;

        /// <summary>テイラー則：需給ギャップ（産出ギャップ）の重み。</summary>
        public const float TaylorOutputWeight = 0.5f;

        /// <summary>名目金利のゼロ下限（ZLB）。ここに貼り付くと流動性の罠（KEYN-4 #1548）。</summary>
        public const float ZeroLowerBound = 0f;

        // ===== CB-1 政策金利（テイラー則） =====

        /// <summary>
        /// テイラー則：政策金利＝中立金利＋現行インフレ＋w_π(π−π*)＋w_y(産出ギャップ)。ZLB でクランプ。
        /// インフレが目標超で利上げ・需給がマイナス（不況）で利下げ。
        /// </summary>
        public static float TaylorRate(float inflation, float inflationTarget, float outputGap, float neutralRate)
        {
            float r = neutralRate + inflation
                    + TaylorInflationWeight * (inflation - inflationTarget)
                    + TaylorOutputWeight * outputGap;
            return Mathf.Max(ZeroLowerBound, r);
        }

        /// <summary>テイラー則（中央銀行のインフレ目標と既定の中立金利を使う簡易窓口）。</summary>
        public static float TaylorRate(CentralBank cb, float inflation, float outputGap)
            => cb == null ? 0f : TaylorRate(inflation, cb.inflationTarget, outputGap, NeutralRate);

        /// <summary>市場の基準金利＝政策金利（<see cref="FiscalRules.FiscalParams"/>.baseInterestRate / 国債利回り #161 の出所）。</summary>
        public static float MarketBaseRate(CentralBank cb) => cb == null ? 0f : Mathf.Max(0f, cb.policyRate);

        /// <summary>流動性の罠＝政策金利がゼロ下限に貼り付く＝これ以上利下げできず金融政策が無効（KEYN-4 と整合）。</summary>
        public static bool IsLiquidityTrap(float policyRate) => policyRate <= ZeroLowerBound + 1e-6f;

        // ===== CB-2 マネーサプライとインフレ（貨幣数量説 MV=PY） =====

        /// <summary>
        /// 貨幣数量説：インフレ率 ≈ マネー成長率 − 実質産出成長率（流通速度 V 一定）。
        /// マネーを撒きすぎ（成長が産出を上回る）でインフレ・絞りすぎでデフレ。
        /// </summary>
        public static float Inflation(float moneyGrowth, float realOutputGrowth)
            => moneyGrowth - realOutputGrowth;

        /// <summary>インフレ目標を満たすマネー成長率＝目標インフレ＋実質産出成長（撒くべき量の逆算）。</summary>
        public static float MoneyGrowthForTarget(float inflationTarget, float realOutputGrowth)
            => inflationTarget + realOutputGrowth;

        // ===== CB-3 公開市場操作（OMO）と量的緩和 =====

        /// <summary>
        /// 公開市場操作：国債の買いオペ（bondPurchase>0）＝市場へ資金供給＝マネーサプライ↑、売りオペ（&lt;0）＝吸収＝↓。
        /// 更新後のマネーサプライを返す（cb を破壊的に更新）。
        /// </summary>
        public static float OpenMarketOperation(CentralBank cb, float bondPurchase)
        {
            if (cb == null) return 0f;
            cb.moneySupply = Mathf.Max(0f, cb.moneySupply + bondPurchase);
            return cb.moneySupply;
        }

        /// <summary>
        /// 公開市場操作の国債利回りへの影響＝買いオペは国債需要↑→価格↑→利回り↓（負を返す）。
        /// marketDepth（債券市場の厚み）が大きいほど一単位あたりの影響は小さい（#161/#185 の利回りに足し込む）。
        /// </summary>
        public static float YieldImpact(float bondPurchase, float marketDepth)
            => marketDepth <= 0f ? 0f : -bondPurchase / marketDepth;

        /// <summary>量的緩和（QE）＝ZLB で金利を下げられない時に直接マネーを供給する買いオペ（非負）。</summary>
        public static float QuantitativeEasing(CentralBank cb, float amount)
            => OpenMarketOperation(cb, Mathf.Max(0f, amount));

        // ===== CB-4 最後の貸し手（危機時の緊急流動性） =====

        /// <summary>緊急流動性供給額＝不足ぶん（非負）。中央銀行が一時的に資金を貸す（最後の貸し手）。</summary>
        public static float EmergencyLiquidity(FinancialInstitution fi, float shortfall)
            => Mathf.Max(0f, shortfall);

        /// <summary>破綻回避に要る最小流動性＝損失が自己資本を超えたぶん（債務超過の穴）。支払可能なら0。</summary>
        public static float RequiredLiquidity(FinancialInstitution fi, float loss)
            => Mathf.Max(0f, Mathf.Max(0f, loss) - (fi == null ? 0f : fi.capital));

        /// <summary>
        /// 緊急流動性供給で破綻を回避できるか＝自己資本−損失＋供給 ≥ 0。
        /// illiquid（流動性不足）だが solvent（支払可能）な機関を救う＝伝染（#1939 LEHM-4）を断つ。
        /// </summary>
        public static bool SurvivesWithLiquidity(FinancialInstitution fi, float loss, float liquidity)
            => ((fi == null ? 0f : fi.capital) - Mathf.Max(0f, loss) + Mathf.Max(0f, liquidity)) >= 0f;

        // ===== CB-5 中央銀行の独立性（政治圧力 vs インフレ目標） =====

        /// <summary>
        /// 実効政策金利＝テイラー則の金利と政府が望む金利（通常は低金利＝緩和圧力）を独立性で混合。
        /// independence=1＝完全にテイラー則／0＝政府の圧力に従う。
        /// </summary>
        public static float EffectivePolicyRate(float taylorRate, float politicalDesiredRate, float independence)
            => Mathf.Max(ZeroLowerBound, Mathf.Lerp(politicalDesiredRate, taylorRate, Mathf.Clamp01(independence)));

        /// <summary>
        /// インフレバイアス＝低独立性×高政治圧力で緩和的に振れ、目標を超えてインフレが進む傾向（0..1）。
        /// 独立した中央銀行ほどこの歪みが小さい（通貨安 #160 の遠因）。
        /// </summary>
        public static float InflationBias(float independence, float politicalPressure)
            => Mathf.Clamp01(1f - Mathf.Clamp01(independence)) * Mathf.Max(0f, politicalPressure);
    }
}
