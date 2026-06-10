using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 徴募（L-4 #96）を固定する：生産年齢人口×上限割合が徴募上限、Draft は上限内で人口を実際に削り、
    /// 徴募割合が産出・支持ペナルティへ写る。復員で働き手が戻る。null安全・クランプを担保。
    /// </summary>
    public class ConscriptionRulesTests
    {
        private static readonly ConscriptionParams P = ConscriptionParams.Default; // 上限20%/産出1.0/支持0.5/1兵=人口1

        [Test]
        public void DraftCapacity_WorkingTimesRatio()
        {
            var pop = new Population(100f, 1000f, 100f);
            Assert.AreEqual(200f, ConscriptionRules.DraftCapacity(pop, P), 1e-4f); // 1000×0.2
            Assert.AreEqual(0f, ConscriptionRules.DraftCapacity(null, P), 1e-5f);  // null安全
        }

        [Test]
        public void Draft_GrantsWithinCap_AndReducesWorking()
        {
            var pop = new Population(0f, 1000f, 0f);
            float got = ConscriptionRules.Draft(pop, 150f, P);
            Assert.AreEqual(150f, got, 1e-4f);
            Assert.AreEqual(850f, pop.working, 1e-4f); // 働き手が減る

            // 上限超過の要求は上限まで
            var pop2 = new Population(0f, 1000f, 0f);
            float capped = ConscriptionRules.Draft(pop2, 500f, P);
            Assert.AreEqual(200f, capped, 1e-4f);
            Assert.AreEqual(800f, pop2.working, 1e-4f);
        }

        [Test]
        public void DraftedFraction_AndPenalties()
        {
            // 1000人から200人抜けた＝0.2
            Assert.AreEqual(0.2f, ConscriptionRules.DraftedFraction(200f, 1000f), 1e-5f);
            // 産出倍率＝1−0.2×1.0=0.8
            Assert.AreEqual(0.8f, ConscriptionRules.OutputFactor(0.2f, P), 1e-5f);
            // 支持ペナルティ＝0.2×0.5=0.1
            Assert.AreEqual(0.1f, ConscriptionRules.SupportPenalty(0.2f, P), 1e-5f);
            // 徴募ゼロなら無傷
            Assert.AreEqual(1f, ConscriptionRules.OutputFactor(0f, P), 1e-5f);
        }

        [Test]
        public void DraftedFraction_ZeroOriginal_EdgeCases()
        {
            Assert.AreEqual(0f, ConscriptionRules.DraftedFraction(0f, 0f), 1e-5f);
            Assert.AreEqual(1f, ConscriptionRules.DraftedFraction(10f, 0f), 1e-5f);
        }

        [Test]
        public void Demobilize_ReturnsWorkersToPopulation()
        {
            var pop = new Population(0f, 800f, 0f);
            float returned = ConscriptionRules.Demobilize(pop, 200f, P);
            Assert.AreEqual(200f, returned, 1e-4f);
            Assert.AreEqual(1000f, pop.working, 1e-4f);
            Assert.AreEqual(0f, ConscriptionRules.Demobilize(null, 200f, P), 1e-5f); // null安全
        }

        [Test]
        public void Draft_NegativeRequest_GrantsNothing()
        {
            var pop = new Population(0f, 1000f, 0f);
            Assert.AreEqual(0f, ConscriptionRules.Draft(pop, -50f, P), 1e-5f);
            Assert.AreEqual(1000f, pop.working, 1e-4f);
        }
    }
}
