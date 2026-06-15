using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>決裁駆動キャンペーンのマクロメーター（適用・勝敗評価）を固定する（決裁ゲーム MVP）。</summary>
    public class CampaignMeterRulesTests
    {
        [Test]
        public void Defaults_AreNeutral_Continue()
        {
            var m = new CampaignMeters();
            Assert.AreEqual(CampaignOutcome.継続, CampaignMeterRules.Evaluate(m));
        }

        [Test]
        public void PillarCollapse_IsDefeat_WithReason()
        {
            var m = new CampaignMeters { support = 0f };
            var outcome = CampaignMeterRules.Evaluate(m, out string reason);
            Assert.AreEqual(CampaignOutcome.敗北, outcome);
            Assert.IsTrue(reason.Contains("民心"), $"理由に崩壊した柱が出ていない: {reason}");
        }

        [Test]
        public void HegemonyTarget_IsVictory()
        {
            var m = new CampaignMeters { hegemony = CampaignMeterRules.HegemonyTarget };
            Assert.AreEqual(CampaignOutcome.勝利, CampaignMeterRules.Evaluate(m, out string reason));
            Assert.IsTrue(reason.Contains("覇権"));
        }

        [Test]
        public void Victory_TakesPriorityOver_Defeat()
        {
            // 覇権達成と柱崩壊が同時でも勝利を優先（既存方針と同じ）
            var m = new CampaignMeters { hegemony = 1f, treasury = 0f };
            Assert.AreEqual(CampaignOutcome.勝利, CampaignMeterRules.Evaluate(m));
        }

        [Test]
        public void Apply_ScalesDelta_AndClamps()
        {
            var m = new CampaignMeters { military = 0.5f, treasury = 0.5f };
            var d = new MeterDelta(+0.2f, 0f, -0.2f, 0f, 0f);
            CampaignMeterRules.Apply(m, d, 0.5f); // 半額
            Assert.AreEqual(0.6f, m.military, 1e-4f);
            Assert.AreEqual(0.4f, m.treasury, 1e-4f);
        }

        [Test]
        public void Apply_Clamps_ToUnitRange()
        {
            var m = new CampaignMeters { military = 0.95f, support = 0.05f };
            CampaignMeterRules.Apply(m, new MeterDelta(+0.5f, -0.5f, 0f, 0f, 0f));
            Assert.AreEqual(1f, m.military, 1e-4f);
            Assert.AreEqual(0f, m.support, 1e-4f);
        }
    }
}
