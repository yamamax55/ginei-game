using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 再分配＝税の階級別負担（#163 #162）を固定する：累進度、実効税率、層別税収、累進/逆進による各層支持の変化、
    /// 階級対立、`FiscalRules.TaxRevenue` への合流。
    /// </summary>
    public class RedistributionRulesTests
    {
        private static TaxStructure Progressive()
            => new TaxStructure { richRate = 0.5f, middleRate = 0.25f, poorRate = 0.1f };

        private static TaxStructure Regressive()
            => new TaxStructure { richRate = 0.1f, middleRate = 0.2f, poorRate = 0.4f };

        [Test]
        public void Progressivity_SignedRichMinusPoor()
        {
            Assert.Greater(RedistributionRules.Progressivity(Progressive()), 0f); // 累進
            Assert.Less(RedistributionRules.Progressivity(Regressive()), 0f);     // 逆進
        }

        [Test]
        public void EffectiveTaxRate_WealthWeighted()
        {
            var t = Progressive(); // 富シェア 0.5/0.35/0.15
            // 0.5*0.5 + 0.35*0.25 + 0.15*0.1 = 0.25 + 0.0875 + 0.015 = 0.3525
            Assert.AreEqual(0.3525f, RedistributionRules.EffectiveTaxRate(t), 1e-4f);
        }

        [Test]
        public void ClassTax_AndTotalMatchEffective()
        {
            var t = Progressive();
            float taxBase = 1000f;
            float sum = RedistributionRules.ClassTax(t, FiscalClass.富裕層, taxBase)
                      + RedistributionRules.ClassTax(t, FiscalClass.中間層, taxBase)
                      + RedistributionRules.ClassTax(t, FiscalClass.貧困層, taxBase);
            Assert.AreEqual(RedistributionRules.TotalTax(t, taxBase), sum, 1e-3f);
        }

        [Test]
        public void ClassSupport_ProgressivePleasesPoor_AngersRich()
        {
            float prog = RedistributionRules.Progressivity(Progressive());
            Assert.Less(RedistributionRules.ClassSupportDelta(FiscalClass.富裕層, prog), 0f); // 富裕層 不満
            Assert.Greater(RedistributionRules.ClassSupportDelta(FiscalClass.貧困層, prog), 0f); // 貧困層 支持
        }

        [Test]
        public void ClassSupport_RegressiveFlips()
        {
            float prog = RedistributionRules.Progressivity(Regressive()); // 負
            Assert.Greater(RedistributionRules.ClassSupportDelta(FiscalClass.富裕層, prog), 0f); // 富裕層 喜ぶ
            Assert.Less(RedistributionRules.ClassSupportDelta(FiscalClass.貧困層, prog), 0f);    // 貧困層 不満
        }

        [Test]
        public void ClassTension_RisesWithPolarization()
        {
            float strong = RedistributionRules.ClassTension(RedistributionRules.Progressivity(Progressive())); // |0.4|
            float flat = RedistributionRules.ClassTension(0f); // 比例税＝対立小
            Assert.Greater(strong, flat);
            Assert.AreEqual(0f, flat, 1e-4f);
        }

        [Test]
        public void FeedsFiscalRules_TaxRevenue()
        {
            var t = Progressive();
            float taxBase = 1000f;
            // 実効税率を FiscalRules.TaxRevenue へ渡すと TotalTax と一致＝財政(#161)へ合流
            float viaFiscal = FiscalRules.TaxRevenue(taxBase, RedistributionRules.EffectiveTaxRate(t));
            Assert.AreEqual(RedistributionRules.TotalTax(t, taxBase), viaFiscal, 1e-3f);
        }
    }
}
