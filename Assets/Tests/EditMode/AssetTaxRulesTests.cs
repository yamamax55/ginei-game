using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 財産税（#163/#917・<see cref="AssetTaxRules"/>）を固定する：固定資産税・富裕税（累進）・譲渡益課税・相続税（累進）。
    /// </summary>
    public class AssetTaxRulesTests
    {
        private static AssetTaxRules.AssetTaxParams P => AssetTaxRules.AssetTaxParams.Default;

        [Test]
        public void PropertyTax_RateOnAssessedValue()
        {
            Assert.AreEqual(50f, AssetTaxRules.PropertyTax(5000f, 0.01f), 1e-3f);
            Assert.AreEqual(0f, AssetTaxRules.PropertyTax(-100f, 0.01f), 1e-3f); // 非負
        }

        [Test]
        public void WealthTax_OnlyAboveExemption_Progressive()
        {
            Assert.AreEqual(20f, AssetTaxRules.WealthTax(3000f, 1000f, 0.01f), 1e-3f); // (3000-1000)*0.01
            Assert.AreEqual(0f, AssetTaxRules.WealthTax(800f, 1000f, 0.01f), 1e-3f);   // 控除以下は非課税
        }

        [Test]
        public void CapitalGainsTax_OnlyOnGain()
        {
            Assert.AreEqual(200f, AssetTaxRules.CapitalGainsTax(1000f, 0.2f), 1e-3f);
            Assert.AreEqual(0f, AssetTaxRules.CapitalGainsTax(-500f, 0.2f), 1e-3f); // 損失は課税しない
        }

        [Test]
        public void InheritanceTax_OnlyAboveExemption()
        {
            Assert.AreEqual(2400f, AssetTaxRules.InheritanceTax(10000f, 2000f, 0.3f), 1e-3f); // (10000-2000)*0.3
            Assert.AreEqual(0f, AssetTaxRules.InheritanceTax(1500f, 2000f, 0.3f), 1e-3f);
        }

        [Test]
        public void AnnualWealthAndProperty_IsSum()
        {
            // 評価5000→固定資産税50、純資産3000・控除1000→富裕税20、計70
            Assert.AreEqual(70f, AssetTaxRules.AnnualWealthAndPropertyTax(5000f, 3000f, 1000f, P), 1e-3f);
        }
    }
}
