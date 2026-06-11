using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 征服地統治の三様（MKV-1 #1139・マキャヴェッリ『君主論』）の純ロジックを既定 Params で固定。
    /// 統合速度・裏切りリスク・初期コスト・恨み・長期安定・傀儡忠誠・最適戦略・統合判定のトレードオフを担保。
    /// </summary>
    public class ConquestGovernanceRulesTests
    {
        const float Tol = 1e-4f;

        /// <summary>統合速度：傀儡が最速・植民が最遅・文化的距離で割引（傀儡＞駆逐＞植民）。</summary>
        [Test]
        public void IntegrationSpeed_PuppetFastestColonizeSlowest()
        {
            float purge = ConquestGovernanceRules.IntegrationSpeed(ConquestStrategy.駆逐, 0f);
            float colonize = ConquestGovernanceRules.IntegrationSpeed(ConquestStrategy.植民, 0f);
            float puppet = ConquestGovernanceRules.IntegrationSpeed(ConquestStrategy.傀儡, 0f);

            Assert.AreEqual(0.05f, purge, Tol);
            Assert.AreEqual(0.04f, colonize, Tol);
            Assert.AreEqual(0.07f, puppet, Tol);
            Assert.Greater(puppet, purge);
            Assert.Greater(purge, colonize);

            // 文化的距離 1 で半減。
            Assert.AreEqual(0.07f * 0.5f, ConquestGovernanceRules.IntegrationSpeed(ConquestStrategy.傀儡, 1f), Tol);
        }

        /// <summary>裏切りリスク：傀儡が最高（現地勢力が残る）・植民が最低。現地勢力と恨みで増える。</summary>
        [Test]
        public void BetrayalRisk_PuppetHighestColonizeLowest()
        {
            // 現地勢力0・恨み0＝基礎値そのもの。
            float purge = ConquestGovernanceRules.BetrayalRisk(ConquestStrategy.駆逐, 0f, 0f);
            float colonize = ConquestGovernanceRules.BetrayalRisk(ConquestStrategy.植民, 0f, 0f);
            float puppet = ConquestGovernanceRules.BetrayalRisk(ConquestStrategy.傀儡, 0f, 0f);
            Assert.AreEqual(0.30f, purge, Tol);
            Assert.AreEqual(0.08f, colonize, Tol);
            Assert.AreEqual(0.40f, puppet, Tol);
            Assert.Greater(puppet, colonize);

            // 傀儡：現地勢力1（重み0.6）＋恨み1（重み0.3）＝0.40+0.6+0.3 をクランプ＝1。
            Assert.AreEqual(1f, ConquestGovernanceRules.BetrayalRisk(ConquestStrategy.傀儡, 1f, 1f), Tol);
            // 現地勢力が傀儡に最も強く効く（同条件で傀儡＞駆逐の増分）。
            float puppetLp = ConquestGovernanceRules.BetrayalRisk(ConquestStrategy.傀儡, 0.5f, 0f);
            float purgeLp = ConquestGovernanceRules.BetrayalRisk(ConquestStrategy.駆逐, 0.5f, 0f);
            Assert.AreEqual(0.40f + 0.6f * 0.5f, puppetLp, Tol);
            Assert.AreEqual(0.30f + 0.2f * 0.5f, purgeLp, Tol);
        }

        /// <summary>初期コスト：駆逐が最も高く傀儡が最も安い。領土規模に比例。</summary>
        [Test]
        public void UpfrontCost_PurgeExpensivePuppetCheap()
        {
            float purge = ConquestGovernanceRules.UpfrontCost(ConquestStrategy.駆逐, 1f);
            float colonize = ConquestGovernanceRules.UpfrontCost(ConquestStrategy.植民, 1f);
            float puppet = ConquestGovernanceRules.UpfrontCost(ConquestStrategy.傀儡, 1f);
            Assert.AreEqual(1.0f, purge, Tol);
            Assert.AreEqual(0.8f, colonize, Tol);
            Assert.AreEqual(0.3f, puppet, Tol);
            Assert.Greater(purge, colonize);
            Assert.Greater(colonize, puppet);

            // 領土規模に比例。
            Assert.AreEqual(0.5f, ConquestGovernanceRules.UpfrontCost(ConquestStrategy.駆逐, 0.5f), Tol);
        }

        /// <summary>恨み：駆逐が最大（虐殺の汚点）・傀儡が最低。残虐度に比例。</summary>
        [Test]
        public void Resentment_PurgeMaxPuppetMin()
        {
            float purge = ConquestGovernanceRules.Resentment(ConquestStrategy.駆逐, 1f);
            float colonize = ConquestGovernanceRules.Resentment(ConquestStrategy.植民, 1f);
            float puppet = ConquestGovernanceRules.Resentment(ConquestStrategy.傀儡, 1f);
            Assert.AreEqual(0.9f, purge, Tol);
            Assert.AreEqual(0.4f, colonize, Tol);
            Assert.AreEqual(0.2f, puppet, Tol);
            Assert.Greater(purge, colonize);
            Assert.Greater(colonize, puppet);

            // 残虐度0で恨み0。
            Assert.AreEqual(0f, ConquestGovernanceRules.Resentment(ConquestStrategy.駆逐, 0f), Tol);
        }

        /// <summary>長期安定：統合が進み裏切りリスクが低いほど高い目標へ収束。高リスクは安定しない。</summary>
        [Test]
        public void LongTermStability_HighIntegLowBetrayal_Stabilizes()
        {
            // 統合速度0.05（満点目安）・裏切り0＝目標1へ向かう。
            float up = ConquestGovernanceRules.LongTermStability(0f, 0.05f, 0f, 1f);
            Assert.Greater(up, 0f);

            // 裏切りリスク1＝目標0＝下がる。
            float down = ConquestGovernanceRules.LongTermStability(0.5f, 0.05f, 1f, 1f);
            Assert.Less(down, 0.5f);

            // dt=0 は不変。
            Assert.AreEqual(0.5f, ConquestGovernanceRules.LongTermStability(0.5f, 0.05f, 0f, 0f), Tol);

            // 多tickで高統合・低リスクは高安定へ。
            float s = 0f;
            for (int i = 0; i < 200; i++) s = ConquestGovernanceRules.LongTermStability(s, 0.05f, 0.1f, 1f);
            Assert.Greater(s, 0.8f);
        }

        /// <summary>傀儡忠誠：利益で釣れるが現地勢力が強いと離反へ下がる。</summary>
        [Test]
        public void PuppetLoyalty_BenefitHoldsLocalPowerErodes()
        {
            // 高利益・低現地勢力＝忠誠は上がる。
            float loyal = 0f;
            for (int i = 0; i < 200; i++) loyal = ConquestGovernanceRules.PuppetLoyalty(loyal, 0.1f, 1f, 1f);
            Assert.Greater(loyal, 0.8f);

            // 低利益・高現地勢力＝忠誠は下がる（目標 = 0 - 0.6 + 0.3 をクランプ = 0）。
            float diss = 1f;
            for (int i = 0; i < 200; i++) diss = ConquestGovernanceRules.PuppetLoyalty(diss, 1f, 0f, 1f);
            Assert.Less(diss, 0.05f);

            // dt=0 は不変。
            Assert.AreEqual(0.5f, ConquestGovernanceRules.PuppetLoyalty(0.5f, 0.5f, 0.5f, 0f), Tol);
        }

        /// <summary>最適戦略：現地勢力が強いと傀儡を避け、自勢力が乏しく抵抗が強いなら駆逐へ寄る。</summary>
        [Test]
        public void OptimalStrategy_AdaptsToSituation()
        {
            // 現地勢力が極めて強い＝傀儡の裏切りリスクが跳ね上がり、傀儡は選ばれない。
            var s1 = ConquestGovernanceRules.OptimalStrategy(0.5f, 0.8f, 1f);
            Assert.AreNotEqual(ConquestStrategy.傀儡, s1);

            // 現地勢力が弱く文化が近い＝傀儡（安く速い）が有利。
            var s2 = ConquestGovernanceRules.OptimalStrategy(0f, 0.8f, 0f);
            Assert.AreEqual(ConquestStrategy.傀儡, s2);
        }

        /// <summary>統合判定：長期安定がしきい値以上で完全統合。</summary>
        [Test]
        public void IsConsolidated_Threshold()
        {
            Assert.IsTrue(ConquestGovernanceRules.IsConsolidated(0.85f, 0.8f));
            Assert.IsFalse(ConquestGovernanceRules.IsConsolidated(0.7f, 0.8f));
            // 同値はしきい値以上＝統合。
            Assert.IsTrue(ConquestGovernanceRules.IsConsolidated(0.8f, 0.8f));
        }
    }
}
