using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>謀反・クーデターの陰謀ロジックの検証（既定Params期待値固定）。</summary>
    public class ConspiracyRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void Recruitment_DefaultProduct()
        {
            // 0.8*0.5*0.5*0.9 = 0.18
            float r = ConspiracyRules.ConspiratorRecruitment(0.8f, 0.5f, 0.5f);
            Assert.AreEqual(0.18f, r, Eps);
        }

        [Test]
        public void Recruitment_WeakRegimeAndCharismaRequired()
        {
            // 政権が盤石（弱さ0）なら誰も集まらない
            Assert.AreEqual(0f, ConspiracyRules.ConspiratorRecruitment(1f, 1f, 0f), Eps);
            // 魅力ゼロの首謀者も輪を広げられない
            Assert.AreEqual(0f, ConspiracyRules.ConspiratorRecruitment(1f, 0f, 1f), Eps);
        }

        [Test]
        public void PlanSecrecy_DropsWithNumbers()
        {
            // 規律0.9 / (1+5) = 0.15
            Assert.AreEqual(0.15f, ConspiracyRules.PlanSecrecy(0.9f, 5f), Eps);
            // 共謀者0なら規律そのまま
            Assert.AreEqual(0.9f, ConspiracyRules.PlanSecrecy(0.9f, 0f), Eps);
            // 負の人数は0扱い
            Assert.AreEqual(0.9f, ConspiracyRules.PlanSecrecy(0.9f, -3f), Eps);
        }

        [Test]
        public void MilitaryBacking_RatioOfSupport()
        {
            // 0.6/(0.6+0.2)=0.75
            Assert.AreEqual(0.75f, ConspiracyRules.MilitaryBacking(0.6f, 0.2f), Eps);
            // 両者ゼロは中立0
            Assert.AreEqual(0f, ConspiracyRules.MilitaryBacking(0f, 0f), Eps);
        }

        [Test]
        public void InformantRisk_DefaultValue()
        {
            // exposure=1-1/10=0.9; 0.8*0.9*0.5*(1-0.1)=0.324
            float risk = ConspiracyRules.InformantRisk(9f, 0.5f, 0.1f);
            Assert.AreEqual(0.324f, risk, Eps);
            // 秘匿が完璧なら密告は出ない
            Assert.AreEqual(0f, ConspiracyRules.InformantRisk(9f, 1f, 1f), Eps);
        }

        [Test]
        public void TimingReadiness_NeedsBoth()
        {
            // 0.8*0.5=0.4
            Assert.AreEqual(0.4f, ConspiracyRules.TimingReadiness(0.8f, 0.5f), Eps);
            // 準備ゼロなら機は熟さない
            Assert.AreEqual(0f, ConspiracyRules.TimingReadiness(1f, 0f), Eps);
        }

        [Test]
        public void CoupSuccess_BackingTimesTimingMinusRisk()
        {
            // 0.75*0.4 - 0.5*0.324 = 0.3 - 0.162 = 0.138
            float s = ConspiracyRules.CoupSuccess(0.75f, 0.4f, 0.324f);
            Assert.AreEqual(0.138f, s, Eps);
            // 密告が重ければ成功率は0へ沈む（負はclamp）
            Assert.AreEqual(0f, ConspiracyRules.CoupSuccess(0.1f, 0.1f, 1f), Eps);
        }

        [Test]
        public void PurgeAftermath_FailureTimesVengefulness()
        {
            // (1-0.138)*0.8*1.0 = 0.6896
            float purge = ConspiracyRules.PurgeAftermath(0.138f, 0.8f);
            Assert.AreEqual(0.6896f, purge, Eps);
            // 成功すれば粛清されない
            Assert.AreEqual(0f, ConspiracyRules.PurgeAftermath(1f, 1f), Eps);
        }

        [Test]
        public void IsCoupViable_Threshold()
        {
            Assert.IsTrue(ConspiracyRules.IsCoupViable(0.5f));   // 既定閾値0.5ちょうどは値する
            Assert.IsFalse(ConspiracyRules.IsCoupViable(0.49f));
            Assert.IsTrue(ConspiracyRules.IsCoupViable(0.3f, 0.2f));
        }

        [Test]
        public void Narrative_LippstadtStyleCoup()
        {
            // 物語：高級将校の不満が募り、魅力ある盟主が弱った政権に対し糾合する。
            float recruit = ConspiracyRules.ConspiratorRecruitment(0.9f, 0.8f, 0.7f);
            Assert.Greater(recruit, 0.4f, "不満・魅力・政権の弱さが揃えば相応に人が集まる");

            // しかし大人数の謀議は秘匿が崩れる
            float secrecyTight = ConspiracyRules.PlanSecrecy(0.9f, 2f);    // 少数精鋭
            float secrecyLoose = ConspiracyRules.PlanSecrecy(0.9f, 30f);   // 大規模
            Assert.Greater(secrecyTight, secrecyLoose, "人数が増えるほど計画は漏れる");

            // 軍を引き込めれば取り付けは高い
            float backing = ConspiracyRules.MilitaryBacking(0.8f, 0.2f);
            Assert.Greater(backing, 0.7f);

            // 大規模で秘匿が緩いと密告リスクが跳ね上がる
            float riskLoose = ConspiracyRules.InformantRisk(30f, 0.7f, secrecyLoose);
            float riskTight = ConspiracyRules.InformantRisk(2f, 0.7f, secrecyTight);
            Assert.Greater(riskLoose, riskTight, "大規模・低秘匿ほど内通が出る");

            // 機が熟したところで決行
            float timing = ConspiracyRules.TimingReadiness(0.7f, 0.8f);
            float successLoose = ConspiracyRules.CoupSuccess(backing, timing, riskLoose);
            float successTight = ConspiracyRules.CoupSuccess(backing, timing, riskTight);
            Assert.Greater(successTight, successLoose, "密告を抑えた決起ほど成功する");

            // 救国軍事会議型＝密告で先手を打たれ失敗、苛烈な体制が粛清する
            float doomed = ConspiracyRules.CoupSuccess(0.5f, 0.3f, 0.9f);
            Assert.IsFalse(ConspiracyRules.IsCoupViable(doomed), "成算なき決起は値しない");
            float purge = ConspiracyRules.PurgeAftermath(doomed, 0.9f);
            Assert.Greater(purge, 0.5f, "失敗したクーデターは根こそぎ粛清される");
        }
    }
}
