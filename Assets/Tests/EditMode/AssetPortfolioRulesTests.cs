using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>資産運用：総資産・配分比率・純資産・集中度（HHI）・実効分散・レバレッジの決定論計算。</summary>
    public class AssetPortfolioRulesTests
    {
        private static readonly float[] Sample = { 50f, 30f, 20f, 0f, 0f }; // gross=100

        [Test]
        public void Gross_SumsNonNegative()
        {
            Assert.AreEqual(100f, AssetPortfolioRules.Gross(Sample), 1e-4f);
            Assert.AreEqual(50f, AssetPortfolioRules.Gross(new[] { 50f, -20f }), 1e-4f); // 負は0扱い＝50+0
            Assert.AreEqual(0f, AssetPortfolioRules.Gross(null), 1e-4f);
        }

        [Test]
        public void Fraction_ClampedAndZeroOnEmptyGross()
        {
            Assert.AreEqual(0.5f, AssetPortfolioRules.Fraction(50f, 100f), 1e-4f);
            Assert.AreEqual(0f, AssetPortfolioRules.Fraction(50f, 0f), 1e-4f);
            Assert.AreEqual(1f, AssetPortfolioRules.Fraction(150f, 100f), 1e-4f); // クランプ
            Assert.AreEqual(0f, AssetPortfolioRules.Fraction(-10f, 100f), 1e-4f); // 負は0
        }

        [Test]
        public void NetWorth_SubtractsLiabilities_CanGoNegative()
        {
            Assert.AreEqual(60f, AssetPortfolioRules.NetWorth(100f, 40f), 1e-4f);
            Assert.AreEqual(-50f, AssetPortfolioRules.NetWorth(100f, 150f), 1e-4f); // 債務超過
            Assert.AreEqual(100f, AssetPortfolioRules.NetWorth(100f, -5f), 1e-4f);  // 負債は0下限
        }

        [Test]
        public void LeverageRatio_DebtOverAssets()
        {
            Assert.AreEqual(0.4f, AssetPortfolioRules.LeverageRatio(40f, 100f), 1e-4f);
            Assert.AreEqual(0f, AssetPortfolioRules.LeverageRatio(0f, 100f), 1e-4f);
            Assert.AreEqual(1f, AssetPortfolioRules.LeverageRatio(50f, 0f), 1e-4f); // 資産0＋負債あり
            Assert.AreEqual(0f, AssetPortfolioRules.LeverageRatio(0f, 0f), 1e-4f);
        }

        [Test]
        public void Herfindahl_And_EffectiveClasses()
        {
            // 0.5²+0.3²+0.2² = 0.25+0.09+0.04 = 0.38
            Assert.AreEqual(0.38f, AssetPortfolioRules.Herfindahl(Sample), 1e-4f);
            Assert.AreEqual(1f / 0.38f, AssetPortfolioRules.EffectiveClasses(Sample), 1e-3f);

            // 一極集中＝HHI 1・実効1クラス。
            Assert.AreEqual(1f, AssetPortfolioRules.Herfindahl(new[] { 100f, 0f, 0f }), 1e-4f);
            Assert.AreEqual(1f, AssetPortfolioRules.EffectiveClasses(new[] { 100f, 0f, 0f }), 1e-4f);

            // 均等4分散＝HHI 0.25・実効4クラス。
            Assert.AreEqual(0.25f, AssetPortfolioRules.Herfindahl(new[] { 25f, 25f, 25f, 25f }), 1e-4f);
            Assert.AreEqual(4f, AssetPortfolioRules.EffectiveClasses(new[] { 25f, 25f, 25f, 25f }), 1e-4f);

            // 空は0。
            Assert.AreEqual(0f, AssetPortfolioRules.Herfindahl(new[] { 0f, 0f }), 1e-4f);
            Assert.AreEqual(0f, AssetPortfolioRules.EffectiveClasses(null), 1e-4f);
        }
    }
}
