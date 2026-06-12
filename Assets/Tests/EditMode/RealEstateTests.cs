using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 不動産会社（#2019・<see cref="RealEstateRules"/>）を固定する：土地と私有可否＝政体(RE-1)、賃貸収入と利回り(RE-2)、
    /// 売買と地価(RE-3)、開発(RE-4)、不動産バブル(RE-5)。
    /// </summary>
    public class RealEstateTests
    {
        // ===== RE-1 土地と私有可否（政体） =====
        [Test]
        public void Land_PrivatizableDependsOnRegime()
        {
            // 資本主義/民主は私有可、共産は国有＝私有地として保有・売買できない
            Assert.AreEqual(Ownership.私有, RealEstateRules.LandOwnership("民主"));
            Assert.AreEqual(Ownership.国有, RealEstateRules.LandOwnership("共産"));
            Assert.IsTrue(RealEstateRules.CanPrivatizeLand("民主"));
            Assert.IsFalse(RealEstateRules.CanPrivatizeLand("共産主義"));
            Assert.AreEqual(1000f, RealEstateRules.TradableLand(1000f, "民主"), 1e-3f);
            Assert.AreEqual(0f, RealEstateRules.TradableLand(1000f, "共産"), 1e-3f); // 共産では取引不可
        }

        // ===== RE-2 賃貸収入と利回り =====
        [Test]
        public void Rent_NoiAndCapRate()
        {
            Assert.AreEqual(900f, RealEstateRules.EffectiveRent(1000f, 0.1f), 1e-3f);  // 空室10%控除
            Assert.AreEqual(600f, RealEstateRules.NetOperatingIncome(900f, 300f), 1e-3f);
            Assert.AreEqual(0.06f, RealEstateRules.CapRate(600f, 10000f), 1e-4f);      // 利回り6%
            Assert.AreEqual(10000f, RealEstateRules.PropertyValueFromCap(600f, 0.06f), 1e-2f); // 収益還元法
        }

        // ===== RE-3 売買と地価 =====
        [Test]
        public void Trade_CapitalGainAndAppreciation()
        {
            Assert.AreEqual(2000f, RealEstateRules.CapitalGain(12000f, 10000f), 1e-3f);
            Assert.AreEqual(1050f, RealEstateRules.LandValueAfterAppreciation(1000f, 0.05f), 1e-3f);
            Assert.AreEqual(800f, RealEstateRules.LandValueAfterAppreciation(1000f, -0.2f), 1e-3f); // 地価下落
        }

        // ===== RE-4 開発 =====
        [Test]
        public void Development_ValueAdd()
        {
            // (地価1000＋開発費2000)×(1+0.2) = 3600
            Assert.AreEqual(3600f, RealEstateRules.DevelopedValue(1000f, 2000f, 0.2f), 1e-3f);
            Assert.AreEqual(600f, RealEstateRules.DevelopmentProfit(3600f, 1000f, 2000f), 1e-3f);
        }

        // ===== RE-5 不動産バブル =====
        [Test]
        public void Bubble_AndBurst()
        {
            Assert.AreEqual(20f, RealEstateRules.PriceToRentRatio(10000f, 500f), 1e-3f);
            Assert.IsFalse(RealEstateRules.IsBubble(20f, 25f));
            Assert.IsTrue(RealEstateRules.IsBubble(30f, 25f));            // 賃料の裏付けを超えた価格
            Assert.AreEqual(4000f, RealEstateRules.BubbleBurstLoss(10000f, 0.4f), 1e-3f); // 4割暴落→#1939
        }
    }
}
