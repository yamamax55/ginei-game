using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>私兵艦艇購入：総額（口銭込み）・私財/借入の内訳・与信枠・購入可否。</summary>
    public class PrivateFleetPurchaseRulesTests
    {
        private static readonly ShipPurchaseParams P = ShipPurchaseParams.Default; // 1隻10／基礎与信5000／倍率2

        [Test]
        public void TotalCost_PriceTimesQuantityPlusCommission()
        {
            Assert.AreEqual(5150, PrivateFleetPurchaseRules.TotalCost(500, 10f, 0.03f)); // 5000×1.03
            Assert.AreEqual(5000, PrivateFleetPurchaseRules.TotalCost(500, 10f, 0f));
            Assert.AreEqual(0, PrivateFleetPurchaseRules.TotalCost(-5, 10f, 0.03f));
        }

        [Test]
        public void Commission_IsMerchantCut()
        {
            Assert.AreEqual(150, PrivateFleetPurchaseRules.Commission(500, 10f, 0.03f)); // 5000×0.03
        }

        [Test]
        public void CashAndBorrow_SplitByAvailableCash()
        {
            // 総額5150・現金3000＝現金3000＋借入2150。
            Assert.AreEqual(3000, PrivateFleetPurchaseRules.CashPortion(5150, 3000f));
            Assert.AreEqual(2150, PrivateFleetPurchaseRules.BorrowPortion(5150, 3000f));
            // 現金潤沢＝全額現金・借入0。
            Assert.AreEqual(5150, PrivateFleetPurchaseRules.CashPortion(5150, 9000f));
            Assert.AreEqual(0, PrivateFleetPurchaseRules.BorrowPortion(5150, 9000f));
        }

        [Test]
        public void CreditLimit_BasePlusWealthMultiple()
        {
            Assert.AreEqual(11000f, PrivateFleetPurchaseRules.CreditLimit(3000f, P), 1e-4f); // 5000+3000×2
            Assert.AreEqual(5000f, PrivateFleetPurchaseRules.CreditLimit(-100f, P), 1e-4f);  // 現金負は0扱い
        }

        [Test]
        public void CanPurchase_BorrowMustFitRemainingCredit()
        {
            // 借入2150 ≤ 与信11000−既存0 ＝ 可。
            Assert.IsTrue(PrivateFleetPurchaseRules.CanPurchase(5150, 3000f, 0f, 3000f, P));
            // 現金0・既存負債10900＝残り与信100。借入200>100 ＝ 不可。
            Assert.IsFalse(PrivateFleetPurchaseRules.CanPurchase(200, 0f, 10900f, 3000f, P));
            // 全額現金で賄えれば借入0＝常に可（与信を使わない）。
            Assert.IsTrue(PrivateFleetPurchaseRules.CanPurchase(5150, 9000f, 99999f, 0f, P));
        }
    }
}
