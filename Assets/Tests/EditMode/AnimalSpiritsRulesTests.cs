using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// AnimalSpiritsRules（KEYN-3 #1545・アニマルスピリッツ＝血気）の純ロジック検証。
    /// 既定 Params の具体値で期待値を固定し、投資意欲（心理が投資を駆動）・信認の変動（群集心理で増幅）・
    /// 投資凍結・悲観スパイラル（負）・楽観スパイラル（正）・美人投票・血気の回復・信認崩壊を担保する。
    /// </summary>
    public class AnimalSpiritsRulesTests
    {
        /// <summary>投資意欲＝信認×楽観×期待収益（積＝いずれか0なら意欲0）。</summary>
        [Test]
        public void InvestmentAppetite_信認楽観収益の積()
        {
            // 0.8×0.6×0.5 = 0.24
            Assert.AreEqual(0.24f, AnimalSpiritsRules.InvestmentAppetite(0.8f, 0.6f, 0.5f), 1e-4f);
            // 信認0なら意欲0（合理的に見合っても血気が支えねば投資は湧かない）
            Assert.AreEqual(0f, AnimalSpiritsRules.InvestmentAppetite(0f, 0.9f, 0.9f), 1e-4f);
        }

        /// <summary>信認は良いニュースで上がり悪いニュースで下がり、群集心理が振れを増幅する。</summary>
        [Test]
        public void ConfidenceTick_ニュースと群集心理で変動()
        {
            // 群集0＝増幅1倍：0.5 + 0.4*0.5*1*1 = 0.7
            Assert.AreEqual(0.7f, AnimalSpiritsRules.ConfidenceTick(0.5f, 0.4f, 0f, 1f), 1e-4f);
            // 群集1＝増幅(1+0.5)=1.5倍：0.5 + 0.4*0.5*1.5*1 = 0.8（群れるほど大きく振れる）
            Assert.AreEqual(0.8f, AnimalSpiritsRules.ConfidenceTick(0.5f, 0.4f, 1f, 1f), 1e-4f);
            // 悪いニュースで下がる：0.5 + (-0.4)*0.5*1*1 = 0.3
            Assert.AreEqual(0.3f, AnimalSpiritsRules.ConfidenceTick(0.5f, -0.4f, 0f, 1f), 1e-4f);
        }

        /// <summary>信認が閾値(0.3)を割ると投資が一斉に凍結する。</summary>
        [Test]
        public void InvestmentFreeze_信認が閾値割れで凍結()
        {
            Assert.IsTrue(AnimalSpiritsRules.InvestmentFreeze(0.2f));   // 0.3未満＝凍結
            Assert.IsFalse(AnimalSpiritsRules.InvestmentFreeze(0.4f));  // 閾値以上は凍結しない
            Assert.IsFalse(AnimalSpiritsRules.InvestmentFreeze(0.3f));  // 境界は凍結しない
        }

        /// <summary>需要不足が悲観を強める（負のスパイラル＝楽観が下がる）。</summary>
        [Test]
        public void PessimismSpiral_需要不足で楽観が下がる()
        {
            // 0.6 - 0.5*0.4*1 = 0.4
            Assert.AreEqual(0.4f, AnimalSpiritsRules.PessimismSpiral(0.6f, 0.5f, 1f), 1e-4f);
            // 需要不足0なら不変
            Assert.AreEqual(0.6f, AnimalSpiritsRules.PessimismSpiral(0.6f, 0f, 1f), 1e-4f);
        }

        /// <summary>好況が楽観を強める（正のスパイラル＝バブルの心理）。</summary>
        [Test]
        public void OptimismSpiral_好況で楽観が上がる()
        {
            // 0.5 + 0.6*0.3*1 = 0.68
            Assert.AreEqual(0.68f, AnimalSpiritsRules.OptimismSpiral(0.5f, 0.6f, 1f), 1e-4f);
            // 需要強さ0なら不変
            Assert.AreEqual(0.5f, AnimalSpiritsRules.OptimismSpiral(0.5f, 0f, 1f), 1e-4f);
        }

        /// <summary>美人投票＝自分の予想を市場のコンセンサスへ半分寄せる（他人の予想を予想する）。</summary>
        [Test]
        public void BeautyContest_自分とコンセンサスの中間へ寄る()
        {
            // Lerp(0.2, 0.8, 0.5) = 0.5
            Assert.AreEqual(0.5f, AnimalSpiritsRules.BeautyContest(0.2f, 0.8f), 1e-4f);
            // 一致なら不変
            Assert.AreEqual(0.7f, AnimalSpiritsRules.BeautyContest(0.7f, 0.7f), 1e-4f);
        }

        /// <summary>政策の信認回復シグナルが血気を取り戻す（伸びしろをシグナル比例で埋める）。</summary>
        [Test]
        public void SpiritRevival_政策シグナルで信認回復()
        {
            // 0.4 + (1-0.4)*0.8*0.5 = 0.4 + 0.24 = 0.64
            Assert.AreEqual(0.64f, AnimalSpiritsRules.SpiritRevival(0.4f, 0.8f), 1e-4f);
            // シグナル0なら不変
            Assert.AreEqual(0.4f, AnimalSpiritsRules.SpiritRevival(0.4f, 0f), 1e-4f);
        }

        /// <summary>信認が崩壊閾値(0.15)未満で総凍結＝信認崩壊と判定する（凍結よりさらに深い臨界）。</summary>
        [Test]
        public void IsConfidenceCollapse_信認崩壊の判定()
        {
            Assert.IsTrue(AnimalSpiritsRules.IsConfidenceCollapse(0.1f));    // 0.15未満＝崩壊
            Assert.IsFalse(AnimalSpiritsRules.IsConfidenceCollapse(0.2f));   // 凍結域だが崩壊ではない
            Assert.IsFalse(AnimalSpiritsRules.IsConfidenceCollapse(0.15f));  // 境界は崩壊しない
        }
    }
}
