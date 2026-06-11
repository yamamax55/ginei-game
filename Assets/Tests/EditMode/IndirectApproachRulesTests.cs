using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 間接的アプローチ（リデルハート LDH-1・#1339）を固定する：最小予期線の評価＝経路が予期される度合い・
    /// 期待外れスコア・経路上の抵抗（予期された道ほど固い）・意表×心理的動揺の利得・分岐の惑わし（デュアル
    /// スレット）・正面直撃のコスト・総合評価のトレードオフ・間接アプローチ判定。
    /// 既定 Params（直接性0.6/警戒0.4・間接閾値0.6・分岐冪1.5・コスト重み0.5）。
    /// </summary>
    public class IndirectApproachRulesTests
    {
        [Test]
        public void ExpectationLevel_IsWeightedBlendOfDirectnessAndAttention()
        {
            // 既定 直接性0.6/警戒0.4：直接1×警戒1=1.0、直接1×警戒0=0.6、直接0×警戒1=0.4。
            Assert.AreEqual(1f, IndirectApproachRules.ExpectationLevel(1f, 1f), 1e-4f);
            Assert.AreEqual(0.6f, IndirectApproachRules.ExpectationLevel(1f, 0f), 1e-4f);
            Assert.AreEqual(0.4f, IndirectApproachRules.ExpectationLevel(0f, 1f), 1e-4f);
            // 入力はクランプされる。
            Assert.AreEqual(0f, IndirectApproachRules.ExpectationLevel(-1f, -1f), 1e-4f);
        }

        [Test]
        public void LeastExpectationScore_IsComplementOfExpectation()
        {
            // 予期される度合いの裏返し＝予期されないほど高い。
            Assert.AreEqual(0.4f, IndirectApproachRules.LeastExpectationScore(0.6f), 1e-4f);
            Assert.AreEqual(1f, IndirectApproachRules.LeastExpectationScore(0f), 1e-4f);
            Assert.AreEqual(0f, IndirectApproachRules.LeastExpectationScore(1f), 1e-4f);
        }

        [Test]
        public void ResistanceOnPath_ExpectedPathsAreBetterDefended()
        {
            // 予期される度合い1＝基礎防御がそのまま実効抵抗に。
            Assert.AreEqual(0.8f, IndirectApproachRules.ResistanceOnPath(1f, 0.8f), 1e-4f);
            // 予期される度合い0.4＝守りが手薄＝抵抗が薄まる（0.8×0.4=0.32）。
            Assert.AreEqual(0.32f, IndirectApproachRules.ResistanceOnPath(0.4f, 0.8f), 1e-4f);
            // 予期されなければ抵抗ゼロ。
            Assert.AreEqual(0f, IndirectApproachRules.ResistanceOnPath(0f, 0.8f), 1e-4f);
        }

        [Test]
        public void IndirectAdvantage_NeedsBothSurpriseAndDislocation()
        {
            // 意表0.8×動揺0.5=0.4。
            Assert.AreEqual(0.4f, IndirectApproachRules.IndirectAdvantage(0.8f, 0.5f), 1e-4f);
            // 動揺ゼロなら意表を突いても利得なし。
            Assert.AreEqual(0f, IndirectApproachRules.IndirectAdvantage(0.8f, 0f), 1e-4f);
            // 意表ゼロなら動揺させても利得なし。
            Assert.AreEqual(0f, IndirectApproachRules.IndirectAdvantage(0f, 0.5f), 1e-4f);
        }

        [Test]
        public void PathFlexibility_MoreBranchesConfuseTheEnemy()
        {
            // 単一目標（分岐1）＝惑わしなし。
            Assert.AreEqual(0f, IndirectApproachRules.PathFlexibility(1), 1e-4f);
            // 分岐0以下も0（空安全）。
            Assert.AreEqual(0f, IndirectApproachRules.PathFlexibility(0), 1e-4f);
            // 2目標：raw=1/2=0.5、0.5^(1/1.5)≈0.62996（Pow箇所＝許容を緩める）。
            Assert.AreEqual(0.62996f, IndirectApproachRules.PathFlexibility(2), 1e-3f);
            // 分岐が増えるほど単調増（3目標 > 2目標）。
            Assert.Greater(IndirectApproachRules.PathFlexibility(3), IndirectApproachRules.PathFlexibility(2));
        }

        [Test]
        public void DirectCostPenalty_ScalesWithConcentration()
        {
            // 正面集中が高いほど直撃コストが高い。
            Assert.AreEqual(0.7f, IndirectApproachRules.DirectCostPenalty(0.7f), 1e-4f);
            Assert.AreEqual(0f, IndirectApproachRules.DirectCostPenalty(0f), 1e-4f);
            // クランプ。
            Assert.AreEqual(1f, IndirectApproachRules.DirectCostPenalty(2f), 1e-4f);
        }

        [Test]
        public void ApproachScore_TradesSurpriseAgainstPathCost()
        {
            // 期待外れ0.8からコスト0.4をコスト重み0.5で割り引く＝0.8−0.2=0.6。
            Assert.AreEqual(0.6f, IndirectApproachRules.ApproachScore(0.8f, 0.4f), 1e-4f);
            // 遠回りゼロなら期待外れがそのまま評価に。
            Assert.AreEqual(0.8f, IndirectApproachRules.ApproachScore(0.8f, 0f), 1e-4f);
            // 過大な遠回りはマイナスへ振れてクランプで0。
            Assert.AreEqual(0f, IndirectApproachRules.ApproachScore(0.2f, 1f), 1e-4f);
        }

        [Test]
        public void IsIndirectApproach_ThresholdAtSixTenths()
        {
            // 既定閾値0.6：期待外れ度がこれ以上なら間接アプローチ。
            Assert.IsTrue(IndirectApproachRules.IsIndirectApproach(0.6f));
            Assert.IsTrue(IndirectApproachRules.IsIndirectApproach(0.8f));
            Assert.IsFalse(IndirectApproachRules.IsIndirectApproach(0.59f));
        }

        [Test]
        public void Narrative_DetourBeatsShortestFrontalAssault()
        {
            // 物語：固めた正面への最短突撃 vs 警戒の薄い迂回。最短≠最善。
            // 正面＝直接性1・敵警戒0.8・遠回りコスト0.1（近い）。
            float frontalExp = IndirectApproachRules.ExpectationLevel(1f, 0.8f);
            float frontalLeast = IndirectApproachRules.LeastExpectationScore(frontalExp);
            float frontalScore = IndirectApproachRules.ApproachScore(frontalLeast, 0.1f);
            // 迂回＝直接性0.2・敵警戒0.2・遠回りコスト0.5（遠い）。
            float detourExp = IndirectApproachRules.ExpectationLevel(0.2f, 0.2f);
            float detourLeast = IndirectApproachRules.LeastExpectationScore(detourExp);
            float detourScore = IndirectApproachRules.ApproachScore(detourLeast, 0.5f);

            // 正面は予期されて間接アプローチでなく、迂回は最小予期線に乗る。
            Assert.IsFalse(IndirectApproachRules.IsIndirectApproach(frontalLeast));
            Assert.IsTrue(IndirectApproachRules.IsIndirectApproach(detourLeast));
            // 遠回りでも、意表を突く迂回が総合評価で最短正面突撃に勝つ。
            Assert.Greater(detourScore, frontalScore);
        }
    }
}
