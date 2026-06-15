using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 主人公の岐路（TKO-7・<see cref="CareerForkRules"/>・#2484）を固定する：忠勤の既定、離反状態の判定、
    /// 下野/独立/亡命が開く条件（武勲・階級・対君主関係）、推奨の優先順位。
    /// </summary>
    public class CareerForkRulesTests
    {
        private static CareerForkRules.ForkParams P => CareerForkRules.ForkParams.Default;

        [Test]
        public void Loyal_OnlyServe()
        {
            // 忠誠高・不満低 → 離反しない＝忠勤のみ
            Assert.IsFalse(CareerForkRules.IsDisaffected(0.9f, 0.2f, P));
            var forks = CareerForkRules.AvailableForks(0.9f, 0.2f, 0.9f, 0.5f, 10, P);
            Assert.AreEqual(1, forks.Count);
            Assert.Contains(CareerFork.忠勤, forks);
            Assert.AreEqual(CareerFork.忠勤, CareerForkRules.Recommend(0.9f, 0.2f, 0.9f, 0.5f, 10, P));
        }

        [Test]
        public void Disaffected_LowRank_OnlyResignAvailableBesidesServe()
        {
            // 不満高・忠誠低・武勲低・低階級・関係中立 → 下野のみ（独立/亡命は開かない）
            Assert.IsTrue(CareerForkRules.IsDisaffected(0.3f, 0.7f, P));
            var forks = CareerForkRules.AvailableForks(0.3f, 0.7f, 0.2f, 0.0f, 5, P);
            CollectionAssert.AreEquivalent(new[] { CareerFork.忠勤, CareerFork.下野 }, forks);
            Assert.AreEqual(CareerFork.下野, CareerForkRules.Recommend(0.3f, 0.7f, 0.2f, 0.0f, 5, P));
        }

        [Test]
        public void Disaffected_HighMeritHighRank_OpensIndependence()
        {
            // 武勲0.7≥0.6 かつ 大将(8)以上 → 独立が開く
            var forks = CareerForkRules.AvailableForks(0.2f, 0.8f, 0.7f, 0.1f, 8, P);
            Assert.Contains(CareerFork.独立, forks);
            Assert.AreEqual(CareerFork.独立, CareerForkRules.Recommend(0.2f, 0.8f, 0.7f, 0.1f, 8, P), "独立を最優先で推奨");
        }

        [Test]
        public void Disaffected_BurnedBridges_OpensDefection()
        {
            // 対君主の関係 −0.5 ≤ −0.3 → 亡命が開く（武勲低・低階級なので独立は不可）
            var forks = CareerForkRules.AvailableForks(0.2f, 0.8f, 0.3f, -0.5f, 6, P);
            Assert.Contains(CareerFork.亡命, forks);
            Assert.IsFalse(forks.Contains(CareerFork.独立));
            Assert.AreEqual(CareerFork.亡命, CareerForkRules.Recommend(0.2f, 0.8f, 0.3f, -0.5f, 6, P));
        }

        [Test]
        public void Independence_TakesPriorityOverDefection()
        {
            // 武勲・階級が独立要件を満たし、かつ橋も焼けている → 独立を推奨（独立＞亡命＞下野）
            var forks = CareerForkRules.AvailableForks(0.1f, 0.9f, 0.8f, -0.6f, 9, P);
            Assert.Contains(CareerFork.独立, forks);
            Assert.Contains(CareerFork.亡命, forks);
            Assert.AreEqual(CareerFork.独立, CareerForkRules.Recommend(0.1f, 0.9f, 0.8f, -0.6f, 9, P));
        }
    }
}
