using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 惑星の不動産基盤（#2019 惑星層・<see cref="PlanetRealEstateRules"/>）を固定する：住宅供給余地（類型）・居住の魅力・
    /// 賃料・土地の希少性・地価・価格賃料倍率/バブル・賃料負担率・年次更新（値上がり/崩壊/開発）。
    /// </summary>
    public class PlanetRealEstateRulesTests
    {
        private static PlanetRealEstateRules.Params P => PlanetRealEstateRules.Params.Default;

        // ===== 住宅供給余地（類型） =====
        [Test]
        public void ResidentialCapacity_ByType()
        {
            // 居住が最大・鉱業が最小（残りが地価の希少性を生む）
            Assert.AreEqual(1.0f, PlanetRealEstateRules.ResidentialCapacity(SystemType.居住), 1e-4f);
            Assert.AreEqual(0.4f, PlanetRealEstateRules.ResidentialCapacity(SystemType.鉱業), 1e-4f);
            Assert.Greater(PlanetRealEstateRules.ResidentialCapacity(SystemType.農業),
                           PlanetRealEstateRules.ResidentialCapacity(SystemType.工業));
        }

        // ===== 居住の魅力 =====
        [Test]
        public void Desirability_HigherForNicePlanet()
        {
            float nice = PlanetRealEstateRules.Desirability(1f, 1f, 1f);
            float grim = PlanetRealEstateRules.Desirability(0.1f, 0.1f, 0.5f);
            Assert.AreEqual(1f, nice, 1e-4f);
            Assert.Less(grim, nice);
            // 0..1 にクランプ
            Assert.AreEqual(0f, PlanetRealEstateRules.Desirability(0f, 0f, 0f), 1e-4f);
            Assert.AreEqual(1f, PlanetRealEstateRules.Desirability(2f, 2f, 2f), 1e-4f);
        }

        // ===== 賃料 =====
        [Test]
        public void RentLevel_ScalesWithPopulationAndDesirability()
        {
            float lowDes = PlanetRealEstateRules.RentLevel(100f, 0f, P);   // 100×0.1×0.5
            float highDes = PlanetRealEstateRules.RentLevel(100f, 1f, P);  // 100×0.1×1.0
            Assert.AreEqual(5f, lowDes, 1e-4f);
            Assert.AreEqual(10f, highDes, 1e-4f);
            // 人口比例
            Assert.AreEqual(20f, PlanetRealEstateRules.RentLevel(200f, 1f, P), 1e-4f);
            // 人口0は賃料0
            Assert.AreEqual(0f, PlanetRealEstateRules.RentLevel(0f, 1f, P), 1e-4f);
        }

        // ===== 土地の希少性 =====
        [Test]
        public void LandDemandPressure_HighWhenWantedButScarce()
        {
            // 魅力高×供給手狭（鉱業0.4）＝希少性高、魅力高×供給潤沢（居住1.0）＝低め
            float scarce = PlanetRealEstateRules.LandDemandPressure(1f, 0.4f);
            float ample = PlanetRealEstateRules.LandDemandPressure(1f, 1.0f);
            Assert.Greater(scarce, ample);
            Assert.AreEqual(1f, ample, 1e-4f);      // 1.0/1.0
            // 0.5..2.0 にクランプ
            Assert.AreEqual(2.0f, PlanetRealEstateRules.LandDemandPressure(1f, 0.1f), 1e-4f);
            Assert.AreEqual(0.5f, PlanetRealEstateRules.LandDemandPressure(0.1f, 1f), 1e-4f);
        }

        // ===== 地価・価格賃料倍率 =====
        [Test]
        public void LandValue_AndPriceToRent()
        {
            float rent = 10f;
            float value = PlanetRealEstateRules.LandValue(rent, 1f, P); // 10×15×1.0
            Assert.AreEqual(150f, value, 1e-4f);
            var re = new PlanetRealEstate { rentLevel = rent, landValue = value };
            Assert.AreEqual(15f, PlanetRealEstateRules.PriceToRent(re), 1e-4f); // 基準＝評価倍率
            Assert.IsFalse(PlanetRealEstateRules.IsBubble(re, P));              // 15 < 25
            // 地価指数が上がると割高＝バブル
            re.landValue = PlanetRealEstateRules.LandValue(rent, 2f, P);        // 倍率30
            Assert.AreEqual(30f, PlanetRealEstateRules.PriceToRent(re), 1e-4f);
            Assert.IsTrue(PlanetRealEstateRules.IsBubble(re, P));
        }

        [Test]
        public void PriceToRent_ZeroRentIsHuge()
        {
            var re = new PlanetRealEstate { rentLevel = 0f, landValue = 100f };
            Assert.Greater(PlanetRealEstateRules.PriceToRent(re), 1000f);
        }

        // ===== 賃料負担率 =====
        [Test]
        public void RentToIncome_AffordabilityGate()
        {
            Assert.AreEqual(0.5f, PlanetRealEstateRules.RentToIncome(0.5f, 1f), 1e-4f);
            Assert.IsTrue(PlanetRealEstateRules.IsAffordable(0.3f, 0.4f));
            Assert.IsFalse(PlanetRealEstateRules.IsAffordable(0.5f, 0.4f));
            // 賃金0は超大（住みづらい）
            Assert.Greater(PlanetRealEstateRules.RentToIncome(1f, 0f), 1000f);
        }

        // ===== Ensure（冪等） =====
        [Test]
        public void Ensure_IdempotentAndDerivesFromProvince()
        {
            var prov = new Province(1, "民主", 100f) { stability = 100f, livingStandard = 1f, integration = 1f };
            PlanetRealEstate re = PlanetRealEstateRules.Ensure(prov, P);
            Assert.IsNotNull(re);
            Assert.AreSame(re, prov.realEstate);
            Assert.Greater(re.rentLevel, 0f);
            Assert.Greater(re.landValue, 0f);
            // 2回目は同じインスタンス（冪等）
            Assert.AreSame(re, PlanetRealEstateRules.Ensure(prov, P));
            // null は安全
            Assert.IsNull(PlanetRealEstateRules.Ensure(null, P));
        }

        // ===== 年次更新：平時は値上がり =====
        [Test]
        public void TickYear_PeacetimeAppreciatesPriceIndex()
        {
            var prov = new Province(1, "民主", 100f) { stability = 80f, livingStandard = 0.8f, integration = 1f, systemType = SystemType.鉱業 };
            PlanetRealEstateRules.TickYear(prov, P, crisis: false);
            float idx1 = prov.realEstate.priceIndex;
            Assert.Greater(idx1, 1f); // 平時は値上がり
            PlanetRealEstateRules.TickYear(prov, P, crisis: false);
            Assert.Greater(prov.realEstate.priceIndex, idx1); // 複利で更に
            // 地価＝賃料×倍率×指数
            float expected = PlanetRealEstateRules.LandValue(prov.realEstate.rentLevel, prov.realEstate.priceIndex, P);
            Assert.AreEqual(expected, prov.realEstate.landValue, 1e-3f);
        }

        // ===== 年次更新：危機で崩壊 =====
        [Test]
        public void TickYear_CrisisCrashesPriceIndex()
        {
            var prov = new Province(1, "民主", 100f) { stability = 80f, livingStandard = 0.8f };
            PlanetRealEstateRules.TickYear(prov, P, crisis: false);
            float boom = prov.realEstate.priceIndex;
            PlanetRealEstateRules.TickYear(prov, P, crisis: true);
            Assert.Less(prov.realEstate.priceIndex, boom); // 崩壊で下落
        }

        // ===== 年次更新：指数は上下限でクランプ（runaway 回避） =====
        [Test]
        public void TickYear_PriceIndexBounded()
        {
            var prov = new Province(1, "民主", 100f) { stability = 100f, livingStandard = 1f, integration = 1f, systemType = SystemType.鉱業 };
            for (int i = 0; i < 500; i++) PlanetRealEstateRules.TickYear(prov, P, crisis: false);
            Assert.LessOrEqual(prov.realEstate.priceIndex, P.maxPriceIndex + 1e-3f);
            for (int i = 0; i < 500; i++) PlanetRealEstateRules.TickYear(prov, P, crisis: true);
            Assert.GreaterOrEqual(prov.realEstate.priceIndex, P.minPriceIndex - 1e-3f);
        }

        // ===== 年次更新：開発が需要へ向かう =====
        [Test]
        public void TickYear_DevelopedRatioMovesTowardDesirability()
        {
            var prov = new Province(1, "民主", 100f) { stability = 100f, livingStandard = 1f, integration = 1f };
            var re = PlanetRealEstateRules.Ensure(prov, P);
            re.developedRatio = 0f;
            PlanetRealEstateRules.TickYear(prov, P, crisis: false);
            Assert.Greater(prov.realEstate.developedRatio, 0f); // 需要（魅力1.0）へ向けて進む
            Assert.LessOrEqual(prov.realEstate.developedRatio, 1f);
        }

        [Test]
        public void TickYear_NullSafe()
        {
            Assert.DoesNotThrow(() => PlanetRealEstateRules.TickYear(null, P, false));
        }
    }
}
