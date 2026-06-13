using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政党制を固定する（政党システム GOV-6 #159）：成熟度が上がるほど有効政党数が2へ収束（二大政党制＝デュヴェルジェ）、
    /// だが二大政党制で成熟するほど分極化（分断）が高まり危機になる。多党は連立で穏健化、一党は対立なし。
    /// 有効政党数は Laakso–Taagepera 指数（1/Σ支持率²）。
    /// </summary>
    public class PartySystemRulesTests
    {
        static Party P(int id, float support) => new Party(id, "党" + id, Faction.同盟) { support = support };
        static float Sum(IEnumerable<Party> ps) { float t = 0f; foreach (var p in ps) t += p.support; return t; }

        [Test]
        public void EffectiveNumberOfParties_LaaksoTaagepera()
        {
            Assert.AreEqual(1f, PartySystemRules.EffectiveNumberOfParties(new[] { P(1, 1f) }), 1e-4f);
            Assert.AreEqual(2f, PartySystemRules.EffectiveNumberOfParties(new[] { P(1, 0.5f), P(2, 0.5f) }), 1e-4f);
            Assert.AreEqual(4f, PartySystemRules.EffectiveNumberOfParties(
                new[] { P(1, 0.25f), P(2, 0.25f), P(3, 0.25f), P(4, 0.25f) }), 1e-4f);
            Assert.AreEqual(0f, PartySystemRules.EffectiveNumberOfParties(new Party[0]));
        }

        [Test]
        public void Maturity_DrivesTargetTowardTwoParty()
        {
            Assert.AreEqual(PartySystemRules.FragmentedParties, PartySystemRules.TargetEffectiveParties(0f), 1e-4f);
            Assert.AreEqual(2f, PartySystemRules.TargetEffectiveParties(1f), 1e-4f); // 成熟＝二大政党
            Assert.AreEqual(3.5f, PartySystemRules.TargetEffectiveParties(0.5f), 1e-4f);
        }

        [Test]
        public void MaturityFrom_AveragesGovernanceHealth()
        {
            var mature = new FactionState(Faction.同盟);
            mature.regime.legitimacy = 0.9f; mature.regime.corruption = 0.1f;
            mature.polity.cooperation = 0.9f; mature.inclusiveness = 0.9f;
            Assert.AreEqual(0.9f, PartySystemRules.MaturityFrom(mature), 1e-4f);

            var immature = new FactionState(Faction.帝国);
            immature.regime.legitimacy = 0.3f; immature.regime.corruption = 0.7f;
            immature.polity.cooperation = 0.3f; immature.inclusiveness = 0.2f;
            Assert.AreEqual(0.275f, PartySystemRules.MaturityFrom(immature), 1e-4f);
            Assert.AreEqual(0f, PartySystemRules.MaturityFrom(null));
        }

        [Test]
        public void Polarization_PeaksAtTwoParty_HighMaturity()
        {
            Assert.AreEqual(1f, PartySystemRules.TwoPartyProximity(2f), 1e-4f);
            Assert.AreEqual(0f, PartySystemRules.TwoPartyProximity(4f), 1e-4f); // 多党は分極化しにくい
            Assert.AreEqual(0f, PartySystemRules.TwoPartyProximity(1f), 1e-4f); // 一党は対立なし

            Assert.AreEqual(1f, PartySystemRules.Polarization(1f, 2f), 1e-4f);   // 成熟×二大政党＝最大
            Assert.AreEqual(0.5f, PartySystemRules.Polarization(0.5f, 2f), 1e-4f);
            Assert.AreEqual(0f, PartySystemRules.Polarization(1f, 4f), 1e-4f);   // 成熟でも多党なら低い
        }

        [Test]
        public void TickConsolidation_MovesTowardTwoParty_PreservesTotal()
        {
            var parties = new List<Party> { P(1, 0.4f), P(2, 0.3f), P(3, 0.2f), P(4, 0.1f) };
            float enpBefore = PartySystemRules.EffectiveNumberOfParties(parties);
            float moved = PartySystemRules.TickConsolidation(parties, maturity: 0.9f, rate: 0.5f);

            Assert.Greater(moved, 0f);
            Assert.Less(PartySystemRules.EffectiveNumberOfParties(parties), enpBefore); // 集中が進む
            Assert.AreEqual(1f, Sum(parties), 1e-3f);                                    // 総支持は保存

            // 成熟0は目標が多党＝統合圧力なし
            Assert.AreEqual(0f, PartySystemRules.ConsolidationPressure(parties, 0f), 1e-4f);
            Assert.Greater(PartySystemRules.ConsolidationPressure(
                new[] { P(1, 0.25f), P(2, 0.25f), P(3, 0.25f), P(4, 0.25f) }, 0.9f), 0f);
        }

        [Test]
        public void MatureTwoParty_IsDivided_ImmatureIsNot()
        {
            var two = new List<Party> { P(1, 0.5f), P(2, 0.5f) };
            Assert.IsTrue(PartySystemRules.IsTwoPartySystem(two));
            Assert.IsFalse(PartySystemRules.IsDividedCrisis(0.3f, two)); // 未成熟な二大政党は分断危機でない
            Assert.IsTrue(PartySystemRules.IsDividedCrisis(0.95f, two)); // 成熟した二大政党は分断危機
        }

        [Test]
        public void Maturation_ConvergesMultiPartyToTwoParty_ThenDividedCrisis()
        {
            // 多党乱立から出発（有効政党数=4）
            var parties = new List<Party> { P(1, 0.25f), P(2, 0.25f), P(3, 0.25f), P(4, 0.25f) };
            float enpStart = PartySystemRules.EffectiveNumberOfParties(parties);
            Assert.IsFalse(PartySystemRules.IsTwoPartySystem(parties));

            // 成熟が高い民主政治では年を追って二大政党へ収束する
            for (int i = 0; i < 40; i++)
                PartySystemRules.TickConsolidation(parties, maturity: 0.95f, rate: 0.5f);

            float enpEnd = PartySystemRules.EffectiveNumberOfParties(parties);
            Assert.Less(enpEnd, enpStart);
            Assert.IsTrue(PartySystemRules.IsTwoPartySystem(parties));        // 二大政党制へ近づいた
            Assert.IsTrue(PartySystemRules.IsDividedCrisis(0.95f, parties));  // そして分断危機
            Assert.AreEqual(1f, Sum(parties), 1e-3f);                         // 総支持は保存
        }
    }
}
