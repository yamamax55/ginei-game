using NUnit.Framework;
using Ginei;
using IAParams = Ginei.InformationAsymmetryParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 情報の非対称と風説の相場（#1074）を固定する：情報優位（自分−市場）、情報裁定の利得（優位ありで取り・劣位で取られる）、
    /// 風説の相場変動（噂×信憑性×軽信）、逆選択（格差で市場が縮む）、情報拡散での優位減衰（早い者勝ち）、シグナリング費用の逓増。
    /// </summary>
    public class InformationAsymmetryRulesTests
    {
        // 情報優位：自分の情報が市場平均より多ければ正、少なければ負
        [Test]
        public void InformationEdge_OwnMinusMarket()
        {
            Assert.AreEqual(0.4f, InformationAsymmetryRules.InformationEdge(0.7f, 0.3f), 1e-5f);
            Assert.AreEqual(-0.3f, InformationAsymmetryRules.InformationEdge(0.2f, 0.5f), 1e-5f);
        }

        // 裁定利得：情報優位を持つ者が無知な相手から取る（知は力）。既定係数1で edge×size×gap。
        [Test]
        public void ArbitrageProfit_EdgePositive_TakesProfit()
        {
            var p = IAParams.Default;
            // edge0.5 × size10 × gap2 × gain1 = 10
            float profit = InformationAsymmetryRules.ArbitrageProfit(0.5f, 10f, 2f, p);
            Assert.AreEqual(10f, profit, 1e-4f);
        }

        // 裁定利得：情報劣位（負の優位）なら損を引く＝取られる
        [Test]
        public void ArbitrageProfit_EdgeNegative_LosesMoney()
        {
            var p = IAParams.Default;
            float loss = InformationAsymmetryRules.ArbitrageProfit(-0.5f, 10f, 2f, p);
            Assert.Less(loss, 0f);
            Assert.AreEqual(-10f, loss, 1e-4f);
        }

        // 風説：噂は真偽を問わず相場を動かす。強く・信じられ・市場が軽信なほど大きい。既定 impact0.5。
        [Test]
        public void RumorPriceMovement_StrongCredibleGullible_MovesMarket()
        {
            var p = IAParams.Default;
            // 1.0 × 1.0 × 1.0 × 0.5 = 0.5
            float full = InformationAsymmetryRules.RumorPriceMovement(1f, 1f, 1f, p);
            Assert.AreEqual(0.5f, full, 1e-4f);
            // 軽信が低いと動かない＝同じ噂でも市場が冷静なら相場は動かない
            float skeptical = InformationAsymmetryRules.RumorPriceMovement(1f, 1f, 0f, p);
            Assert.AreEqual(0f, skeptical, 1e-4f);
            Assert.Greater(full, skeptical);
        }

        // 逆選択：情報格差が大きいほど市場が縮む（レモン市場）。格差0で健全1・格差最大で2割。
        [Test]
        public void AdverseSelection_GapShrinksMarket()
        {
            var p = IAParams.Default; // decay0.8
            Assert.AreEqual(1f, InformationAsymmetryRules.AdverseSelection(0f, p), 1e-4f);
            // 1 − 1.0×0.8 = 0.2
            Assert.AreEqual(0.2f, InformationAsymmetryRules.AdverseSelection(1f, p), 1e-4f);
        }

        // 情報減衰：情報が広まると優位が消える（早い者勝ち）。拡散1・dt1で優位ゼロへ。
        [Test]
        public void InformationDecay_SpreadErasesEdge()
        {
            var p = IAParams.Default; // spreadDecayRate1
            // edge0.8、拡散1、dt0.5 → remain=1-0.5=0.5 → 0.4
            float half = InformationAsymmetryRules.InformationDecay(0.8f, 1f, 0.5f, p);
            Assert.AreEqual(0.4f, half, 1e-4f);
            // 拡散しきれば優位消失（remain0）
            float gone = InformationAsymmetryRules.InformationDecay(0.8f, 1f, 1f, p);
            Assert.AreEqual(0f, gone, 1e-4f);
        }

        // シグナリング費用：高い信憑性を得るほど非線形に高くつく（口先だけでは動かない）
        [Test]
        public void SignalingCost_RisesNonlinearly()
        {
            var p = IAParams.Default; // scale1
            float low = InformationAsymmetryRules.SignalingCost(0.5f, p);  // 0.25
            float high = InformationAsymmetryRules.SignalingCost(1f, p);   // 1.0
            Assert.AreEqual(0.25f, low, 1e-4f);
            Assert.AreEqual(1f, high, 1e-4f);
            // 二乗逓増＝信憑性2倍で費用4倍
            Assert.Greater(high, low * 2f);
        }
    }
}
