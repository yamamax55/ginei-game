using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 自生的秩序の脆弱性（ハイエク型・HAYK-6 #1556）の純ロジック検証。育成の遅い漸成・介入の速い侵食・
    /// 市場効率・知識問題・根付いた秩序の頑健性・回復の非対称・崩壊判定を既定Paramsの具体値で固定する。
    /// </summary>
    public class SpontaneousOrderRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>秩序は自由×分散知識から遅く育つ＝既定育成0.02で0.5→0.52。</summary>
        [Test]
        public void OrderFormationTick_自由と分散知識から遅く育つ()
        {
            float next = SpontaneousOrderRules.OrderFormationTick(0.5f, 1f, 1f, 1f);
            Assert.AreEqual(0.52f, next, Eps);

            // 分散知識が0なら育たない（両方が要る）。
            float none = SpontaneousOrderRules.OrderFormationTick(0.5f, 1f, 0f, 1f);
            Assert.AreEqual(0.5f, none, Eps);
        }

        /// <summary>強制介入は秩序を速く侵食＝既定侵食0.2で0.5→0.4（育成0.02の10倍速＝非対称）。</summary>
        [Test]
        public void InterventionErosion_介入は秩序を速く侵食する()
        {
            float eroded = SpontaneousOrderRules.InterventionErosion(0.5f, 1f);
            Assert.AreEqual(0.4f, eroded, Eps);

            // 介入0なら不変。
            float intact = SpontaneousOrderRules.InterventionErosion(0.5f, 0f);
            Assert.AreEqual(0.5f, intact, Eps);

            // 1tickの侵食(0.1)≫1tickの育成(0.02)＝壊すのは一瞬の非対称を確認。
            float lost = 0.5f - eroded;          // 0.1
            float gained = SpontaneousOrderRules.OrderFormationTick(0.5f, 1f, 1f, 1f) - 0.5f; // 0.02
            Assert.Greater(lost, gained * 4f);
        }

        /// <summary>市場効率は秩序に比例＝下駄0.3〜1.0。秩序0.5で0.65・秩序1で満点。</summary>
        [Test]
        public void MarketEfficiency_秩序が高いほど市場効率が高い()
        {
            Assert.AreEqual(0.65f, SpontaneousOrderRules.MarketEfficiency(0.5f), Eps);
            Assert.AreEqual(1f, SpontaneousOrderRules.MarketEfficiency(1f), Eps);
            Assert.AreEqual(0.3f, SpontaneousOrderRules.MarketEfficiency(0f), Eps);
        }

        /// <summary>中央計画が増えるほど分散知識が活かされない（知識問題）＝中央計画1で活用0。</summary>
        [Test]
        public void KnowledgeUtilization_中央計画が分散知識を殺す()
        {
            Assert.AreEqual(0.4f, SpontaneousOrderRules.KnowledgeUtilization(0.8f, 0.5f), Eps);
            Assert.AreEqual(0f, SpontaneousOrderRules.KnowledgeUtilization(0.8f, 1f), Eps);
            Assert.AreEqual(0.8f, SpontaneousOrderRules.KnowledgeUtilization(0.8f, 0f), Eps);
        }

        /// <summary>長く育った秩序ほど介入に耐える＝秩序0.8×成熟1×頑健最大0.5で0.4。</summary>
        [Test]
        public void OrderResilience_根付いた秩序は壊れにくい()
        {
            Assert.AreEqual(0.4f, SpontaneousOrderRules.OrderResilience(0.8f, 1f), Eps);

            // 成熟していなければ耐性ゼロ（根付いていない秩序は脆い）。
            Assert.AreEqual(0f, SpontaneousOrderRules.OrderResilience(0.8f, 0f), Eps);
        }

        /// <summary>侵食された秩序の回復は遅い＝既定回復0.01で0.4→0.406（侵食0.1の桁違いに遅い非対称）。</summary>
        [Test]
        public void RecoveryAsymmetry_回復は育成より遅い()
        {
            float recovered = SpontaneousOrderRules.RecoveryAsymmetry(0.4f, 1f);
            Assert.AreEqual(0.406f, recovered, Eps);

            // 同じ秩序0.4から：1tickの回復(0.006)は1tickの侵食(0.2*1*0.4=0.08)よりはるかに小さい。
            float erosionLoss = 0.4f - SpontaneousOrderRules.InterventionErosion(0.4f, 1f); // 0.08
            float recoveryGain = recovered - 0.4f;                                          // 0.006
            Assert.Greater(erosionLoss, recoveryGain * 10f);
        }

        /// <summary>設計しすぎると創発が死ぬ＝介入の二乗。介入0.5で0.25・介入1で全死1。</summary>
        [Test]
        public void OverDesignPenalty_過剰設計は創発を殺す()
        {
            Assert.AreEqual(0.25f, SpontaneousOrderRules.OverDesignPenalty(0.5f), Eps);
            Assert.AreEqual(1f, SpontaneousOrderRules.OverDesignPenalty(1f), Eps);
            // 非線形＝軽い介入(0.2)の害は小さい(0.04)。
            Assert.AreEqual(0.04f, SpontaneousOrderRules.OverDesignPenalty(0.2f), Eps);
        }

        /// <summary>介入過剰で秩序は崩壊しつつある＝高介入で侵食>閾値かつ侵食>回復でtrue・介入0でfalse。</summary>
        [Test]
        public void IsOrderCollapsing_介入過剰で崩壊する()
        {
            // 秩序0.8・介入1.0：侵食0.16>閾値0.05 かつ 0.16>回復0.002 ＝崩壊中。
            Assert.IsTrue(SpontaneousOrderRules.IsOrderCollapsing(0.8f, 1f, 0.05f));

            // 介入0なら侵食0＝崩壊しない（自生的秩序は緩い介入には耐える）。
            Assert.IsFalse(SpontaneousOrderRules.IsOrderCollapsing(0.8f, 0f, 0.05f));
        }
    }
}
