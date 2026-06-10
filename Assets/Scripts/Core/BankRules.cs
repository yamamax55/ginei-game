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
    }
}
