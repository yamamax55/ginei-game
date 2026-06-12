using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>消費者金融（#2025・<see cref="MoneyLendingRules"/>）：総量規制(LEND-1)・利息(LEND-2)・過払い金(LEND-3)・貸倒れ(LEND-4)。</summary>
    public class MoneyLendingTests
    {
        [Test]
        public void Regulation_AndInterest()
        {
            Assert.AreEqual(1000f, MoneyLendingRules.MaxLending(3000f, 1f / 3f), 1e-1f); // 年収の1/3
            Assert.AreEqual(180f, MoneyLendingRules.LendingInterestIncome(1000f, 0.18f), 1e-3f);
        }

        [Test]
        public void Overpayment_AndChargeOff()
        {
            Assert.AreEqual(30f, MoneyLendingRules.OverpaymentRefund(1000f, 0.18f, 0.15f), 1e-3f); // グレーゾーン3%
            Assert.AreEqual(0f, MoneyLendingRules.OverpaymentRefund(1000f, 0.14f, 0.15f), 1e-3f);  // 上限以下＝返還なし
            Assert.AreEqual(80f, MoneyLendingRules.LendingChargeOff(1000f, 0.08f), 1e-3f);
        }
    }
}
