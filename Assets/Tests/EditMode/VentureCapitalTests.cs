using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ベンチャーキャピタル（#2025・<see cref="VentureCapitalRules"/>）：評価額(VC-1)・パワーロー(VC-2)・ヒット率(VC-3)・ファンド倍率(VC-4)。</summary>
    public class VentureCapitalTests
    {
        [Test]
        public void Portfolio_PowerLaw()
        {
            var exits = new List<float> { 0f, 0f, 0f, 10f }; // 3社全損・1社10倍
            Assert.AreEqual(1000f, VentureCapitalRules.PortfolioValue(exits, 100f), 1e-3f);
            Assert.AreEqual(1.0f, VentureCapitalRules.TopDealContribution(exits), 1e-3f); // 1社が全リターン
        }

        [Test]
        public void HitRate_AndFundMultiple()
        {
            Assert.AreEqual(0.25f, VentureCapitalRules.HitRate(1, 4), 1e-4f);
            Assert.AreEqual(2.5f, VentureCapitalRules.FundMultiple(1000f, 400f), 1e-3f);
        }
    }
}
