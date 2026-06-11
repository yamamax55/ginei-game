using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 決勝点の識別（ジョミニ JOM-2・#1347）を固定する：地理的レバレッジ（接続次数×切断点性）・
    /// 戦略的重み（地理×経済×前線近接）・到達可能性（距離×敵防備×自軍機動）・決勝点スコア・占領波及・
    /// 優先目標化・逐次/同時適性・決勝点判定。既定 Params（次数基準4・切断点跳ね1.5・防備冪1.5・
    /// 決勝点閾値0.6・同時閾値0.6）。
    /// </summary>
    public class DecisivePointRulesTests
    {
        [Test]
        public void GeographicLeverage_CutVertexBoostsHighDegreePoint()
        {
            // 次数4=基準満額1.0、切断点でなければそのまま。
            Assert.AreEqual(1f, DecisivePointRules.GeographicLeverage(4, false), 1e-4f);
            // 次数2=0.5、切断点でなければ0.5。
            Assert.AreEqual(0.5f, DecisivePointRules.GeographicLeverage(2, false), 1e-4f);
            // 次数2で切断点なら1.5倍跳ね上げ：0.5×1.5=0.75。
            Assert.AreEqual(0.75f, DecisivePointRules.GeographicLeverage(2, true), 1e-4f);
            // 切断点が高次数なら上限1.0でクランプ。
            Assert.AreEqual(1f, DecisivePointRules.GeographicLeverage(4, true), 1e-4f);
            // 次数0や負はクランプ。
            Assert.AreEqual(0f, DecisivePointRules.GeographicLeverage(-2, true), 1e-4f);
        }

        [Test]
        public void StrategicWeight_RequiresGeographyEconomyAndFront()
        {
            // 三要素そろえば重み最大。
            Assert.AreEqual(1f, DecisivePointRules.StrategicWeight(1f, 1f, 1f), 1e-4f);
            // どれか0なら0（後方の無価値な要所は決勝点にならない）。
            Assert.AreEqual(0f, DecisivePointRules.StrategicWeight(1f, 0f, 1f), 1e-4f);
            Assert.AreEqual(0f, DecisivePointRules.StrategicWeight(1f, 1f, 0f), 1e-4f);
            // 0.5×0.8×0.5 = 0.2。
            Assert.AreEqual(0.2f, DecisivePointRules.StrategicWeight(0.5f, 0.8f, 0.5f), 1e-4f);
        }

        [Test]
        public void Attainability_HeavyGarrisonIsUnreachableButMobilityOffsetsDistance()
        {
            // 近く・敵なし・機動不要なら満額到達。
            Assert.AreEqual(1f, DecisivePointRules.Attainability(0f, 0f, 0f), 1e-4f);
            // 重防備は機動でも越えられない＝到達不能。
            Assert.AreEqual(0f, DecisivePointRules.Attainability(0f, 1f, 1f), 1e-4f);
            // 遠いが機動が高ければ距離を相殺して到達できる。
            Assert.AreEqual(1f, DecisivePointRules.Attainability(1f, 0f, 1f), 1e-4f);
            // 遠く機動なしは到達不能。
            Assert.AreEqual(0f, DecisivePointRules.Attainability(1f, 0f, 0f), 1e-4f);
            // 中距離・敵なし・機動なし：(1-0.5)*(1-0)=0.5。
            Assert.AreEqual(0.5f, DecisivePointRules.Attainability(0.5f, 0f, 0f), 1e-4f);
        }

        [Test]
        public void DecisivePointScore_UnreachableWeightYieldsZero()
        {
            // 重く・かつ届く点はスコアが立つ：0.8×0.5=0.4。
            Assert.AreEqual(0.4f, DecisivePointRules.DecisivePointScore(0.8f, 0.5f), 1e-4f);
            // 届かなければ価値があってもスコア0（作戦線を向けられない）。
            Assert.AreEqual(0f, DecisivePointRules.DecisivePointScore(1f, 0f), 1e-4f);
        }

        [Test]
        public void CaptureImpact_HighLeverageCaptureSplitsEnemy()
        {
            // 高レバレッジ（切断点）占領は最大の分断波及。
            Assert.AreEqual(1f, DecisivePointRules.CaptureImpact(1f), 1e-4f);
            // 中レバレッジは二乗で割り引かれる：0.5^2=0.25。
            Assert.AreEqual(0.25f, DecisivePointRules.CaptureImpact(0.5f), 1e-4f);
            // 結節でない点の占領は波及なし。
            Assert.AreEqual(0f, DecisivePointRules.CaptureImpact(0f), 1e-4f);
        }

        [Test]
        public void PriorityRank_HigherScoreIsTargetedFirst()
        {
            // A が高ければ A 優先＝-1（小さいほど優先）。
            Assert.AreEqual(-1, DecisivePointRules.PriorityRank(0.8f, 0.5f));
            // B が高ければ B 優先＝1。
            Assert.AreEqual(1, DecisivePointRules.PriorityRank(0.5f, 0.8f));
            // 同点なら0（裁定なし）。
            Assert.AreEqual(0, DecisivePointRules.PriorityRank(0.5f, 0.5f));
        }

        [Test]
        public void SequentialVsSimultaneous_AbundantForceEnablesSimultaneous()
        {
            // 決勝点が1つなら戦力をその一点へ集中＝同時適性はそのまま戦力。
            Assert.AreEqual(0.8f, DecisivePointRules.SequentialVsSimultaneous(1, 0.8f), 1e-4f);
            // 2点へ戦力0.8：1点あたり0.4、閾値0.6で割って0.6667。
            Assert.AreEqual(0.6667f, DecisivePointRules.SequentialVsSimultaneous(2, 0.8f), 1e-3f);
            // 4点へ同じ戦力なら1点あたり0.2＝同時適性が下がる（逐次が無難）：0.2/0.6=0.3333。
            Assert.AreEqual(0.3333f, DecisivePointRules.SequentialVsSimultaneous(4, 0.8f), 1e-3f);
            // 点数が増えるほど同時適性は下がる。
            Assert.Greater(
                DecisivePointRules.SequentialVsSimultaneous(2, 0.8f),
                DecisivePointRules.SequentialVsSimultaneous(4, 0.8f));
        }

        [Test]
        public void IsDecisivePoint_ThresholdAtSixTenths()
        {
            // 既定閾値0.6：以上なら決勝点、未満なら決勝点でない。
            Assert.IsTrue(DecisivePointRules.IsDecisivePoint(0.6f));
            Assert.IsTrue(DecisivePointRules.IsDecisivePoint(0.8f));
            Assert.IsFalse(DecisivePointRules.IsDecisivePoint(0.59f));
        }

        [Test]
        public void DecisivePoint_CutVertexButUnreachable_IsNotDecisive()
        {
            // 物語：切断点で地理的レバレッジは最大級・経済も前線価値も高いが、遠く重防備で到達不能なら
            // 決勝点にならない＝届かない要点に作戦線を向けても決定的優位は得られない（ジョミニの核）。
            float geo = DecisivePointRules.GeographicLeverage(3, true); // 0.75×1.5→クランプ1.0
            Assert.AreEqual(1f, geo, 1e-4f);
            float weight = DecisivePointRules.StrategicWeight(geo, 1f, 1f);
            Assert.AreEqual(1f, weight, 1e-4f);
            // 遠く（距離1）・重防備（敵1）・機動なし＝到達不能。
            float reach = DecisivePointRules.Attainability(1f, 1f, 0f);
            Assert.AreEqual(0f, reach, 1e-4f);
            float score = DecisivePointRules.DecisivePointScore(weight, reach);
            Assert.AreEqual(0f, score, 1e-4f);
            Assert.IsFalse(DecisivePointRules.IsDecisivePoint(score));

            // 対照：同じ要点が手薄かつ近ければ到達でき、決勝点になる。
            float reachOpen = DecisivePointRules.Attainability(0.2f, 0.1f, 0.8f);
            float scoreOpen = DecisivePointRules.DecisivePointScore(weight, reachOpen);
            Assert.IsTrue(DecisivePointRules.IsDecisivePoint(scoreOpen));
        }

        [Test]
        public void Params_ClampInvalidValues()
        {
            // ctor クランプ：次数基準/跳ね上げ/防備冪は下限1、閾値は0..1。
            var p = new DecisivePointParams(-5f, 0.2f, -3f, 2f, -1f);
            Assert.AreEqual(1f, p.degreeNorm, 1e-4f);
            Assert.AreEqual(1f, p.cutVertexBoost, 1e-4f);
            Assert.AreEqual(1f, p.garrisonExponent, 1e-4f);
            Assert.AreEqual(1f, p.decisiveThreshold, 1e-4f);
            Assert.AreEqual(0f, p.simultaneousThreshold, 1e-4f);
        }
    }
}
