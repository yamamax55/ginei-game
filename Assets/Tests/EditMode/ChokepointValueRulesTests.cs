using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 要衝価値（AI・自動配備の判断材料）を固定する：迂回路の少なさ・経済流量・前線距離の点数化と
    /// 重み付き合成、守備優先度、要衝判定。イゼルローン型（迂回路0・前線直結）が満点になることを含む。
    /// </summary>
    public class ChokepointValueRulesTests
    {
        [Test]
        public void ScarcityValue_DecaysWithAlternativeRoutes()
        {
            // 唯一の道=最大1、迂回路1本で0.5、2本で1/3。負数は0本扱い。
            Assert.AreEqual(1f, ChokepointValueRules.ScarcityValue(0), 1e-4f);
            Assert.AreEqual(0.5f, ChokepointValueRules.ScarcityValue(1), 1e-4f);
            Assert.AreEqual(1f / 3f, ChokepointValueRules.ScarcityValue(2), 1e-4f);
            Assert.AreEqual(1f, ChokepointValueRules.ScarcityValue(-3), 1e-4f);
        }

        [Test]
        public void EconomicValue_ClampsToUnitRange()
        {
            Assert.AreEqual(0f, ChokepointValueRules.EconomicValue(-0.5f), 1e-4f);
            Assert.AreEqual(0.6f, ChokepointValueRules.EconomicValue(0.6f), 1e-4f);
            Assert.AreEqual(1f, ChokepointValueRules.EconomicValue(1.5f), 1e-4f);
        }

        [Test]
        public void FrontlineValue_DecaysWithDistance()
        {
            // 前線直結=最大1、距離=falloff で半減、遠いほど逓減。
            Assert.AreEqual(1f, ChokepointValueRules.FrontlineValue(0f, 2f), 1e-4f);
            Assert.AreEqual(0.5f, ChokepointValueRules.FrontlineValue(2f, 2f), 1e-4f);
            Assert.AreEqual(0.25f, ChokepointValueRules.FrontlineValue(6f, 2f), 1e-4f);
            Assert.AreEqual(1f, ChokepointValueRules.FrontlineValue(-1f, 2f), 1e-4f); // 負距離は0扱い
        }

        [Test]
        public void IserlohnTypeCorridor_IsMaxValueAndCritical()
        {
            // イゼルローン型＝迂回路0（唯一の道）・全交易が通る・前線直結 → 満点1.0。
            float value = ChokepointValueRules.TotalValue(0, 1f, 0f);
            Assert.AreEqual(1f, value, 1e-4f);
            Assert.IsTrue(ChokepointValueRules.IsCriticalChokepoint(value)); // 既定閾値0.7
        }

        [Test]
        public void BackwaterCorridor_HasLowValueAndIsNotCritical()
        {
            // 僻地＝迂回路4本・流量0.1・前線から遠い(距離20)。
            // 既定重み(0.5/0.2/0.3・falloff2)：0.5*0.2 + 0.2*0.1 + 0.3*(2/22) ≈ 0.1473
            float value = ChokepointValueRules.TotalValue(4, 0.1f, 20f);
            Assert.AreEqual(0.1473f, value, 1e-3f);
            Assert.IsFalse(ChokepointValueRules.IsCriticalChokepoint(value));
        }

        [Test]
        public void GarrisonPriority_HighValueUndefendedThreatened_IsHighest()
        {
            // 価値1×手薄(充足0)×脅威1 ＝最優先1.0。
            Assert.AreEqual(1f, ChokepointValueRules.GarrisonPriority(1f, 0f, 1f), 1e-4f);
            // 満員守備・無脅威はそれぞれ半減（平均の片翼が落ちる）。
            Assert.AreEqual(0.5f, ChokepointValueRules.GarrisonPriority(1f, 1f, 1f), 1e-4f);
            Assert.AreEqual(0.5f, ChokepointValueRules.GarrisonPriority(1f, 0f, 0f), 1e-4f);
            // 価値が低ければ比例して下がる。
            Assert.AreEqual(0.5f, ChokepointValueRules.GarrisonPriority(0.5f, 0f, 1f), 1e-4f);
            // 手薄×脅威大 ＞ 守備充足の同価値回廊。
            Assert.Greater(
                ChokepointValueRules.GarrisonPriority(0.8f, 0.2f, 0.9f),
                ChokepointValueRules.GarrisonPriority(0.8f, 0.9f, 0.9f));
        }

        [Test]
        public void Params_ClampInvalidValues_TotalValueZeroWhenNoWeights()
        {
            // ctor クランプ：負の重み→0、falloff→下限0.01、閾値→0..1。
            var p = new ChokepointParams(-1f, -1f, -1f, -5f, 2f);
            Assert.AreEqual(0f, p.scarcityWeight, 1e-4f);
            Assert.AreEqual(0f, p.economyWeight, 1e-4f);
            Assert.AreEqual(0f, p.frontlineWeight, 1e-4f);
            Assert.AreEqual(0.01f, p.frontlineFalloff, 1e-4f);
            Assert.AreEqual(1f, p.criticalThreshold, 1e-4f);
            // 重み合計0なら総合価値は0（ゼロ除算しない）。
            Assert.AreEqual(0f, ChokepointValueRules.TotalValue(0, 1f, 0f, p), 1e-4f);
        }
    }
}
