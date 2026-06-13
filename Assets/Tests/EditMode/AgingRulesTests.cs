using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>CDR-5 加齢の能力曲線：総合（全盛期1.0）・機動（若く高い）・知略（円熟で高い）。</summary>
    public class AgingRulesTests
    {
        [Test]
        public void GeneralAgingFactor_PrimeIsPeak()
        {
            Assert.AreEqual(0.85f, AgingRules.GeneralAgingFactor(20), 1e-4f);
            Assert.AreEqual(0.85f, AgingRules.GeneralAgingFactor(25), 1e-4f);
            Assert.AreEqual(1.0f, AgingRules.GeneralAgingFactor(40), 1e-4f);
            Assert.AreEqual(1.0f, AgingRules.GeneralAgingFactor(55), 1e-4f);
            Assert.AreEqual(0.75f, AgingRules.GeneralAgingFactor(70), 1e-4f);
            Assert.AreEqual(0.75f, AgingRules.GeneralAgingFactor(80), 1e-4f);
        }

        [Test]
        public void MobilityDeclines_WisdomRises()
        {
            Assert.AreEqual(1.15f, AgingRules.MobilityAgingFactor(30), 1e-4f);
            Assert.AreEqual(0.975f, AgingRules.MobilityAgingFactor(45), 1e-4f);
            Assert.AreEqual(0.8f, AgingRules.MobilityAgingFactor(60), 1e-4f);

            Assert.AreEqual(0.85f, AgingRules.WisdomAgingFactor(25), 1e-4f);
            Assert.AreEqual(1.0f, AgingRules.WisdomAgingFactor(40), 1e-4f);
            Assert.AreEqual(1.15f, AgingRules.WisdomAgingFactor(55), 1e-4f);
        }

        [Test]
        public void IsPrime_Range()
        {
            Assert.IsTrue(AgingRules.IsPrime(40));
            Assert.IsTrue(AgingRules.IsPrime(55));
            Assert.IsFalse(AgingRules.IsPrime(39));
            Assert.IsFalse(AgingRules.IsPrime(56));
        }
    }
}
