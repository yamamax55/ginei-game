using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 寡頭制の少数合議を固定する（選挙システム基盤）：実権の重みで決まり（一人一票でない）、合意の閾値を満たさねば膠着する。
    /// 少数の実力者が決める＝大きな重みを持つ寡頭の声が多数の小物を覆す。
    /// </summary>
    public class CouncilRulesTests
    {
        [Test]
        public void ResolveByWeight_WeightedNotHeadcount()
        {
            // 候補1を推す実権=3+1=4、候補2を推す実権=2 → 候補1（重み多数）
            var votes = new List<CouncilVote>
            {
                new CouncilVote(oligarchId: 10, candidateId: 1, weight: 3f),
                new CouncilVote(oligarchId: 11, candidateId: 1, weight: 1f),
                new CouncilVote(oligarchId: 12, candidateId: 2, weight: 2f),
            };
            int winner = CouncilRules.ResolveByWeight(votes, out float support);
            Assert.AreEqual(1, winner);
            Assert.AreEqual(4f / 6f, support, 1e-4f);
            Assert.AreEqual(2f / 6f, CouncilRules.Support(votes, 2), 1e-4f);
        }

        [Test]
        public void FewPowerfulOverrideManyWeak()
        {
            // 1人の大物（実権10・候補1）が、5人の小物（各実権1・候補2）を覆す＝寡頭制
            var votes = new List<CouncilVote> { new CouncilVote(1, 1, 10f) };
            for (int i = 0; i < 5; i++) votes.Add(new CouncilVote(100 + i, 2, 1f));

            int winner = CouncilRules.ResolveByWeight(votes, out _);
            Assert.AreEqual(1, winner); // 頭数では2が多いが実権で1が勝つ
        }

        [Test]
        public void Decide_RequiresConsensusThreshold_ElseDeadlock()
        {
            // 候補1=4/6≈0.667。過半(0.5)なら決まり、特別多数(0.7)なら膠着
            var votes = new List<CouncilVote>
            {
                new CouncilVote(10, 1, 3f),
                new CouncilVote(11, 1, 1f),
                new CouncilVote(12, 2, 2f),
            };
            Assert.IsTrue(CouncilRules.HasConsensus(votes, 1)); // 過半
            Assert.IsFalse(CouncilRules.HasConsensus(votes, 1, 0.7f));

            Assert.AreEqual(1, CouncilRules.Decide(votes, 0.5f, out _));   // 過半で決まる
            Assert.AreEqual(-1, CouncilRules.Decide(votes, 0.7f, out _));  // 特別多数に届かず膠着

            Assert.IsFalse(CouncilRules.IsDeadlocked(votes, 0.5f));
            Assert.IsTrue(CouncilRules.IsDeadlocked(votes, 0.7f));
        }

        [Test]
        public void TieBreak_PrefersLowerCandidateId_Deterministic()
        {
            var votes = new List<CouncilVote>
            {
                new CouncilVote(10, 5, 2f),
                new CouncilVote(11, 3, 2f),
            };
            Assert.AreEqual(3, CouncilRules.ResolveByWeight(votes, out _)); // 同点は id 小
        }

        [Test]
        public void EmptyOrNull_IsNotDeadlock_ButNoDecision()
        {
            var empty = new List<CouncilVote>();
            Assert.AreEqual(-1, CouncilRules.ResolveByWeight(empty, out float s));
            Assert.AreEqual(0f, s);
            Assert.AreEqual(0f, CouncilRules.Support(empty, 1));
            Assert.AreEqual(-1, CouncilRules.Decide(empty, out _));
            Assert.IsFalse(CouncilRules.IsDeadlocked(empty)); // 票が無いのは膠着でなく不成立
            Assert.AreEqual(-1, CouncilRules.ResolveByWeight(null, out _));
            Assert.AreEqual(0f, CouncilRules.TotalWeight(null));
        }
    }
}
