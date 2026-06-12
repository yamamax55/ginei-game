using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>通信会社（#2024・<see cref="TelecomRules"/>）：サービス収益(TEL-1)・純加入者(TEL-2)・チャーン(TEL-3)・LTV(TEL-4)。</summary>
    public class TelecomTests
    {
        [Test]
        public void Revenue_AndSubscribers()
        {
            Assert.AreEqual(50000f, TelecomRules.ServiceRevenue(1000f, 50f), 1e-1f);
            // 既存1000＋新規200−解約100 = 1100
            Assert.AreEqual(1100f, TelecomRules.NetSubscribers(1000f, 200f, 0.1f), 1e-3f);
        }

        [Test]
        public void Churn_AndLifetimeValue()
        {
            Assert.AreEqual(5000f, TelecomRules.ChurnLoss(1000f, 0.1f, 50f), 1e-1f);
            Assert.AreEqual(500f, TelecomRules.CustomerLifetimeValue(50f, 0.1f), 1e-3f); // 解約低いほどLTV高い
        }
    }
}
