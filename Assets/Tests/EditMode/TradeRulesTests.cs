using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 星間交易を固定する：総利得は量×補完性、取り分は交渉力、仲介者は断絶時のみ口銭を抜く
    /// （戦争が仲介者を太らせる）、断絶損失、対一国依存の危険判定。境界を担保。
    /// </summary>
    public class TradeRulesTests
    {
        private static readonly TradeParams P = TradeParams.Default;
        // 利得1/補完+100%/口銭10%/依存閾値30%

        [Test]
        public void TotalGain_VolumeTimesComplementarity()
        {
            Assert.AreEqual(100f, TradeRules.TotalGain(100f, 0f, P), 1e-4f);   // 補完なし＝素の利得
            Assert.AreEqual(200f, TradeRules.TotalGain(100f, 1f, P), 1e-4f);   // 完全補完＝倍
            Assert.AreEqual(150f, TradeRules.TotalGain(100f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void ShareOfGain_ByBargainingPower()
        {
            Assert.AreEqual(50f, TradeRules.ShareOfGain(100f, 0.5f), 1e-4f);  // 対等＝折半
            Assert.AreEqual(70f, TradeRules.ShareOfGain(100f, 0.7f), 1e-4f);  // 強い側が多く取る
            Assert.AreEqual(0f, TradeRules.ShareOfGain(100f, 0f), 1e-5f);
        }

        [Test]
        public void BrokerProfit_OnlyWhenDirectTradeBlocked()
        {
            // 平時＝直接取引＝中抜きの余地なし
            Assert.AreEqual(0f, TradeRules.BrokerProfit(100f, false, P), 1e-5f);
            // 交戦で断絶＝仲介者が口銭10%を抜く（戦争が仲介者を太らせる）
            Assert.AreEqual(10f, TradeRules.BrokerProfit(100f, true, P), 1e-4f);
        }

        [Test]
        public void WarDisruptionLoss_OpportunityCostOfWar()
        {
            // 量100・補完0.5・対等＝150×0.5=75 の取り分が消える
            Assert.AreEqual(75f, TradeRules.WarDisruptionLoss(100f, 0.5f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void Dependence_ShareOfTotalTrade()
        {
            Assert.AreEqual(0.3f, TradeRules.Dependence(30f, 100f), 1e-5f);
            Assert.AreEqual(0f, TradeRules.Dependence(30f, 0f), 1e-5f);   // 総額0＝0
            Assert.AreEqual(1f, TradeRules.Dependence(200f, 100f), 1e-5f); // クランプ
        }

        [Test]
        public void IsDangerouslyDependent_AtThreshold()
        {
            Assert.IsTrue(TradeRules.IsDangerouslyDependent(30f, 100f, P));  // 30%ちょうど＝危険
            Assert.IsFalse(TradeRules.IsDangerouslyDependent(29f, 100f, P));
        }
    }
}
