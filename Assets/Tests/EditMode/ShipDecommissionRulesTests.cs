using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦の退役・除籍を固定する：艦齢×世代差で旧式化し残存価値が削れる・老朽ほど維持コストが高い・
    /// 改修妥当性と退役切迫度の天秤・予備役/解体の価値・改修より退役が勝てば除籍。クランプを担保。
    /// </summary>
    public class ShipDecommissionRulesTests
    {
        private static readonly ShipDecommissionParams P = ShipDecommissionParams.Default; // 旧式化基準40/世代重み0.25/老朽重み0.5/負担基準40/改修費下限1/価値下限1

        [Test]
        public void Obsolescence_AgeAndGenerationCombine()
        {
            // 艦齢20/40=0.5 ＋ 世代差2×0.25=0.5 ＝ 1.0
            Assert.AreEqual(1f, ShipDecommissionRules.Obsolescence(20f, 3f, 5f, P), 1e-4f);
            // 艦齢のみ：10/40=0.25・同世代
            Assert.AreEqual(0.25f, ShipDecommissionRules.Obsolescence(10f, 5f, 5f, P), 1e-4f);
            // 老齢で頭打ち：60/40=1.5→1
            Assert.AreEqual(1f, ShipDecommissionRules.Obsolescence(60f, 5f, 5f, P), 1e-4f);
        }

        [Test]
        public void Obsolescence_ClampsAndIgnoresNewerGeneration()
        {
            Assert.AreEqual(0f, ShipDecommissionRules.Obsolescence(-10f, 5f, 5f, P), 1e-4f); // 負艦齢はクランプ
            // 自艦が現行より新しい世代でも旧式化に負寄与しない（genGap=0）
            Assert.AreEqual(0.25f, ShipDecommissionRules.Obsolescence(10f, 8f, 5f, P), 1e-4f);
        }

        [Test]
        public void RemainingServiceValue_ErodedByObsolescence()
        {
            Assert.AreEqual(70f, ShipDecommissionRules.RemainingServiceValue(100f, 0.3f, P), 1e-4f); // 100×0.7
            Assert.AreEqual(0f, ShipDecommissionRules.RemainingServiceValue(100f, 1f, P), 1e-4f);    // 完全旧式は無価値
            Assert.AreEqual(0f, ShipDecommissionRules.RemainingServiceValue(-50f, 0.3f, P), 1e-4f);  // 負戦力はクランプ
        }

        [Test]
        public void MaintenanceBurden_HighForOldAndWornHulls()
        {
            Assert.AreEqual(1f, ShipDecommissionRules.MaintenanceBurden(40f, 1f, P), 1e-4f);     // 老齢でも状態満点なら基礎のみ
            Assert.AreEqual(1.25f, ShipDecommissionRules.MaintenanceBurden(40f, 0.5f, P), 1e-4f); // 1×(1+0.5×0.5)
            Assert.AreEqual(0.625f, ShipDecommissionRules.MaintenanceBurden(20f, 0.5f, P), 1e-4f); // 若い艦は負担が軽い
        }

        [Test]
        public void ModernizationViability_GoodHullCheapUpgrade()
        {
            Assert.AreEqual(1.6f, ShipDecommissionRules.ModernizationViability(0.8f, 50f, 100f, P), 1e-4f); // 0.8×100/50
            // ボロ船は妥当性が低い（状態0.2）
            Assert.AreEqual(0.4f, ShipDecommissionRules.ModernizationViability(0.2f, 50f, 100f, P), 1e-4f);
            // 改修費は下限クランプ＝ゼロ割しない
            Assert.AreEqual(80f, ShipDecommissionRules.ModernizationViability(0.8f, 0f, 100f, P), 1e-4f); // 0.8×100/1
        }

        [Test]
        public void MothballValue_And_ScrapRecovery()
        {
            Assert.AreEqual(50f, ShipDecommissionRules.MothballValue(70f, 20f, P), 1e-4f);  // 残存70−保管20
            Assert.AreEqual(70f, ShipDecommissionRules.MothballValue(70f, -5f, P), 1e-4f);  // 負コストはクランプ
            Assert.AreEqual(300f, ShipDecommissionRules.ScrapRecovery(1000f, 0.3f, P), 1e-4f); // 質量×資材価値
            Assert.AreEqual(0f, ShipDecommissionRules.ScrapRecovery(-100f, 0.3f, P), 1e-4f);   // 負質量はクランプ
        }

        [Test]
        public void RetirementUrgency_BurdenOverValue()
        {
            Assert.AreEqual(0.05f, ShipDecommissionRules.RetirementUrgency(1.25f, 25f, P), 1e-4f); // 1.25/25＝価値あれば切迫せず
            Assert.AreEqual(1f, ShipDecommissionRules.RetirementUrgency(1f, 0.5f, P), 1e-4f);      // 価値下限1で割って頭打ち
            Assert.AreEqual(0f, ShipDecommissionRules.RetirementUrgency(0f, 25f, P), 1e-4f);       // 負担ゼロは切迫なし
        }

        [Test]
        public void ShouldDecommission_RetireWhenUrgencyBeatsModernization()
        {
            Assert.IsTrue(ShipDecommissionRules.ShouldDecommission(0.5f, 0.3f, 0.1f));  // 0.5 > 0.3+0.1
            Assert.IsFalse(ShipDecommissionRules.ShouldDecommission(0.5f, 0.4f, 0.1f)); // 0.5 = 0.5 で同点は退役せず
            Assert.IsTrue(ShipDecommissionRules.ShouldDecommission(0.6f, 0.3f));        // 既定閾値0.1：0.6 > 0.4
            Assert.IsFalse(ShipDecommissionRules.ShouldDecommission(0.3f, 0.3f));       // 改修に分があれば温存
        }

        [Test]
        public void AgingHulkStory_OldShipGetsScrappedNotModernized()
        {
            // 物語：旧式化した老朽艦。世代遅れ・船体ボロ・残存価値薄く維持費だけかさむ → 改修より退役が勝つ
            float obs = ShipDecommissionRules.Obsolescence(45f, 2f, 5f, P);            // 艦齢頭打ち1.0（+世代差）→1.0
            Assert.AreEqual(1f, obs, 1e-4f);
            float value = ShipDecommissionRules.RemainingServiceValue(100f, obs, P);  // 完全旧式＝残存0
            Assert.AreEqual(0f, value, 1e-4f);
            float burden = ShipDecommissionRules.MaintenanceBurden(45f, 0.3f, P);     // ageFactor 1×(1+0.5×0.7)=1.35
            Assert.AreEqual(1.35f, burden, 1e-4f);
            float viab = ShipDecommissionRules.ModernizationViability(0.3f, 80f, value, P); // 残存0なら改修も無意味＝0
            Assert.AreEqual(0f, viab, 1e-4f);
            float urgency = ShipDecommissionRules.RetirementUrgency(burden, value, P); // 1.35/価値下限1→頭打ち1
            Assert.AreEqual(1f, urgency, 1e-4f);
            Assert.IsTrue(ShipDecommissionRules.ShouldDecommission(urgency, viab)); // 既定閾値で退役確定
            // 解体すれば資材を取り戻せる（眠らせるより回収が前向き）
            Assert.Greater(ShipDecommissionRules.ScrapRecovery(800f, 0.25f, P), 0f);
        }
    }
}
