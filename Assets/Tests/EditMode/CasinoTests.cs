using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>カジノ（#2025・<see cref="CasinoRules"/>）：ゲーミング収入(CASI-1)・コンプ(CASI-2)・信用貸倒れ(CASI-3)・利益(CASI-4)。</summary>
    public class CasinoTests
    {
        [Test]
        public void Gaming_AndComp()
        {
            Assert.AreEqual(15000000f, CasinoRules.GamingRevenue(100000000f, 0.15f), 1e1f); // ハウスエッジ15%
            Assert.AreEqual(3000000f, CasinoRules.VipComplimentaryCost(10000000f, 0.3f), 1e1f);
        }

        [Test]
        public void Credit_AndProfit()
        {
            Assert.AreEqual(500000f, CasinoRules.CreditChargeOff(5000000f, 0.1f), 1e-1f); // VIP信用の焦げ付き
            Assert.AreEqual(10000000f, CasinoRules.CasinoProfit(15000000f, 5000000f, 3000000f, 7000000f), 1e1f);
        }
    }
}
