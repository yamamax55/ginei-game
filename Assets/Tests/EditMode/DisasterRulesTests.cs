using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 災害を固定する：救援が被害を緩和（人口・安定度）、対応の巧拙が正統性を±に振る（深刻度で増幅）、
    /// 疫病はロジスティック増殖で封じ込めが増殖を止める。境界・クランプを担保。
    /// </summary>
    public class DisasterRulesTests
    {
        private static readonly DisasterParams P = DisasterParams.Default;
        // 人口喪失0.1/安定度30/救援緩和0.8/救援+0.1/放置−0.2

        [Test]
        public void ReliefFactor_CutsDamage()
        {
            Assert.AreEqual(1f, DisasterRules.ReliefFactor(0f, P), 1e-5f);    // 放置＝被害満額
            Assert.AreEqual(0.2f, DisasterRules.ReliefFactor(1f, P), 1e-5f);  // 全力救援＝2割まで軽減
        }

        [Test]
        public void PopulationLoss_SeverityTimesRelief()
        {
            // 深刻度1・放置＝1000×0.1=100
            Assert.AreEqual(100f, DisasterRules.PopulationLoss(1000f, 1f, 0f, P), 1e-4f);
            // 全力救援＝20
            Assert.AreEqual(20f, DisasterRules.PopulationLoss(1000f, 1f, 1f, P), 1e-4f);
            // 軽微な災害＝比例
            Assert.AreEqual(50f, DisasterRules.PopulationLoss(1000f, 0.5f, 0f, P), 1e-4f);
        }

        [Test]
        public void StabilityHit_SameShape()
        {
            Assert.AreEqual(30f, DisasterRules.StabilityHit(1f, 0f, P), 1e-4f);
            Assert.AreEqual(6f, DisasterRules.StabilityHit(1f, 1f, P), 1e-4f);
            Assert.AreEqual(0f, DisasterRules.StabilityHit(0f, 0f, P), 1e-5f);
        }

        [Test]
        public void LegitimacyDelta_RewardsReliefPunishesNeglect()
        {
            // 全力救援×大災害＝+0.1
            Assert.AreEqual(0.1f, DisasterRules.LegitimacyDelta(1f, 1f, P), 1e-5f);
            // 完全放置×大災害＝−0.2
            Assert.AreEqual(-0.2f, DisasterRules.LegitimacyDelta(1f, 0f, P), 1e-5f);
            // 中立対応＝±0
            Assert.AreEqual(0f, DisasterRules.LegitimacyDelta(1f, 0.5f, P), 1e-5f);
            // 小災害は対応が問われない＝深刻度で減衰
            Assert.AreEqual(-0.02f, DisasterRules.LegitimacyDelta(0.1f, 0f, P), 1e-5f);
        }

        [Test]
        public void EpidemicTick_LogisticGrowth()
        {
            // 中腹（0.5）が最速：0.5+1×1×0.5×0.5×1=0.75
            Assert.AreEqual(0.75f, DisasterRules.EpidemicTick(0.5f, 1f, 0f, 1f), 1e-5f);
            // 封じ込め完全＝増えない
            Assert.AreEqual(0.5f, DisasterRules.EpidemicTick(0.5f, 1f, 1f, 1f), 1e-5f);
            // 感染ゼロからは発生しない（s=0 で増殖0）
            Assert.AreEqual(0f, DisasterRules.EpidemicTick(0f, 1f, 0f, 1f), 1e-5f);
            // 飽和（s=1）では広がる余地なし
            Assert.AreEqual(1f, DisasterRules.EpidemicTick(1f, 1f, 0f, 1f), 1e-5f);
        }

        [Test]
        public void DisasterKind_EnumExists()
        {
            // 種類enumの存在と値を固定（被害の出方は値駆動で各倍率に委ねる）
            Assert.AreEqual(0, (int)DisasterKind.疫病);
            Assert.AreEqual(1, (int)DisasterKind.飢饉);
            Assert.AreEqual(2, (int)DisasterKind.天災);
        }
    }
}
