using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// SovereigntyNormRules（TYW-5 #1428・三十年戦争／ウェストファリア体制）の純ロジック検証。
    /// 規範の成熟・干渉の正当性低下・内政不干渉・宗教口実の減衰・主権平等・規範違反のコスト・
    /// 体系の安定・ウェストファリア体制判定を既定 Params の具体値で固定する。
    /// </summary>
    public class SovereigntyNormRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>規範成熟：惨禍×条約で時間成熟する。trauma=1/treaty=1・norm=0・dt=1 で 0+0.1×(0.6+0.4)=0.1。</summary>
        [Test]
        public void NormMaturity_惨禍と条約で成熟する()
        {
            float m = SovereigntyNormRules.NormMaturity(0f, 1f, 1f, 1f);
            Assert.AreEqual(0.1f, m, Eps);

            // トラウマも条約もゼロなら成熟しない＝惨禍と制度の両輪がないと規範は生まれない
            float none = SovereigntyNormRules.NormMaturity(0.3f, 0f, 0f, 1f);
            Assert.AreEqual(0.3f, none, Eps);
        }

        /// <summary>内政干渉の正当性：規範が成熟するほど口実の正当性が下がる。pretext=0.8/norm=1 で 0.8×(1−0.9)=0.08。</summary>
        [Test]
        public void InterventionLegitimacy_規範が干渉の正当性を下げる()
        {
            // 規範ゼロなら口実がそのまま正当性
            Assert.AreEqual(0.8f, SovereigntyNormRules.InterventionLegitimacy(0f, 0.8f), Eps);
            // 規範満点なら(1−0.9)まで圧縮
            Assert.AreEqual(0.08f, SovereigntyNormRules.InterventionLegitimacy(1f, 0.8f), Eps);
            // 成熟が進むほど正当性は下がる（単調減少）
            float low = SovereigntyNormRules.InterventionLegitimacy(0.3f, 0.8f);
            float high = SovereigntyNormRules.InterventionLegitimacy(0.9f, 0.8f);
            Assert.Less(high, low);
        }

        /// <summary>内政不干渉の原則：規範に正比例。</summary>
        [Test]
        public void NonInterferenceNorm_規範に比例する()
        {
            Assert.AreEqual(0f, SovereigntyNormRules.NonInterferenceNorm(0f), Eps);
            Assert.AreEqual(0.7f, SovereigntyNormRules.NonInterferenceNorm(0.7f), Eps);
            Assert.AreEqual(1f, SovereigntyNormRules.NonInterferenceNorm(1.5f), Eps); // クランプ
        }

        /// <summary>宗教口実の減衰：規範が宗教口実を蝕む。just=1/norm=0.75 で 1×(1−0.75)=0.25。規範満点で0。</summary>
        [Test]
        public void ReligiousPretextDecay_規範が宗教口実を蝕む()
        {
            Assert.AreEqual(0.25f, SovereigntyNormRules.ReligiousPretextDecay(0.75f, 1f), Eps);
            // cuius regio＝規範満点なら宗教口実は完全無効
            Assert.AreEqual(0f, SovereigntyNormRules.ReligiousPretextDecay(1f, 1f), Eps);
            // 規範ゼロなら宗教正当化はそのまま残る
            Assert.AreEqual(0.6f, SovereigntyNormRules.ReligiousPretextDecay(0f, 0.6f), Eps);
        }

        /// <summary>主権平等：規範上は平等でも力の差が建前を侵食する。norm=1/asym=1 で 1×(1−0.5)=0.5。</summary>
        [Test]
        public void SovereignEquality_力の差が建前を侵食する()
        {
            // 力が対称なら規範どおりの平等
            Assert.AreEqual(0.8f, SovereigntyNormRules.SovereignEquality(0.8f, 0f), Eps);
            // 力が最大非対称なら半分まで侵食（大国は規範を破りうる）
            Assert.AreEqual(0.5f, SovereigntyNormRules.SovereignEquality(1f, 1f), Eps);
        }

        /// <summary>規範違反のコスト：成熟した規範ほど破ると高くつく。violation=0.5/norm=0.8 で 0.5×0.8×1.0=0.4。</summary>
        [Test]
        public void NormViolationCost_確立した規範を破ると咎められる()
        {
            Assert.AreEqual(0.4f, SovereigntyNormRules.NormViolationCost(0.8f, 0.5f), Eps);
            // 規範未成熟なら破っても咎められずコスト0
            Assert.AreEqual(0f, SovereigntyNormRules.NormViolationCost(0f, 1f), Eps);
        }

        /// <summary>国際秩序の安定：規範×勢力均衡の両輪。どちらか欠ければ揺らぐ。</summary>
        [Test]
        public void SystemStability_規範と均衡の両輪で安定する()
        {
            Assert.AreEqual(0.56f, SovereigntyNormRules.SystemStability(0.7f, 0.8f), Eps);
            // 勢力均衡が崩れれば規範だけでは安定しない
            Assert.AreEqual(0f, SovereigntyNormRules.SystemStability(1f, 0f), Eps);
        }

        /// <summary>ウェストファリア体制判定：規範が既定閾値0.7以上で確立。</summary>
        [Test]
        public void IsWestphalianOrder_閾値で体制確立を判定する()
        {
            Assert.IsFalse(SovereigntyNormRules.IsWestphalianOrder(0.69f));
            Assert.IsTrue(SovereigntyNormRules.IsWestphalianOrder(0.7f));
            Assert.IsTrue(SovereigntyNormRules.IsWestphalianOrder(0.95f));
        }
    }
}
