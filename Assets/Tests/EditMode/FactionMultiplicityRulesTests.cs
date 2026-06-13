using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>派閥増殖安定則（FED-1 #1473・フェデラリスト第10篇）の EditMode テスト。</summary>
    public class FactionMultiplicityRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>HHI＝1派閥独占で1.0・N派閥均等で1/N・null/空は0。</summary>
        [Test]
        public void HerfindahlIndex_DominanceVsDiversity()
        {
            Assert.AreEqual(1f, FactionMultiplicityRules.HerfindahlIndex(new[] { 1f, 0f, 0f, 0f }), Eps);
            // 4派閥均等＝4×0.25²＝0.25。
            Assert.AreEqual(0.25f, FactionMultiplicityRules.HerfindahlIndex(new[] { 1f, 1f, 1f, 1f }), Eps);
            // 正規化されるので合計は1でなくても同じ。
            Assert.AreEqual(0.25f, FactionMultiplicityRules.HerfindahlIndex(new[] { 5f, 5f, 5f, 5f }), Eps);
            Assert.AreEqual(0f, FactionMultiplicityRules.HerfindahlIndex(null));
            Assert.AreEqual(0f, FactionMultiplicityRules.HerfindahlIndex(new float[0]));
        }

        /// <summary>実効派閥数＝HHI逆数（独占で1・4派閥均等で4・0は0）。</summary>
        [Test]
        public void EffectiveFactionCount_IsInverseOfHhi()
        {
            Assert.AreEqual(1f, FactionMultiplicityRules.EffectiveFactionCount(1f), Eps);
            Assert.AreEqual(4f, FactionMultiplicityRules.EffectiveFactionCount(0.25f), Eps);
            Assert.AreEqual(0f, FactionMultiplicityRules.EffectiveFactionCount(0f), Eps);
        }

        /// <summary>多数派暴政リスク＝集中度×強度×鋭さ（分散すればリスクは消える）。</summary>
        [Test]
        public void MajorityTyrannyRisk_ScalesWithConcentration()
        {
            Assert.AreEqual(0.4f, FactionMultiplicityRules.MajorityTyrannyRisk(0.5f, 0.8f), Eps);
            // 派閥が分散すれば（HHI低）どれだけ対立が烈しくてもリスクは小さい。
            float diffuse = FactionMultiplicityRules.MajorityTyrannyRisk(0.1f, 1f);
            float concentrated = FactionMultiplicityRules.MajorityTyrannyRisk(0.9f, 1f);
            Assert.Less(diffuse, concentrated);
            // 対立がゼロならリスクもゼロ。
            Assert.AreEqual(0f, FactionMultiplicityRules.MajorityTyrannyRisk(1f, 0f), Eps);
        }

        /// <summary>多数性の安定化＝派閥が多いほど1へ漸近・単独は0（マディソンの逆説）。</summary>
        [Test]
        public void MultiplicityStabilization_MoreFactionsMoreStable()
        {
            Assert.AreEqual(0f, FactionMultiplicityRules.MultiplicityStabilization(1f), Eps);
            // 実効4派閥＝(3)×0.5=1.5 → 1-1/2.5=0.6。
            Assert.AreEqual(0.6f, FactionMultiplicityRules.MultiplicityStabilization(4f), Eps);
            // 派閥が増えるほど単調に安定が増す。
            Assert.Less(FactionMultiplicityRules.MultiplicityStabilization(2f),
                        FactionMultiplicityRules.MultiplicityStabilization(8f));
        }

        /// <summary>連立の必要性＝1−HHI（分散するほど単独過半数が取れず連立が要る）。</summary>
        [Test]
        public void CoalitionNecessity_RisesWithDispersion()
        {
            Assert.AreEqual(0.75f, FactionMultiplicityRules.CoalitionNecessity(0.25f), Eps);
            // 独占なら連立は不要。
            Assert.AreEqual(0f, FactionMultiplicityRules.CoalitionNecessity(1f), Eps);
        }

        /// <summary>会派形成コスト＝障壁＋既存派閥の混雑×重み（混んでいると新派閥は作りにくい）。</summary>
        [Test]
        public void FactionFormationCost_CrowdingRaisesCost()
        {
            // 0.2 + 0.5×0.6 = 0.5。
            Assert.AreEqual(0.5f, FactionMultiplicityRules.FactionFormationCost(0.5f, 0.2f), Eps);
            // 既存派閥が増えるほどコストは上がる。
            Assert.Less(FactionMultiplicityRules.FactionFormationCost(0.1f, 0.2f),
                        FactionMultiplicityRules.FactionFormationCost(0.9f, 0.2f));
        }

        /// <summary>争点の交差＝多次元の争点が固定的対立を防ぐ指標（そのまま写す・クランプ）。</summary>
        [Test]
        public void CrossCuttingCleavages_ReflectsIssueDimensions()
        {
            Assert.AreEqual(0.7f, FactionMultiplicityRules.CrossCuttingCleavages(0.7f), Eps);
            Assert.AreEqual(1f, FactionMultiplicityRules.CrossCuttingCleavages(1.5f), Eps);
            Assert.AreEqual(0f, FactionMultiplicityRules.CrossCuttingCleavages(-0.3f), Eps);
        }

        /// <summary>派閥均衡判定＝実効派閥数が閾値以上で多数派専制に強い（既定3.0）。</summary>
        [Test]
        public void IsFactionallyBalanced_RequiresEnoughDiversity()
        {
            Assert.IsTrue(FactionMultiplicityRules.IsFactionallyBalanced(4f));
            Assert.IsFalse(FactionMultiplicityRules.IsFactionallyBalanced(2f));
            // 大きな共和国＝多様な派閥（HHI低→実効派閥数大）は均衡条件を満たす。
            float hhi = FactionMultiplicityRules.HerfindahlIndex(new[] { 1f, 1f, 1f, 1f, 1f });
            float effective = FactionMultiplicityRules.EffectiveFactionCount(hhi);
            Assert.IsTrue(FactionMultiplicityRules.IsFactionallyBalanced(effective));
        }
    }
}
