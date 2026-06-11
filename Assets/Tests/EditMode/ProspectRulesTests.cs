using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// プロスペクト理論（KAHN-1 #1833・カーネマン）を固定する：参照点からの相対評価、
    /// 損失回避（lambda≈2.25 倍重い価値関数）、利得=リスク回避／損失=リスク選好、
    /// 確率の逆S字重み付け（低確率を過大評価）、保有効果、参照点の順応。
    /// Pow を多用するため許容誤差は緩め。期待値は電卓的に検算しコメントに残す。
    /// </summary>
    public class ProspectRulesTests
    {
        private static readonly ProspectParams P = ProspectParams.Default;
        // alpha=0.88, lambda=2.25, beta=0.61, adaptationRate=0.3

        [Test]
        public void RelativeOutcome_IsChangeFromReference()
        {
            Assert.AreEqual(30f, ProspectRules.RelativeOutcome(130f, 100f), 1e-5f); // 参照点上＝利得
            Assert.AreEqual(-20f, ProspectRules.RelativeOutcome(80f, 100f), 1e-5f); // 参照点下＝損失
            Assert.AreEqual(0f, ProspectRules.RelativeOutcome(100f, 100f), 1e-5f);  // 参照点どおり
        }

        [Test]
        public void PerceivedValue_GainIsConcave()
        {
            // v(10)=10^0.88≈7.586, v(0)=0
            Assert.AreEqual(7.586f, ProspectRules.PerceivedValue(10f, P), 5e-3f);
            Assert.AreEqual(0f, ProspectRules.PerceivedValue(0f, P), 1e-5f);
            // 凹性：v(10) < 10*v(1)。v(1)=1 なので 7.586 < 10
            Assert.Less(ProspectRules.PerceivedValue(10f, P), 10f * ProspectRules.PerceivedValue(1f, P));
        }

        [Test]
        public void PerceivedValue_LossIsHeavierByLambda()
        {
            // v(-10) = -2.25 * 10^0.88 ≈ -2.25*7.586 = -17.07
            float gain = ProspectRules.PerceivedValue(10f, P);
            float loss = ProspectRules.PerceivedValue(-10f, P);
            Assert.AreEqual(-17.069f, loss, 1e-2f);
            // 同額の損失は利得より lambda 倍重い（絶対値比≈2.25）
            Assert.AreEqual(2.25f, -loss / gain, 5e-3f);
        }

        [Test]
        public void RiskAttitude_GainAverseLossSeeking()
        {
            // 利得局面＝リスク回避（正）
            Assert.Greater(ProspectRules.RiskAttitude(10f), 0f);
            // 損失局面＝リスク選好（負）
            Assert.Less(ProspectRules.RiskAttitude(-10f), 0f);
            // 参照点上＝中立
            Assert.AreEqual(0f, ProspectRules.RiskAttitude(0f), 1e-5f);
            // 対称性：±同額は逆符号・同絶対値（x/(1+x)）
            Assert.AreEqual(-ProspectRules.RiskAttitude(5f), ProspectRules.RiskAttitude(-5f), 1e-5f);
        }

        [Test]
        public void DecisionWeight_InverseSAndEndpoints()
        {
            // 端点はそのまま
            Assert.AreEqual(0f, ProspectRules.DecisionWeight(0f, P), 1e-5f);
            Assert.AreEqual(1f, ProspectRules.DecisionWeight(1f, P), 1e-5f);
            // w(0.5)≈0.4205（0.5 をやや過小評価）
            Assert.AreEqual(0.4205f, ProspectRules.DecisionWeight(0.5f, P), 5e-3f);
            // 低確率の過大評価：w(0.01)≈0.0552 > 0.01
            float w01 = ProspectRules.DecisionWeight(0.01f, P);
            Assert.AreEqual(0.0552f, w01, 5e-3f);
            Assert.Greater(w01, 0.01f);
        }

        [Test]
        public void EndowmentEffect_LossWeightedValuation()
        {
            // 手放す痛み＝|v(-10)|≈17.07（名目10より大きい＝保有効果）
            float endow = ProspectRules.EndowmentEffect(10f, P);
            Assert.AreEqual(17.069f, endow, 1e-2f);
            Assert.Greater(endow, 10f);
            // 負の保有はクランプして0
            Assert.AreEqual(0f, ProspectRules.EndowmentEffect(-5f, P), 1e-5f);
        }

        [Test]
        public void ReferencePointShift_AdaptsTowardRecent()
        {
            // 100 から直近 200 へ adaptationRate=0.3 ぶん寄る → 100+0.3*(200-100)=130
            Assert.AreEqual(130f, ProspectRules.ReferencePointShift(100f, 200f, P), 1e-3f);
            // 順応ゼロなら不変
            ProspectParams noAdapt = new ProspectParams(0.88f, 2.25f, 0.61f, 0f);
            Assert.AreEqual(100f, ProspectRules.ReferencePointShift(100f, 200f, noAdapt), 1e-5f);
        }

        [Test]
        public void Narrative_LossLoomsLargerAndDrivesRiskSeeking()
        {
            // 物語：同額(±50)でも損失が利得より重く、損失局面では確実な損より賭けに出る。
            float gainValue = ProspectRules.PerceivedValue(50f, P);
            float lossValue = ProspectRules.PerceivedValue(-50f, P);
            // (1) 損失は利得より重い（絶対値）
            Assert.Greater(-lossValue, gainValue, "同額でも損失の主観的痛みが利得の喜びを上回る");

            // (2) 損失局面ではリスク選好＝確実な-50 より、五分五分で-100/0 の賭けを好む。
            //     確実な損の主観価値 v(-50) を、賭けの見込み価値（半分の確率で-100）と比べる。
            float sureLoss = ProspectRules.PerceivedValue(-50f, P);
            float gambleLoss = ProspectRules.ProspectValue(-100f, 0.5f, P); // 0.5で-100、残りは0
            // 賭けの方が痛みが浅い（値が大きい＝マシ）＝損を避けて賭けに出る誘因
            Assert.Greater(gambleLoss, sureLoss, "損失局面では確実な損を避けて賭けに出る（リスク選好）");

            // (3) リスク態度の符号が局面で反転する
            Assert.Greater(ProspectRules.RiskAttitude(50f), 0f);
            Assert.Less(ProspectRules.RiskAttitude(-50f), 0f);
        }
    }
}
