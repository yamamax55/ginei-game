using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 銀行・信用の純ロジック（CAP-2 #186・唯一の窓口）。部分準備銀行制での<b>信用創造</b>
    /// （準備率が低いほど少ない預金から多くの信用を生む＝マネー乗数）と、信認低下が招く<b>取り付け</b>、
    /// 貸出金利と預金金利の<b>利鞘</b>、債務超過（貸出＜預金＝資本毀損）の判定を扱う。
    /// 細かい会計は持たない（タイクン化回避）。基準フィールドは非破壊。test-first。
    /// </summary>
    public static class BankRules
    {
        /// <summary>銀行の調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct BankParams
        {
            public readonly float minReserveRatio;  // 信用創造で割る準備率の下限（ゼロ除算防止）
            public readonly float runConfidence;     // この信認で取り付けリスク最大
            public readonly float safeConfidence;    // この信認で取り付けリスク0
            public readonly float reserveRiskWeight; // 準備率の薄さが取り付けリスクに寄与する重み

            public BankParams(float minReserveRatio, float runConfidence, float safeConfidence, float reserveRiskWeight)
            {
                this.minReserveRatio = Mathf.Clamp(minReserveRatio, 0.001f, 1f);
                this.runConfidence = Mathf.Clamp01(runConfidence);
                this.safeConfidence = Mathf.Clamp(safeConfidence, this.runConfidence + 0.01f, 1f);
                this.reserveRiskWeight = Mathf.Clamp01(reserveRiskWeight);
            }

            /// <summary>既定＝準備率下限1%・信認0.2で取り付け最大/0.8で安全・準備薄リスク重み0.5。</summary>
            public static BankParams Default => new BankParams(0.01f, 0.2f, 0.8f, 0.5f);
        }

        /// <summary>信用創造額＝預金×(1/準備率−1)＝マネー乗数。準備率が薄いほど多くの信用を生む。</summary>
        public static float CreditCreation(float deposits, float reserveRatio, BankParams p)
        {
            float d = Mathf.Max(0f, deposits);
            float r = Mathf.Clamp(reserveRatio, p.minReserveRatio, 1f);
            return d * (1f / r - 1f);
        }

        /// <summary>取り付けリスク 0..1。信認が低いほど高く、準備率が薄いほど上乗せ（信頼が崩れると現金が足りない）。</summary>
        public static float BankRunRisk(float reserveRatio, float confidence, BankParams p)
        {
            float c = Mathf.Clamp01(confidence);
            // 信認ベースのリスク（safe で0・run で1・間は線形）。
            float confRisk;
            if (c >= p.safeConfidence) confRisk = 0f;
            else if (c <= p.runConfidence) confRisk = 1f;
            else confRisk = (p.safeConfidence - c) / (p.safeConfidence - p.runConfidence);
            // 準備率が薄いほどリスク上乗せ（準備率1で寄与0・準備率0で寄与最大）。
            float reserveRisk = (1f - Mathf.Clamp01(reserveRatio)) * p.reserveRiskWeight;
            return Mathf.Clamp01(confRisk + confRisk * reserveRisk);
        }

        /// <summary>利鞘＝貸出金利−預金金利（銀行の収益源。負＝逆鞘）。</summary>
        public static float InterestSpread(float loanRate, float depositRate)
            => loanRate - depositRate;

        /// <summary>債務超過＝資産（貸出）が負債（預金）を下回る＝自己資本がマイナス（不良債権で毀損）。</summary>
        public static bool IsInsolvent(Bank b)
            => b != null && b.loans < b.deposits;

        // ===== バランスシート拡張（BANK #1976・additive） =====

        /// <summary>BIS規制の最低自己資本比率（リスク資産に対する自己資本の最低割合＝8%）。</summary>
        public const float MinCapitalRatio = 0.08f;

        /// <summary>貸出のリスク重み（自己資本比率の分母＝信用リスクが高い）。</summary>
        public const float DefaultLoanRiskWeight = 1f;

        /// <summary>有価証券（国債等）のリスク重み（貸出より低い）。</summary>
        public const float DefaultSecuritiesRiskWeight = 0.2f;

        /// <summary>信用乗数で割る準備率の下限（ゼロ除算防止）。</summary>
        public const float MinReserveForMultiplier = 0.01f;

        // ----- BANK-1 バランスシート -----

        /// <summary>総資産＝準備金＋貸出＋有価証券。</summary>
        public static float TotalAssets(Bank b)
            => b == null ? 0f : Mathf.Max(0f, b.reserves) + Mathf.Max(0f, b.loans) + Mathf.Max(0f, b.securities);

        /// <summary>自己資本＝総資産−負債（預金＋借入）。マイナス＝債務超過。既存 <see cref="IsInsolvent"/>（貸出＜預金）の一般化。</summary>
        public static float Equity(Bank b)
            => b == null ? 0f : TotalAssets(b) - Mathf.Max(0f, b.deposits) - Mathf.Max(0f, b.borrowings);

        /// <summary>バランスシート上の債務超過＝自己資本がマイナス（不良債権・評価損で資産が負債を割る）。</summary>
        public static bool IsInsolventBySheet(Bank b) => b != null && Equity(b) < 0f;

        // ----- BANK-2 自己資本比率（BIS規制） -----

        /// <summary>リスク加重資産＝貸出×貸出リスク重み＋有価証券×証券リスク重み（リスクの高い資産ほど多く積む）。</summary>
        public static float RiskWeightedAssets(Bank b, float loanRiskWeight, float securitiesRiskWeight)
        {
            if (b == null) return 0f;
            return Mathf.Max(0f, b.loans) * Mathf.Max(0f, loanRiskWeight)
                 + Mathf.Max(0f, b.securities) * Mathf.Max(0f, securitiesRiskWeight);
        }

        /// <summary>自己資本比率＝自己資本/リスク加重資産（BIS規制の指標）。リスク資産0以下はリスクなし＝大きい値。</summary>
        public static float CapitalAdequacyRatio(Bank b, float loanRiskWeight, float securitiesRiskWeight)
        {
            float rwa = RiskWeightedAssets(b, loanRiskWeight, securitiesRiskWeight);
            return rwa <= 0f ? 1f : Equity(b) / rwa;
        }

        /// <summary>自己資本比率規制を満たすか＝比率が最低基準以上（割れると過小資本＝増資/貸し渋りを迫られる）。</summary>
        public static bool MeetsCapitalRequirement(Bank b, float minRatio, float loanRiskWeight, float securitiesRiskWeight)
            => b != null && CapitalAdequacyRatio(b, loanRiskWeight, securitiesRiskWeight) >= minRatio;

        // ----- BANK-3 信用乗数と中央銀行 -----

        /// <summary>信用乗数（マネー乗数）＝1/準備率（準備率が薄いほど多くのマネーを生む）。下限クランプ。</summary>
        public static float MoneyMultiplier(float reserveRatio)
            => 1f / Mathf.Clamp(reserveRatio, MinReserveForMultiplier, 1f);

        /// <summary>中央銀行（#1945）の法定準備率からの信用乗数。中央銀行が準備率を上げると乗数が下がる（引き締め）。</summary>
        public static float MoneyMultiplierFromCentralBank(CentralBank cb)
            => cb == null ? 0f : MoneyMultiplier(cb.reserveRequirement);

        // ----- BANK-4 銀行の収益 -----

        /// <summary>純金利収益＝貸出×貸出金利−預金×預金金利（銀行の本業の儲け＝利鞘の実額）。</summary>
        public static float NetInterestIncome(Bank b, float loanRate, float depositRate)
            => b == null ? 0f : Mathf.Max(0f, b.loans) * loanRate - Mathf.Max(0f, b.deposits) * depositRate;

        /// <summary>不良債権の損失引当＝不良債権×デフォルト時損失率（焦げ付きの見込み損）。</summary>
        public static float LoanLossProvision(Bank b, float lossGivenDefault)
            => b == null ? 0f : Mathf.Max(0f, b.nonPerformingLoans) * Mathf.Clamp01(lossGivenDefault);

        /// <summary>銀行利益＝純金利収益−不良債権損失引当（不良債権が増えると利益・自己資本が削られる＝#1939 の入口）。</summary>
        public static float BankProfit(Bank b, float loanRate, float depositRate, float lossGivenDefault)
            => NetInterestIncome(b, loanRate, depositRate) - LoanLossProvision(b, lossGivenDefault);

        // ----- BANK-5 流動性と取り付け -----

        /// <summary>流動性比率＝準備金/預金（手元現金がどれだけ預金を賄えるか）。預金0以下は1.0。</summary>
        public static float LiquidityRatio(Bank b)
        {
            if (b == null || b.deposits <= 0f) return 1f;
            return Mathf.Max(0f, b.reserves) / b.deposits;
        }

        /// <summary>引き出し要求を準備金で賄えるか＝準備金 ≥ 預金×引き出し割合（満期変換のリスク＝賄えないと取り付け）。</summary>
        public static bool CanCoverWithdrawal(Bank b, float withdrawalFraction)
            => b != null && Mathf.Max(0f, b.reserves) >= Mathf.Max(0f, b.deposits) * Mathf.Clamp01(withdrawalFraction);
    }
}
