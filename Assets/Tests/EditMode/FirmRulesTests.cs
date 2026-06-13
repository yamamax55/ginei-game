using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// FirmRules（企業＝生産主体のミクロ経済ロジック・#1024）の EditMode テスト。
    /// 既定 FirmParams（撤退赤字許容0.5・倒産カバレッジ1.0・最低稼働0.1）で期待値を固定し、
    /// 「需要に応じた稼働・営業赤字での退出・利払い不能の倒産リスク」を担保する。
    /// </summary>
    public class FirmRulesTests
    {
        private FirmRules.FirmParams P => FirmRules.FirmParams.Default;

        /// <summary>生産量＝能力×稼働率（遊休は能力を使い切らない）。</summary>
        [Test]
        public void ProductionOutput_能力と稼働率の積()
        {
            Assert.AreEqual(60f, FirmRules.ProductionOutput(100f, 0.6f), 1e-4f);
            // 稼働率は0..1にクランプ・能力負はゼロ
            Assert.AreEqual(100f, FirmRules.ProductionOutput(100f, 1.5f), 1e-4f);
            Assert.AreEqual(0f, FirmRules.ProductionOutput(-10f, 1f), 1e-4f);
        }

        /// <summary>売上＝生産量×単価／営業利益＝売上−変動費−固定費／利益率＝営業利益/売上。</summary>
        [Test]
        public void RevenueProfitMargin_損益の連鎖()
        {
            float rev = FirmRules.Revenue(60f, 5f);
            Assert.AreEqual(300f, rev, 1e-4f);

            float op = FirmRules.OperatingProfit(rev, 100f, 50f);
            Assert.AreEqual(150f, op, 1e-4f); // 300-100-50

            Assert.AreEqual(0.5f, FirmRules.ProfitMargin(op, rev), 1e-4f);
            // 売上0は評価不能＝0
            Assert.AreEqual(0f, FirmRules.ProfitMargin(0f, 0f), 1e-4f);
        }

        /// <summary>営業利益は赤字（負）を許容する。</summary>
        [Test]
        public void OperatingProfit_赤字を許容()
        {
            float op = FirmRules.OperatingProfit(80f, 100f, 30f);
            Assert.AreEqual(-50f, op, 1e-4f); // 80-100-30
        }

        /// <summary>稼働率の決定＝需要が能力を下回れば遊休・上回ればフル稼働。</summary>
        [Test]
        public void CapacityUtilizationDecision_需要に応じて操業()
        {
            // 需要60 / 能力100 ＝ 0.6 で遊休
            Assert.AreEqual(0.6f, FirmRules.CapacityUtilizationDecision(60f, 100f, P), 1e-4f);
            // 需要が能力を超えればフル稼働1.0
            Assert.AreEqual(1f, FirmRules.CapacityUtilizationDecision(200f, 100f, P), 1e-4f);
        }

        /// <summary>需要が枯れても最低稼働率（0.1）を下回らない／能力0は操業不能。</summary>
        [Test]
        public void CapacityUtilizationDecision_最低稼働と操業不能()
        {
            Assert.AreEqual(0.1f, FirmRules.CapacityUtilizationDecision(0f, 100f, P), 1e-4f);
            Assert.AreEqual(0f, FirmRules.CapacityUtilizationDecision(50f, 0f, P), 1e-4f);
        }

        /// <summary>倒産リスク＝現金が利払いを賄えないほど高い（カバレッジ1未満で不足分がリスク）。</summary>
        [Test]
        public void SolvencyRisk_利払い不能で上昇()
        {
            // 現金50 / 利払い100 ＝ カバレッジ0.5 → リスク0.5
            Assert.AreEqual(0.5f, FirmRules.SolvencyRisk(50f, 0f, 100f), 1e-4f);
            // 現金が利払いを上回れば破綻なし0
            Assert.AreEqual(0f, FirmRules.SolvencyRisk(100f, 0f, 50f), 1e-4f);
            // 現金0＝全く賄えない → リスク1
            Assert.AreEqual(1f, FirmRules.SolvencyRisk(0f, 0f, 100f), 1e-4f);
            // 返済義務なし＝破綻なし0
            Assert.AreEqual(0f, FirmRules.SolvencyRisk(0f, 0f, 0f), 1e-4f);
        }

        /// <summary>撤退判断＝営業赤字が固定費の許容率（0.5）を超えたら退出。</summary>
        [Test]
        public void MarketExitDecision_営業赤字で退出()
        {
            // 固定費50・許容0.5 → 閾値 -25。-30 < -25 ＝ 退出
            Assert.IsTrue(FirmRules.MarketExitDecision(-30f, 50f, P));
            // -20 は閾値 -25 を下回らない＝残留
            Assert.IsFalse(FirmRules.MarketExitDecision(-20f, 50f, P));
            // 黒字は残留
            Assert.IsFalse(FirmRules.MarketExitDecision(100f, 50f, P));
        }
    }
}
