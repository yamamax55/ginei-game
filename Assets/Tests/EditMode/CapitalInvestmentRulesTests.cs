using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>CapitalInvestmentRules（#1025 資本投下・投資判断）の純ロジックを既定Paramsで固定する。</summary>
    public class CapitalInvestmentRulesTests
    {
        private CapitalInvestmentParams P => CapitalInvestmentParams.Default;

        /// <summary>投資利回り＝利益増分／投資額（コスト0以下は0）。</summary>
        [Test]
        public void ExpectedReturn_ReturnsProfitOverCost()
        {
            // 100投じて30の利益増＝利回り0.3
            Assert.AreEqual(0.3f, CapitalInvestmentRules.ExpectedReturn(100f, 30f), 1e-5f);
            Assert.AreEqual(0f, CapitalInvestmentRules.ExpectedReturn(0f, 30f), 1e-5f);
        }

        /// <summary>ハードルレートを超える利回りなら投資（資本コストの壁）。</summary>
        [Test]
        public void InvestmentDecision_GoWhenAboveHurdle()
        {
            // ハードル0.1・リスク0＝要求0.1。利回り0.15>0.1で投資。
            Assert.IsTrue(CapitalInvestmentRules.InvestmentDecision(0.15f, 0.1f, 0f, P));
            // 利回り0.08<0.1で見送り。
            Assert.IsFalse(CapitalInvestmentRules.InvestmentDecision(0.08f, 0.1f, 0f, P));
        }

        /// <summary>リスクが高いほど高い利回りを要求＝同じ利回りでも投資が通らなくなる。</summary>
        [Test]
        public void InvestmentDecision_HighRiskRaisesHurdle()
        {
            // ハードル0.1・リスク1＝要求0.1+1*0.15=0.25。利回り0.2では足りない。
            Assert.AreEqual(0.25f, CapitalInvestmentRules.RequiredReturn(0.1f, 1f, P), 1e-5f);
            Assert.IsFalse(CapitalInvestmentRules.InvestmentDecision(0.2f, 0.1f, 1f, P));
            // 同じ利回り0.2でもリスク0なら要求0.1で通る。
            Assert.IsTrue(CapitalInvestmentRules.InvestmentDecision(0.2f, 0.1f, 0f, P));
        }

        /// <summary>回収期間＝投資額／年間CF（CF0以下は回収不能＝∞）。</summary>
        [Test]
        public void PaybackPeriod_YearsToRecover()
        {
            // 100を年20で回収＝5年。
            Assert.AreEqual(5f, CapitalInvestmentRules.PaybackPeriod(100f, 20f), 1e-5f);
            Assert.IsTrue(float.IsPositiveInfinity(CapitalInvestmentRules.PaybackPeriod(100f, 0f)));
        }

        /// <summary>内部留保で足りれば自前・不足なら負債コストvs希薄化の安い方。</summary>
        [Test]
        public void FinancingChoice_PicksCheapestSource()
        {
            // 内部留保で足りる＝自前。
            Assert.AreEqual(FinancingSource.内部留保,
                CapitalInvestmentRules.FinancingChoice(100f, 80f, 0.05f, 0.2f));
            // 不足・金利0.05<希薄化0.2＝融資。
            Assert.AreEqual(FinancingSource.銀行融資,
                CapitalInvestmentRules.FinancingChoice(50f, 100f, 0.05f, 0.2f));
            // 不足・金利0.3>希薄化0.1＝増資。
            Assert.AreEqual(FinancingSource.増資,
                CapitalInvestmentRules.FinancingChoice(50f, 100f, 0.3f, 0.1f));
        }

        /// <summary>稼働率が高ければ過剰投資リスク0、低いと計画拡大に比例して遊休リスク上昇。</summary>
        [Test]
        public void OverInvestmentRisk_IdleWhenLowUtilization()
        {
            // 稼働率0.9>=0.8＝増設余地ありリスク0。
            Assert.AreEqual(0f, CapitalInvestmentRules.OverInvestmentRisk(0.9f, 1f, P), 1e-5f);
            // 稼働率0.4＝余裕(0.8)の半分が未稼働(idleShare=0.5)×拡大1.0＝0.5。
            Assert.AreEqual(0.5f, CapitalInvestmentRules.OverInvestmentRisk(0.4f, 1f, P), 1e-5f);
            // 稼働率0低い＋拡大大＝設備が遊ぶ＝上限1。
            Assert.AreEqual(1f, CapitalInvestmentRules.OverInvestmentRisk(0f, 2f, P), 1e-5f);
        }

        /// <summary>能力増設は建設ラグで遅れて稼働＝投資から能力への変換に時間がかかる。</summary>
        [Test]
        public void CapacityExpansionTick_LagsBehindInvestment()
        {
            // ラグ2年・投資100・dt1＝50ぶん増える。100→150。
            Assert.AreEqual(150f, CapitalInvestmentRules.CapacityExpansionTick(100f, 100f, 2f, 1f, P), 1e-4f);
            // dt0は据え置き。
            Assert.AreEqual(100f, CapitalInvestmentRules.CapacityExpansionTick(100f, 100f, 2f, 0f, P), 1e-4f);
            // ラグが下限(1年)未満でも下限で割る＝即時稼働しない（投資100→+100）。
            Assert.AreEqual(200f, CapitalInvestmentRules.CapacityExpansionTick(100f, 100f, 0.1f, 1f, P), 1e-4f);
        }
    }
}
