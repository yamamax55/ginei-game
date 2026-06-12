using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 消費者金融（サラ金）のロジック（業種細分化・信販 #1996 の無担保少額融資サブ業種・#2025・純ロジック・唯一の窓口）：総量規制＝年収の1/3まで（LEND-1）／
    /// 高金利の利息収入（LEND-2）／過払い金＝法定上限超過分の返還（LEND-3）／貸倒れ（LEND-4）。
    /// 信販（#1996）より高リスク・高金利＝所得（#1969）以上に前借りさせ消費C（#1951）を膨らませるが、貸倒れは#1939の火種。マクロ近似。test-first。
    /// </summary>
    public static class MoneyLendingRules
    {
        /// <summary>総量規制の貸付上限＝年収×規制割合（既定1/3）。多重債務を防ぐ与信枠。</summary>
        public static float MaxLending(float annualIncome, float regulationFraction)
            => Mathf.Max(0f, annualIncome) * Mathf.Max(0f, regulationFraction);

        /// <summary>利息収入＝貸付残高×年利（無担保ゆえ高金利）。</summary>
        public static float LendingInterestIncome(float loanBalance, float annualRate)
            => Mathf.Max(0f, loanBalance) * Mathf.Max(0f, annualRate);

        /// <summary>過払い金＝残高×max(0, 約定金利−法定上限金利)（グレーゾーン金利の遡及返還＝業界を傾けた負債）。</summary>
        public static float OverpaymentRefund(float loanBalance, float chargedRate, float legalCapRate)
            => Mathf.Max(0f, loanBalance) * Mathf.Max(0f, chargedRate - legalCapRate);

        /// <summary>貸倒れ＝残高×貸倒率（高金利は高デフォルトと裏表）。</summary>
        public static float LendingChargeOff(float loanBalance, float defaultRate)
            => Mathf.Max(0f, loanBalance) * Mathf.Clamp01(defaultRate);
    }
}
