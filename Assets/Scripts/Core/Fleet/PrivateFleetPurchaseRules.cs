using UnityEngine;

namespace Ginei
{
    /// <summary>私兵の艦艇購入の費用/与信パラメータ。</summary>
    public readonly struct ShipPurchaseParams
    {
        public readonly float unitPrice;       // 艦艇1隻の本体価格
        public readonly float baseCreditLimit; // 私財担保の基礎与信枠（現金0でもこれだけは借りられる）
        public readonly float creditMultiple;  // 私財（現金）に対する与信倍率

        public ShipPurchaseParams(float unitPrice, float baseCreditLimit, float creditMultiple)
        {
            this.unitPrice = unitPrice;
            this.baseCreditLimit = baseCreditLimit;
            this.creditMultiple = creditMultiple;
        }

        /// <summary>既定：1隻10／基礎与信5000／私財倍率2。</summary>
        public static ShipPurchaseParams Default => new ShipPurchaseParams(10f, 5000f, 2f);
    }

    /// <summary>
    /// 私兵の艦艇購入（商人を介し私有財産から支出・不足分は私財担保の借金）の純ロジック（決定論）。
    /// 総額（本体×数量×(1+商人口銭)）・私財からの支払額・借入額・与信枠・購入可否を算出する唯一の窓口。
    /// 財布（<see cref="Person.wealth"/>）・私兵（<see cref="Person.privateSoldiers"/>）・個人負債（<see cref="Person.personalDebt"/>）の
    /// 実増減や通知は呼び出し側（<see cref="PlayerFleetManagementOverlay"/>）が行い、本ルールは金額と可否だけを担う。test-first。
    /// </summary>
    public static class PrivateFleetPurchaseRules
    {
        /// <summary>総額＝本体価格×数量×(1+口銭率)。非負整数。</summary>
        public static int TotalCost(int quantity, float unitPrice, float commissionRate)
        {
            float c = Mathf.Max(0, quantity) * Mathf.Max(0f, unitPrice) * (1f + Mathf.Max(0f, commissionRate));
            return Mathf.Max(0, Mathf.RoundToInt(c));
        }

        /// <summary>商人の口銭（手数料）＝本体価格×数量×口銭率（非負整数・商社の利益）。</summary>
        public static int Commission(int quantity, float unitPrice, float commissionRate)
        {
            float c = Mathf.Max(0, quantity) * Mathf.Max(0f, unitPrice) * Mathf.Max(0f, commissionRate);
            return Mathf.Max(0, Mathf.RoundToInt(c));
        }

        /// <summary>私財（現金）から支払う額＝min(総額, 現金)。</summary>
        public static int CashPortion(int totalCost, float cash)
            => Mathf.RoundToInt(Mathf.Min(Mathf.Max(0, totalCost), Mathf.Max(0f, cash)));

        /// <summary>借金で賄う額＝max(0, 総額−現金)。</summary>
        public static int BorrowPortion(int totalCost, float cash)
            => Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, totalCost) - Mathf.Max(0f, cash)));

        /// <summary>私財担保の与信枠＝基礎枠＋現金×倍率（非負）。</summary>
        public static float CreditLimit(float wealth, ShipPurchaseParams p)
            => Mathf.Max(0f, p.baseCreditLimit + Mathf.Max(0f, wealth) * p.creditMultiple);

        /// <summary>購入可能か＝新規借入（総額−現金）が残り与信（与信枠−既存負債）に収まるか。</summary>
        public static bool CanPurchase(int totalCost, float cash, float existingDebt, float wealth, ShipPurchaseParams p)
        {
            int borrow = BorrowPortion(totalCost, cash);
            // 借入0（全額現金）なら与信に依らず可。借りる場合だけ残り与信（負はクランプ）に収まること。
            float remainingCredit = Mathf.Max(0f, CreditLimit(wealth, p) - Mathf.Max(0f, existingDebt));
            return borrow <= remainingCredit;
        }
    }
}
