using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>決済・キャッシュレス（#2025・<see cref="PaymentRules"/>）：加盟店手数料(PAY-1)・ネットワーク料(PAY-2)・フロート(PAY-3)・利益(PAY-4)。</summary>
    public class PaymentTests
    {
        [Test]
        public void Fees_AndFloat()
        {
            Assert.AreEqual(30000f, PaymentRules.MerchantFeeRevenue(1000000f, 0.03f), 1e-1f); // 取扱高×手数料率
            Assert.AreEqual(10000f, PaymentRules.NetworkFeeRevenue(100000, 0.1f), 1e-1f);
            Assert.AreEqual(2000f, PaymentRules.FloatIncome(100000f, 0.02f), 1e-2f); // チャージ残高の運用益
        }

        [Test]
        public void Profit()
        {
            Assert.AreEqual(15000f, PaymentRules.PaymentProfit(30000f, 10000f, 5000f), 1e-1f);
        }
    }
}
