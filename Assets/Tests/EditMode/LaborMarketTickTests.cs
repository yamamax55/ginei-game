using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>労働市場の暦境界Tick（POPLAB-2/3 配線・<see cref="LaborMarketTickRules"/>）：安定度#109 連動の失業増減・職業配分の収束（総量保存）。</summary>
    public class LaborMarketTickTests
    {
        [Test]
        public void Stable_EmploymentStaysStructural()
        {
            var p = new Province(1, "", 100f) { systemType = SystemType.工業 }; // stability=基準（安定）
            var demand = LaborMarketTickRules.JobDemandShares(p);
            Assert.AreEqual(0.05f, demand.Share(Occupation.無職), 1e-4f); // 安定＝循環失業なし＝類型既定の無職
            Assert.AreEqual(1f, demand.Total, 1e-4f);

            LaborMarketTickRules.TickYear(p, 0.25f);
            Assert.AreEqual(0.05f, LaborMarketTickRules.UnemploymentRate(p), 1e-4f); // 構造的失業のまま
        }

        [Test]
        public void Unstable_RaisesUnemployment_TotalPreserved()
        {
            var p = new Province(2, "", 100f) { systemType = SystemType.工業 };
            p.stability = GovernanceRules.BaseStability - 20f; // 不安定（占領直後・戦時）

            var demand = LaborMarketTickRules.JobDemandShares(p);
            // 循環失業 = 20×0.005 = 0.1 → 無職 = 0.05 + 0.95×0.1 = 0.145
            Assert.AreEqual(0.145f, demand.Share(Occupation.無職), 1e-4f);
            Assert.AreEqual(1f, demand.Total, 1e-4f); // 総量保存

            // 1年目＝需要へ転職フロー（摩擦で緩やか）：0.05 + (0.145-0.05)×0.25 = 0.07375
            LaborMarketTickRules.TickYear(p, 0.25f);
            Assert.AreEqual(0.07375f, LaborMarketTickRules.UnemploymentRate(p), 1e-4f);

            // 年を重ねると需要へ収束（失業は0.145へ近づく）＝不安定が続くほど失業が積む
            for (int y = 0; y < 30; y++) LaborMarketTickRules.TickYear(p, 0.25f);
            float u = LaborMarketTickRules.UnemploymentRate(p);
            Assert.Greater(u, 0.144f);
            Assert.LessOrEqual(u, 0.145f);
        }

        [Test]
        public void WorkforceCreatedWhenNull_BackwardCompatible()
        {
            var p = new Province(3, "", 100f) { systemType = SystemType.農業 };
            Assert.IsNull(p.workforce);
            LaborMarketTickRules.TickYear(p, 0.25f);
            Assert.IsNotNull(p.workforce);
            Assert.AreEqual(1f, p.workforce.Total, 1e-4f); // 合計1（総量保存）
        }
    }
}
