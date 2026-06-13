using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>環境・リサイクル（#2025・<see cref="RecyclingRules"/>）：処理委託料(RCYL-1)・回収資源(RCYL-2)・再生材売却(RCYL-3)・利益(RCYL-4)。</summary>
    public class RecyclingTests
    {
        [Test]
        public void Tipping_AndRecovery()
        {
            Assert.AreEqual(500000f, RecyclingRules.TippingFeeRevenue(10000f, 50f), 1e-1f); // 廃棄物を引き取って収入
            Assert.AreEqual(3000f, RecyclingRules.RecoveredMaterial(10000f, 0.3f), 1e-1f); // 回収率3割→資源#92へ
        }

        [Test]
        public void MaterialSales_AndProfit()
        {
            Assert.AreEqual(240000f, RecyclingRules.MaterialSalesRevenue(3000f, 80f), 1e-1f); // 都市鉱山
            Assert.AreEqual(200000f, RecyclingRules.RecyclingProfit(500000f, 240000f, 400000f, 140000f), 1e-1f);
        }
    }
}
