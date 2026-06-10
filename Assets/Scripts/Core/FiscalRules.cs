using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 財政・経済の純ロジック（#163 EPIC・#161 PB/国債/金利/為替・#162 税/社会保障・唯一の窓口）。
    /// プライマリーバランス（歳入−歳出、利払い除く）→赤字は国債で埋め、債務に金利（債務が大きいほどリスクプレミアムで上昇）→
    /// <b>債務スパイラル</b>。財政健全度は安定度#109/支持#113/為替へ係数(#106・実効値)で効く。税（階級別負担#110）と社会保障
    /// （人口オーナス#153 連動）の<b>再分配</b>は政治帰結（不満↔希望）を生む。細かい通貨管理は持たない（タイクン化回避）。test-first。
    /// </summary>
    public static class FiscalRules
    {
        /// <summary>財政の調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct FiscalParams
        {
            public readonly float baseInterestRate;  // 基準金利
            public readonly float riskPremiumSlope;   // 債務比率の超過1あたりの金利上乗せ
            public readonly float safeDebtRatio;      // ここまではリスクプレミアム無し
            public readonly float crisisDebtRatio;    // この債務比率で財政健全度0＝危機
            public readonly float welfarePerDependent; // 扶養1あたりの社会保障コスト

            public FiscalParams(float baseInterestRate, float riskPremiumSlope, float safeDebtRatio, float crisisDebtRatio, float welfarePerDependent)
            {
                this.baseInterestRate = Mathf.Max(0f, baseInterestRate);
                this.riskPremiumSlope = Mathf.Max(0f, riskPremiumSlope);
                this.safeDebtRatio = Mathf.Max(0f, safeDebtRatio);
                this.crisisDebtRatio = Mathf.Max(safeDebtRatio + 0.01f, crisisDebtRatio);
                this.welfarePerDependent = Mathf.Max(0f, welfarePerDependent);
            }

            /// <summary>既定＝基準金利2%・債務比率0.6まで安全/2.0で危機・超過1あたり+10%・扶養あたり社会保障1.0。</summary>
            public static FiscalParams Default => new FiscalParams(0.02f, 0.1f, 0.6f, 2.0f, 1.0f);
        }

        // 再分配の政治帰結の上限（係数）。
        public const float TaxDiscontentMax = 0.3f;  // 高税の不満（支持/協力↓）
        public const float WelfareHopeMax = 0.3f;     // 高福祉の希望（支持/希望↑）

        // ===== #161 PB・国債・金利・為替 =====

        /// <summary>債務比率＝国債残高 / 経済規模（GDP代理＝生産#93 等）。</summary>
        public static float DebtRatio(FiscalState s, float economy)
        {
            if (s == null) return 0f;
            return s.debt / Mathf.Max(1f, economy);
        }

        /// <summary>金利＝基準＋リスクプレミアム（債務比率が safe を超えるほど上昇＝市場が利回りを要求）。</summary>
        public static float InterestRate(FiscalState s, float economy, FiscalParams p)
        {
            float premium = Mathf.Max(0f, DebtRatio(s, economy) - p.safeDebtRatio) * p.riskPremiumSlope;
            return p.baseInterestRate + premium;
        }

        /// <summary>プライマリーバランス＝歳入−歳出（利払い除く）。黒字＞0／赤字＜0。</summary>
        public static float PrimaryBalance(FiscalState s)
            => s == null ? 0f : s.revenue - s.baseExpenditure;

        /// <summary>利払い＝債務残高×金利（歳出のPB外）。</summary>
        public static float InterestPayment(FiscalState s, float economy, FiscalParams p)
            => s == null ? 0f : s.debt * InterestRate(s, economy, p);

        /// <summary>総合収支＝PB−利払い（＋＝減債／−＝国債発行）。</summary>
        public static float OverallBalance(FiscalState s, float economy, FiscalParams p)
            => PrimaryBalance(s) - InterestPayment(s, economy, p);

        /// <summary>1ターン財政を進める：黒字は減債、赤字は国債発行で債務増（債務は0未満にならない）。</summary>
        public static void Tick(FiscalState s, float economy, float dt, FiscalParams p)
        {
            if (s == null || dt <= 0f) return;
            float balance = OverallBalance(s, economy, p);
            s.debt = Mathf.Max(0f, s.debt - balance * dt); // 黒字→減債、赤字→増債
        }

        /// <summary>債務スパイラル＝高債務下で利払いがPB黒字を上回り、債務が複利的に膨らむ状態。</summary>
        public static bool IsDebtSpiral(FiscalState s, float economy, FiscalParams p)
        {
            if (s == null) return false;
            return DebtRatio(s, economy) > p.safeDebtRatio
                && PrimaryBalance(s) < InterestPayment(s, economy, p);
        }

        /// <summary>財政健全度 0..1（safe で1・crisis で0・間は線形）。安定度#109/支持#113 へ係数#106 で効く。</summary>
        public static float FiscalHealthFactor(FiscalState s, float economy, FiscalParams p)
        {
            float ratio = DebtRatio(s, economy);
            if (ratio <= p.safeDebtRatio) return 1f;
            if (ratio >= p.crisisDebtRatio) return 0f;
            return 1f - (ratio - p.safeDebtRatio) / (p.crisisDebtRatio - p.safeDebtRatio);
        }

        /// <summary>為替係数（0.5〜1.0）。財政悪化で通貨安＝交易#94・フェザーン金融ハブ#160 へ。</summary>
        public static float ExchangeRateFactor(FiscalState s, float economy, FiscalParams p)
            => 0.5f + 0.5f * FiscalHealthFactor(s, economy, p);

        // ===== #162 税・社会保障（再分配） =====

        /// <summary>税収＝課税ベース×税率（課税ベースは経済/人口・階級別負担#110 を呼び出し側で重み付け）。</summary>
        public static float TaxRevenue(float taxBase, float taxRate)
            => Mathf.Max(0f, taxBase) * Mathf.Clamp01(taxRate);

        /// <summary>社会保障費＝扶養人口×水準×係数（扶養＝年少+高齢＝人口オーナス#153 連動）。</summary>
        public static float WelfareCost(float dependents, float welfareLevel, FiscalParams p)
            => Mathf.Max(0f, dependents) * Mathf.Clamp01(welfareLevel) * p.welfarePerDependent;

        /// <summary>歳入を組み立てる（税収＋交易収入）。</summary>
        public static float Revenue(float taxBase, float taxRate, float tradeIncome)
            => TaxRevenue(taxBase, taxRate) + Mathf.Max(0f, tradeIncome);

        /// <summary>歳出（利払い除く）を組み立てる（軍事＋内政＋社会保障）。</summary>
        public static float Expenditure(float military, float admin, float dependents, float welfareLevel, FiscalParams p)
            => Mathf.Max(0f, military) + Mathf.Max(0f, admin) + WelfareCost(dependents, welfareLevel, p);

        /// <summary>高税の不満（支持#113・協力#836 への負係数 0..TaxDiscontentMax）。</summary>
        public static float TaxBurdenPenalty(float taxRate)
            => Mathf.Clamp01(taxRate) * TaxDiscontentMax;

        /// <summary>高福祉の希望（希望#852・支持#113 への正係数 0..WelfareHopeMax）。人口オーナス期ほど効く前提。</summary>
        public static float WelfareHopeBonus(float welfareLevel)
            => Mathf.Clamp01(welfareLevel) * WelfareHopeMax;
    }
}
