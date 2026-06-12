using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>暗号資産・分散金融（#2025・<see cref="CryptoAssetRules"/>）：採掘報酬(CRYP-1)・時価(CRYP-2)・ドローダウン(CRYP-3)・取引所手数料(CRYP-4)。</summary>
    public class CryptoAssetTests
    {
        [Test]
        public void Reward_AndMarketValue()
        {
            Assert.AreEqual(100f, CryptoAssetRules.MiningReward(0.1f, 1000f), 1e-3f); // ハッシュシェア10%
            Assert.AreEqual(50000f, CryptoAssetRules.MarketValue(1000f, 50f), 1e-1f);
        }

        [Test]
        public void Drawdown_AndFee()
        {
            Assert.AreEqual(0.4f, CryptoAssetRules.VolatilityDrawdown(50000f, 30000f), 1e-4f); // 高値から4割暴落
            Assert.AreEqual(1000f, CryptoAssetRules.TradingFeeRevenue(1000000f, 0.001f), 1e-2f);
        }
    }
}
