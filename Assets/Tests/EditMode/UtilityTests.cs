using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 公益事業（電気・ガス・水道・#2021・<see cref="UtilityRules"/>）を固定する：自然独占と料金規制(UTL-1)、民営/公営＝政体＋
    /// 法律(UTL-2)、ユニバーサルサービス(UTL-3)、供給と需要(UTL-4)、インフラ投資と料金(UTL-5)。
    /// </summary>
    public class UtilityTests
    {
        // ===== UTL-1 自然独占と料金規制 =====
        [Test]
        public void RateRegulation_RevenueRequirement()
        {
            // 総括原価＝運営費600＋規制資産2000×5% = 700
            Assert.AreEqual(700f, UtilityRules.RevenueRequirement(600f, 2000f, 0.05f), 1e-3f);
            Assert.AreEqual(7f, UtilityRules.RegulatedUnitPrice(700f, 100f), 1e-3f);
            Assert.IsTrue(UtilityRules.IsNaturalMonopoly(5000f, 1000f));   // 高固定費＝自然独占
            Assert.IsFalse(UtilityRules.IsNaturalMonopoly(500f, 1000f));
        }

        // ===== UTL-2 民営/公営＝政体＋法律 =====
        [Test]
        public void Ownership_DependsOnRegimeAndLaw()
        {
            // 共産は法律に関わらず国有
            Assert.AreEqual(Ownership.国有, UtilityRules.OwnershipFor("共産", true));
            Assert.AreEqual(Ownership.国有, UtilityRules.OwnershipFor("共産", false));
            // 資本主義は民営化法が許せば私有・許さなければ公営
            Assert.AreEqual(Ownership.私有, UtilityRules.OwnershipFor("民主", true));
            Assert.AreEqual(Ownership.国有, UtilityRules.OwnershipFor("民主", false));
            // 民営化は政体＋法律の両方が要る
            Assert.IsTrue(UtilityRules.CanPrivatize("民主", true));
            Assert.IsFalse(UtilityRules.CanPrivatize("民主", false)); // 法律が許さない
            Assert.IsFalse(UtilityRules.CanPrivatize("共産", true));  // 政体が許さない
        }

        // ===== UTL-3 ユニバーサルサービス =====
        [Test]
        public void UniversalService_CrossSubsidy()
        {
            // 不採算100戸×(費用10−収入6) = 赤字400
            Assert.AreEqual(400f, UtilityRules.UniversalServiceCost(100f, 10f, 6f), 1e-3f);
            // 採算地域の黒字1000が不採算赤字400を補助＝残600
            Assert.AreEqual(600f, UtilityRules.CrossSubsidy(1000f, 400f), 1e-3f);
        }

        // ===== UTL-4 供給と需要・安定供給 =====
        [Test]
        public void Supply_ShortfallAndBlackout()
        {
            Assert.AreEqual(100f, UtilityRules.SuppliedDemand(120f, 100f), 1e-3f); // 能力までしか供給できない
            Assert.AreEqual(0.16667f, UtilityRules.ShortfallRatio(120f, 100f), 1e-3f); // 停電/断水率
            Assert.IsTrue(UtilityRules.IsBlackout(120f, 100f));
            Assert.IsFalse(UtilityRules.IsBlackout(80f, 100f));
        }

        // ===== UTL-5 インフラ投資と料金 =====
        [Test]
        public void Investment_RateBaseAndDepreciation()
        {
            Assert.AreEqual(2500f, UtilityRules.RateBaseAfterInvestment(2000f, 500f), 1e-3f); // 投資が規制資産に入る
            Assert.AreEqual(80f, UtilityRules.DepreciationCost(2000f, 0.04f), 1e-3f);          // 老朽化＝償却費
        }
    }
}
