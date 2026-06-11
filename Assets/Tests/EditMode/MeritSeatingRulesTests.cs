using NUnit.Framework;

namespace Ginei.Tests
{
    public class MeritSeatingRulesTests
    {
        [Test]
        public void 座次は功績人望年功の加重合成()
        {
            // 0.6+0.3+0.1=1.0
            Assert.AreEqual(1.0f, MeritSeatingRules.SeatingRank(1f, 1f, 1f), 1e-4f);
            // 功績のみ＝0.6
            Assert.AreEqual(0.6f, MeritSeatingRules.SeatingRank(1f, 0f, 0f), 1e-4f);
        }

        [Test]
        public void 功績は年功より重い()
        {
            float meritOnly = MeritSeatingRules.SeatingRank(1f, 0f, 0f);      // 0.6
            float seniorityOnly = MeritSeatingRules.SeatingRank(0f, 0f, 1f);  // 0.1
            Assert.Greater(meritOnly, seniorityOnly);
            Assert.AreEqual(0.1f, seniorityOnly, 1e-4f);
        }

        [Test]
        public void 席次の近い者ほど対立する()
        {
            // 同位＝差0＝頂点 0.8
            Assert.AreEqual(0.8f, MeritSeatingRules.SeatRivalry(0.5f, 0.5f), 1e-4f);
            // 差0.7＝0.8*0.3=0.24
            Assert.AreEqual(0.24f, MeritSeatingRules.SeatRivalry(0.2f, 0.9f), 1e-4f);
        }

        [Test]
        public void 衆議の一票は上位ほど重いが独裁ではない()
        {
            // 1/4 + 1.0*0.5 = 0.75
            Assert.AreEqual(0.75f, MeritSeatingRules.CollectiveDecisionWeight(1f, 4), 1e-4f);
            // 下位でも平等票は残る 1/5 = 0.2
            Assert.AreEqual(0.2f, MeritSeatingRules.CollectiveDecisionWeight(0f, 5), 1e-4f);
        }

        [Test]
        public void 合意度は最大シェアで割れるほど低い()
        {
            Assert.AreEqual(0.6f, MeritSeatingRules.ConsensusLevel(new[] { 0.6f, 0.3f, 0.1f }), 1e-4f);
            // null/空は0
            Assert.AreEqual(0f, MeritSeatingRules.ConsensusLevel(null), 1e-4f);
            Assert.AreEqual(0f, MeritSeatingRules.ConsensusLevel(new float[0]), 1e-4f);
        }

        [Test]
        public void 決定の正統性は合意と裁可の合成()
        {
            // 0.6*0.7 + 1.0*0.3 = 0.42+0.3 = 0.72
            Assert.AreEqual(0.72f, MeritSeatingRules.DecisionLegitimacy(0.6f, 1f), 1e-4f);
        }

        [Test]
        public void 功績で座次は上がりうる下剋上()
        {
            // (1-0.2)*0.9*0.6 = 0.432
            Assert.AreEqual(0.432f, MeritSeatingRules.MeritMobility(0.2f, 0.9f), 1e-4f);
            // 既に最上位なら上昇余地0
            Assert.AreEqual(0f, MeritSeatingRules.MeritMobility(1f, 1f), 1e-4f);
        }

        [Test]
        public void 正統な序列判定は功績本位度の閾値()
        {
            Assert.IsTrue(MeritSeatingRules.IsLegitimateSeating(0.6f, 0.5f));
            Assert.IsFalse(MeritSeatingRules.IsLegitimateSeating(0.3f, 0.5f));
        }

        [Test]
        public void 物語_公正な座次が義兄弟の結束を生む()
        {
            // 同じ大義でも、功で報いた公正な座次の方が結束が強い（梁山泊の義）。
            float fair = MeritSeatingRules.BrotherhoodCohesion(0.8f, 0.8f);   // 0.4+0.4=0.8
            float unfair = MeritSeatingRules.BrotherhoodCohesion(0.8f, 0.2f); // 0.4+0.1=0.5
            Assert.AreEqual(0.8f, fair, 1e-4f);
            Assert.AreEqual(0.5f, unfair, 1e-4f);
            Assert.Greater(fair, unfair);
        }
    }
}
