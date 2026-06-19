using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>情報統合の純ロジック（IntelFusionRules）の EditMode テスト。既定 Params で期待値を固定。</summary>
    public class IntelFusionRulesTests
    {
        const float Eps = 1e-4f;
        const float EpsLoose = 1e-3f;

        [Test]
        public void FusedConfidence_IndependentSources_Combine()
        {
            // 1-(1-0.5)(1-0.5)=0.75（独立2ソースで単一より精度↑）。
            Assert.AreEqual(0.75f, IntelFusionRules.FusedConfidence(new[] { 0.5f, 0.5f }), EpsLoose);
            // 1ソースはその値そのもの。
            Assert.AreEqual(0.6f, IntelFusionRules.FusedConfidence(new[] { 0.6f }), Eps);
        }

        [Test]
        public void FusedConfidence_NullOrEmpty_IsZero()
        {
            Assert.AreEqual(0f, IntelFusionRules.FusedConfidence(null), Eps);
            Assert.AreEqual(0f, IntelFusionRules.FusedConfidence(new float[0]), Eps);
        }

        [Test]
        public void CorroborationBonus_MoreAgreeingSources_RaiseConfidence()
        {
            // 2ソース全一致＝0.3*(1-1/2)*1=0.15。
            Assert.AreEqual(0.15f, IntelFusionRules.CorroborationBonus(2, 1f), Eps);
            // 4ソース全一致＝0.3*(1-1/4)*1=0.225（ソースが増えるほど大きい）。
            Assert.AreEqual(0.225f, IntelFusionRules.CorroborationBonus(4, 1f), Eps);
            // 1ソース以下はボーナスなし。
            Assert.AreEqual(0f, IntelFusionRules.CorroborationBonus(1, 1f), Eps);
        }

        [Test]
        public void ContradictionPenalty_Disagreement_LowersConfidence()
        {
            // 0.4*0.5=0.2。
            Assert.AreEqual(0.2f, IntelFusionRules.ContradictionPenalty(0.5f), Eps);
            Assert.AreEqual(0f, IntelFusionRules.ContradictionPenalty(0f), Eps);
        }

        [Test]
        public void DeceptionFilter_EnemyDeception_DiscountsConfidence()
        {
            // 0.8*(1-0.5*0.5)=0.6。
            Assert.AreEqual(0.6f, IntelFusionRules.DeceptionFilter(0.8f, 0.5f), Eps);
            // 欺瞞ゼロなら不変。
            Assert.AreEqual(0.8f, IntelFusionRules.DeceptionFilter(0.8f, 0f), Eps);
        }

        [Test]
        public void TimelinessFactor_OlderIntel_DecaysToward_Zero()
        {
            // age=0 で 1。
            Assert.AreEqual(1f, IntelFusionRules.TimelinessFactor(0f), Eps);
            // 1/(1+0.1*10)=0.5。
            Assert.AreEqual(0.5f, IntelFusionRules.TimelinessFactor(10f), EpsLoose);
            // 負の age は 0 にクランプ＝1。
            Assert.AreEqual(1f, IntelFusionRules.TimelinessFactor(-5f), Eps);
        }

        [Test]
        public void PictureCompleteness_And_ActionableIntel_And_Reliable()
        {
            // 3/4=0.75。総数0以下は0。
            Assert.AreEqual(0.75f, IntelFusionRules.PictureCompleteness(3, 4), Eps);
            Assert.AreEqual(0f, IntelFusionRules.PictureCompleteness(3, 0), Eps);
            // 0.8*0.5=0.4。
            Assert.AreEqual(0.4f, IntelFusionRules.ActionableIntel(0.8f, 0.5f), Eps);
            // 0.7>=0.6 で信頼可、0.5<0.6 で不可。
            Assert.IsTrue(IntelFusionRules.IsPictureReliable(0.7f, 0.6f));
            Assert.IsFalse(IntelFusionRules.IsPictureReliable(0.5f, 0.6f));
        }

        [Test]
        public void Story_MultiSourceFusion_BeatsSingleStaleSource()
        {
            // 単一の古い欺瞞混じりソース：確信0.6・古さ20・敵欺瞞0.6。
            float single = IntelFusionRules.FusedConfidence(new[] { 0.6f });
            single *= IntelFusionRules.TimelinessFactor(20f);            // 1/(1+0.1*20)=1/3
            single = IntelFusionRules.DeceptionFilter(single, 0.6f);     // *(1-0.5*0.6)=*0.7

            // 偵察・傍受・哨戒の新鮮な3ソースを統合：各0.5・古さ2・欺瞞0.6・一致度0.8。
            float fused = IntelFusionRules.FusedConfidence(new[] { 0.5f, 0.5f, 0.5f }); // 1-0.125=0.875
            fused += IntelFusionRules.CorroborationBonus(3, 0.8f);                       // 0.3*(2/3)*0.8=0.16
            fused = Mathf.Clamp01(fused);
            fused *= IntelFusionRules.TimelinessFactor(2f);                              // 1/(1+0.2)=0.833...
            fused = IntelFusionRules.DeceptionFilter(fused, 0.6f);                       // *0.7

            // 複数の新鮮なソースを突き合わせた方が、敵の全体像をよりよく描ける。
            Assert.Greater(fused, single);
            Assert.IsTrue(IntelFusionRules.IsPictureReliable(fused, 0.4f));
            Assert.IsFalse(IntelFusionRules.IsPictureReliable(single, 0.4f));
        }
    }
}
