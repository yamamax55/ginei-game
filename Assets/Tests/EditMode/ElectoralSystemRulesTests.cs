using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政体が指導者の選び方を決めることを固定する（選挙システム基盤）：寡頭制（共産の集団指導/首長制の長老会）は
    /// 少数による合議、民主政治（立憲君主制/共和制）は選挙（党内/惑星/星系/勢力の四層）、君主制は世襲、独裁は指名。
    /// </summary>
    public class ElectoralSystemRulesTests
    {
        [Test]
        public void ModeFor_MapsRegimeToSelectionMethod()
        {
            Assert.AreEqual(LeaderSelectionMode.選挙, ElectoralSystemRules.ModeFor(GovernmentForm.共和制));
            Assert.AreEqual(LeaderSelectionMode.選挙, ElectoralSystemRules.ModeFor(GovernmentForm.立憲君主制));
            Assert.AreEqual(LeaderSelectionMode.世襲, ElectoralSystemRules.ModeFor(GovernmentForm.君主制));
            Assert.AreEqual(LeaderSelectionMode.指名, ElectoralSystemRules.ModeFor(GovernmentForm.指導者独裁));
            Assert.AreEqual(LeaderSelectionMode.合議, ElectoralSystemRules.ModeFor(GovernmentForm.共産主義)); // 集団指導＝寡頭
            Assert.AreEqual(LeaderSelectionMode.合議, ElectoralSystemRules.ModeFor(GovernmentForm.首長制));   // 長老会＝寡頭
        }

        [Test]
        public void Oligarchy_UsesCouncil_Democracy_UsesElections()
        {
            Assert.IsTrue(ElectoralSystemRules.IsOligarchic(GovernmentForm.共産主義));
            Assert.IsTrue(ElectoralSystemRules.IsOligarchic(GovernmentForm.首長制));
            Assert.IsFalse(ElectoralSystemRules.IsElectoral(GovernmentForm.共産主義));

            Assert.IsTrue(ElectoralSystemRules.IsElectoral(GovernmentForm.共和制));
            Assert.IsTrue(ElectoralSystemRules.IsElectoral(GovernmentForm.立憲君主制));
            Assert.IsFalse(ElectoralSystemRules.IsOligarchic(GovernmentForm.共和制));
        }

        [Test]
        public void ActiveTiers_DemocracyHasFourLayers_OthersNone()
        {
            var tiers = ElectoralSystemRules.ActiveElectionTiers(GovernmentForm.共和制);
            CollectionAssert.AreEquivalent(
                new[] { ElectionTier.党内, ElectionTier.惑星, ElectionTier.星系, ElectionTier.勢力 }, tiers);

            // 党内/惑星/星系/勢力 すべて存在する＝民主政治
            Assert.IsTrue(ElectoralSystemRules.HasTier(GovernmentForm.共和制, ElectionTier.党内));
            Assert.IsTrue(ElectoralSystemRules.HasTier(GovernmentForm.共和制, ElectionTier.惑星));
            Assert.IsTrue(ElectoralSystemRules.HasTier(GovernmentForm.共和制, ElectionTier.星系));
            Assert.IsTrue(ElectoralSystemRules.HasTier(GovernmentForm.共和制, ElectionTier.勢力));

            // 寡頭/君主/独裁は選挙が存在しない
            Assert.AreEqual(0, ElectoralSystemRules.ActiveElectionTiers(GovernmentForm.共産主義).Length);
            Assert.AreEqual(0, ElectoralSystemRules.ActiveElectionTiers(GovernmentForm.君主制).Length);
            Assert.IsFalse(ElectoralSystemRules.HasTier(GovernmentForm.君主制, ElectionTier.勢力));
        }
    }
}
