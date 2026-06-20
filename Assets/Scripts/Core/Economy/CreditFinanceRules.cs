using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 信販＝消費者信用のロジック（#1996 SHIN・純ロジック・唯一の窓口）。消費者と加盟店の間に立ち、個人へ少額・多数の与信を
    /// する：割賦販売・立替払い（SHIN-1）／カードと加盟店手数料（SHIN-2）／リボルビング（SHIN-3）／与信審査と貸倒れ
    /// （SHIN-4）／信用保証（SHIN-5）／債権の証券化（SHIN-6）。消費者の所得（#1969）・消費 C（#1951）・銀行（#186）・与信
    /// （#185）・証券化（#1963/#161）・危機（#1939）へ接続（read-only/接続のみ）。マクロ近似。test-first。
    /// </summary>
    public static class CreditFinanceRules
    {
        /// <summary>既定の分割手数料率（消費者が払う上乗せ）。</summary>
        public const float DefaultInstallmentFeeRate = 0.12f;

        /// <summary>既定の加盟店手数料率（カード/立替で加盟店が払う）。</summary>
        public const float DefaultMerchantFeeRate = 0.03f;

        /// <summary>既定のリボルビング月利（高金利）。</summary>
        public const float DefaultRevolvingRate = 0.015f;

        /// <summary>リボルビングの最低支払率（残高に対する最低支払の割合）。</summary>
        public const float MinPaymentRatio = 0.05f;

        /// <summary>既定の信用保証料率。</summary>
        public const float DefaultGuaranteeFeeRate = 0.02f;

        // ===== SHIN-1 割賦販売・立替払い =====

        /// <summary>分割払いの1回あたり支払＝購入額×(1＋手数料率)/分割回数。回数≤0は0。</summary>
        public static float InstallmentPayment(float principal, float feeRate, int termPeriods)
        {
            if (termPeriods <= 0) return 0f;
            return Mathf.Max(0f, principal) * (1f + Mathf.Max(0f, feeRate)) / termPeriods;
        }

        /// <summary>分割手数料収入＝購入額×手数料率（信販が消費者から得る上乗せ）。</summary>
        public static float ConsumerFeeIncome(float principal, float feeRate)
            => Mathf.Max(0f, principal) * Mathf.Max(0f, feeRate);

        /// <summary>加盟店への立替額＝購入額×(1−加盟店手数料率)（手数料を引いて加盟店へ払う）。</summary>
        public static float MerchantAdvance(float principal, float merchantFeeRate)
            => Mathf.Max(0f, principal) * (1f - Mathf.Clamp01(merchantFeeRate));

        /// <summary>加盟店手数料収入＝購入額×加盟店手数料率（信販が加盟店から得る取り分）。</summary>
        public static float MerchantFeeIncome(float principal, float merchantFeeRate)
            => Mathf.Max(0f, principal) * Mathf.Clamp01(merchantFeeRate);

        // ===== SHIN-2 クレジットカードと加盟店手数料 =====

        /// <summary>カード決済の加盟店手数料＝決済額×加盟店手数料率（信販の主収益。消費者は一括なら手数料なし）。</summary>
        public static float CardTransactionFee(float transactionAmount, float merchantFeeRate)
            => Mathf.Max(0f, transactionAmount) * Mathf.Clamp01(merchantFeeRate);

        /// <summary>利用可能枠＝信用限度−現在残高（これ以上は使えない）。非負。</summary>
        public static float AvailableCredit(float creditLimit, float currentBalance)
            => Mathf.Max(0f, creditLimit - Mathf.Max(0f, currentBalance));

        // ===== SHIN-3 リボルビング =====

        /// <summary>リボ金利＝残高×月利（高金利が残高に付く）。</summary>
        public static float RevolvingInterest(float balance, float monthlyRate)
            => Mathf.Max(0f, balance) * Mathf.Max(0f, monthlyRate);

        /// <summary>最低支払額＝残高×最低支払率（毎月これだけは払う）。</summary>
        public static float MinimumPayment(float balance, float minRatio)
            => Mathf.Max(0f, balance) * Mathf.Clamp01(minRatio);

        /// <summary>支払後の残高＝残高＋利息−支払（支払が利息＋元本に満たないと残高が膨らむ）。非負。</summary>
        public static float BalanceAfterPayment(float balance, float payment, float interest)
            => Mathf.Max(0f, Mathf.Max(0f, balance) + Mathf.Max(0f, interest) - Mathf.Max(0f, payment));

        /// <summary>リボの罠か＝支払額が利息以下＝元本が減らず残高が永遠に縮まない（雪だるま式）。</summary>
        public static bool IsDebtTrap(float payment, float interest)
            => payment <= interest;

        // ===== SHIN-4 与信審査と貸倒れ =====

        /// <summary>与信限度額＝所得×限度比率（所得の何割まで貸すか）。</summary>
        public static float CreditLimit(float income, float limitRatio)
            => Mathf.Max(0f, income) * Mathf.Max(0f, limitRatio);

        /// <summary>与信承認できるか＝要求枠が限度額以内（甘い limitRatio は売上↑だが貸倒れ↑）。</summary>
        public static bool CanApprove(float requestedLimit, float income, float limitRatio)
            => requestedLimit <= CreditLimit(income, limitRatio);

        /// <summary>延滞による損失＝残高×デフォルト率。</summary>
        public static float DelinquencyLoss(float balance, float defaultRate)
            => Mathf.Max(0f, balance) * Mathf.Clamp01(defaultRate);

        /// <summary>貸倒償却＝残高×(1−回収率)（回収しきれず償却する純損失）。</summary>
        public static float ChargeOff(float balance, float recoveryRate)
            => Mathf.Max(0f, balance) * (1f - Mathf.Clamp01(recoveryRate));

        // ===== SHIN-5 信用保証 =====

        /// <summary>保証料収入＝保証するローン額×保証料率（銀行 #186 のリスクを引き取る対価）。</summary>
        public static float GuaranteeFee(float loanAmount, float feeRate)
            => Mathf.Max(0f, loanAmount) * Mathf.Max(0f, feeRate);

        /// <summary>代位弁済の純損失＝ローン額−借り手からの回収（借り手が焦げ付き信販が銀行へ肩代わり、回収しきれない分が損失）。非負。</summary>
        public static float Subrogation(float loanAmount, float recoveredFromBorrower)
            => Mathf.Max(0f, Mathf.Max(0f, loanAmount) - Mathf.Max(0f, recoveredFromBorrower));

        /// <summary>保証エクスポージャ＝保証残高（at risk＝一斉焦げ付きで信販が負う最大）。</summary>
        public static float GuaranteeExposure(float guaranteedLoans)
            => Mathf.Max(0f, guaranteedLoans);

        // ===== SHIN-6 債権の証券化・資金調達 =====

        /// <summary>証券化で即資金化＝売掛債権×掛け目（掛け目ぶん割り引いて資金を得る・#185/#1963）。</summary>
        public static float SecuritizeReceivables(float receivables, float advanceRate)
            => Mathf.Max(0f, receivables) * Mathf.Clamp01(advanceRate);

        /// <summary>調達コスト＝証券化額×調達金利。</summary>
        public static float FundingCost(float securitizedAmount, float fundingRate)
            => Mathf.Max(0f, securitizedAmount) * Mathf.Max(0f, fundingRate);

        /// <summary>利鞘＝債権利回り−調達金利（信販の儲けの源。負なら逆鞘）。</summary>
        public static float NetSpread(float receivableYield, float fundingRate)
            => receivableYield - fundingRate;
    }
}
