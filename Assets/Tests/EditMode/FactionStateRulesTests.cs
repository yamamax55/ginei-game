using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国家状態の合成（社会・政治シミュ層の統合）を固定する：収奪的統治は腐敗→合意の崩壊→末人で
    /// 崩れ、包摂的統治＋有徳は安定する。安定度の合成・崩壊判定。
    /// </summary>
    public class FactionStateRulesTests
    {
        [Test]
        public void ExtractiveRule_CollapsesOverTime()
        {
            // 収奪的（包摂度0＝抑圧最大）・無徳。
            var s = new FactionState(Faction.帝国, inclusiveness: 0f);
            s.regime.virtue = 0f;

            for (int i = 0; i < 10; i++) FactionStateRules.Tick(s, 1f);

            Assert.IsTrue(FactionStateRules.IsCollapsing(s));        // 天命喪失/統治不能/末人のいずれか
            Assert.Less(FactionStateRules.Stability(s), 0.5f);
        }

        [Test]
        public void InclusiveVirtuousRule_StaysStable()
        {
            // 包摂的（抑圧0）・有徳。
            var s = new FactionState(Faction.帝国, inclusiveness: 1f);
            s.regime.virtue = 0.9f;

            for (int i = 0; i < 10; i++) FactionStateRules.Tick(s, 1f);

            Assert.IsFalse(FactionStateRules.IsCollapsing(s));
            Assert.IsFalse(s.community.dissent);
            Assert.IsFalse(ConsentRules.IsUngovernable(s.polity));
            Assert.Greater(FactionStateRules.Stability(s), 0.6f);
        }

        [Test]
        public void Stability_IsAverageOfFourPillars()
        {
            var s = new FactionState(Faction.帝国);
            s.regime.legitimacy = 0.8f;
            s.polity.cooperation = 0.6f;
            s.organization.cohesion = 1.0f;
            s.community.hope = 0.4f;
            Assert.AreEqual((0.8f + 0.6f + 1.0f + 0.4f) / 4f, FactionStateRules.Stability(s), 1e-4f);
        }

        [Test]
        public void OrganizationFragmentation_CountsAsCollapsing()
        {
            var s = new FactionState(Faction.帝国, inclusiveness: 1f);
            s.regime.virtue = 0.9f;
            // 健全な国家でも、継承で組織が崩れれば崩壊扱い
            Assert.IsFalse(FactionStateRules.IsCollapsing(s));
            SuccessionRules.ResolveSuccession(s.organization, successorLegitimacy: 0.2f, successorCharisma: 0.2f);
            Assert.IsTrue(s.organization.fragmented);
            Assert.IsTrue(FactionStateRules.IsCollapsing(s));
        }
    }
}
