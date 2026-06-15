using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 資産担保融資のロジック（#186/#2070 資産担保・唯一の窓口）。資産（土地等）を担保に借り入れる＝担保評価に対する借入上限
    /// （<b>LTV＝loan-to-value</b>）、金利の発生、担保価値が借入を割ったときの<b>差し押さえ（foreclosure）</b>を扱う。借入は信用#186 を
    /// 資産で裏付ける＝財産を流動化する手段。資金の決済（借入金の受け取り・利払い・差し押さえの資産移転）は呼び側＝ここは
    /// 与信額/金利/差し押さえ判定の算定に徹する。台帳は <see cref="CollateralLoanRegistry"/>。集約で扱い個別審査へ降りない（タイクン化回避）。test-first。
    /// </summary>
    public static class CollateralLoanRules
    {
        /// <summary>調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct CollateralParams
        {
            public readonly float maxLtv;            // 借入上限＝担保評価×これ（loan-to-value）
            public readonly float marginCallLtv;     // 借入/担保 がこれを超えたら差し押さえ（担保割れ）
            public readonly float foreclosureHaircut; // 差し押さえ時の回収減価（投げ売りで目減り）

            public CollateralParams(float maxLtv, float marginCallLtv, float foreclosureHaircut)
            {
                this.maxLtv = Mathf.Clamp01(maxLtv);
                this.marginCallLtv = Mathf.Max(this.maxLtv, Mathf.Clamp(marginCallLtv, 0.01f, 1.5f));
                this.foreclosureHaircut = Mathf.Clamp01(foreclosureHaircut);
            }

            /// <summary>既定＝LTV上限70%・担保割れ90%で差し押さえ・回収減価20%。</summary>
            public static CollateralParams Default => new CollateralParams(0.7f, 0.9f, 0.2f);
        }

        /// <summary>借入上限＝担保評価×LTV上限。</summary>
        public static float MaxBorrow(float collateralValue, float maxLtv)
            => Mathf.Max(0f, collateralValue) * Mathf.Clamp01(maxLtv);

        /// <summary>LTV比＝借入残高/担保評価（高いほど担保に対し借りすぎ）。担保0以下は超大。</summary>
        public static float LoanToValue(float principal, float collateralValue)
            => collateralValue <= 0f ? 999999f : Mathf.Max(0f, principal) / collateralValue;

        /// <summary>担保割れ＝LTV比が差し押さえ閾値を超える（担保価値が借入を裏付けられない）。</summary>
        public static bool IsUnderwater(float principal, float collateralValue, CollateralParams p)
            => LoanToValue(principal, collateralValue) > p.marginCallLtv;

        /// <summary>年間利息＝借入残高×金利（非負）。</summary>
        public static float AnnualInterest(float principal, float rate)
            => Mathf.Max(0f, principal) * Mathf.Max(0f, rate);

        /// <summary>差し押さえの回収額＝担保評価×(1−回収減価)（投げ売りで満額は回収できない）。</summary>
        public static float ForeclosureRecovery(float collateralValue, CollateralParams p)
            => Mathf.Max(0f, collateralValue) * (1f - p.foreclosureHaircut);

        /// <summary>
        /// 担保ローンを組成（借入額は <see cref="MaxBorrow"/> でクランプ・正のときのみ）。担保評価・借り手/貸し手・金利・担保物件を刻んで登録。
        /// 借入金の受け渡しは呼び側（戻り値の principal を借り手へ・貸し手の与信へ）。担保割れ/未満は null。
        /// </summary>
        public static CollateralLoan Borrow(OwnerRef borrower, OwnerRef lender, float requestedAmount,
            float collateralValue, float interestRate, int pledgedSystemId, CollateralParams p)
        {
            float cap = MaxBorrow(collateralValue, p.maxLtv);
            float amount = Mathf.Min(Mathf.Max(0f, requestedAmount), cap);
            if (amount <= 0f) return null;
            var loan = new CollateralLoan
            {
                borrowerKind = borrower.kind, borrowerPersonId = borrower.personId, borrowerFaction = borrower.faction, borrowerEnterpriseId = borrower.enterpriseId,
                lenderKind = lender.kind, lenderPersonId = lender.personId, lenderFaction = lender.faction, lenderEnterpriseId = lender.enterpriseId,
                principal = amount,
                interestRate = Mathf.Max(0f, interestRate),
                collateralValue = Mathf.Max(0f, collateralValue),
                pledgedSystemId = pledgedSystemId,
            };
            return CollateralLoanRegistry.Register(loan);
        }

        /// <summary>返済＝残高を減らす（実際に減った額を返す・残高を超えない）。</summary>
        public static float Repay(CollateralLoan loan, float amount)
        {
            if (loan == null) return 0f;
            float paid = Mathf.Clamp(amount, 0f, Mathf.Max(0f, loan.principal));
            loan.principal -= paid;
            return paid;
        }
    }
}
