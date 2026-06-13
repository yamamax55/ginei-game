using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>資質本位の後継指名（HDRN-2 #1804・ローマ養子皇帝型）の純ロジックを担保する。</summary>
    public class HeirDesignationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>候補の資質スコア＝能力0.5/忠誠0.25/活力0.25の加重和（合計1.0で正規化）。</summary>
        [Test]
        public void CandidateScore_能力忠誠活力の加重和()
        {
            Assert.AreEqual(1f, HeirDesignationRules.CandidateScore(1f, 1f, 1f), Eps);
            Assert.AreEqual(0.5f, HeirDesignationRules.CandidateScore(1f, 0f, 0f), Eps); // 能力のみ
            Assert.AreEqual(0.5f, HeirDesignationRules.CandidateScore(0f, 1f, 1f), Eps); // 忠誠+活力
            Assert.AreEqual(0.6f, HeirDesignationRules.CandidateScore(0.8f, 0.4f, 0.4f), Eps);
        }

        /// <summary>2候補は資質の高い方を選ぶ（-1=A・0=同等・1=B）＝法順位でなくスコアで決める。</summary>
        [Test]
        public void BestCandidate_資質の高い方を選ぶ()
        {
            Assert.AreEqual(-1, HeirDesignationRules.BestCandidate(0.8f, 0.5f));
            Assert.AreEqual(1, HeirDesignationRules.BestCandidate(0.5f, 0.8f));
            Assert.AreEqual(0, HeirDesignationRules.BestCandidate(0.5f, 0.5f));
        }

        /// <summary>資質が法順位を覆す度合い＝法順位が劣後で資質が高いほど大きい。</summary>
        [Test]
        public void MeritOverLineage_資質が法順位を覆す()
        {
            // 末位(r=1)で資質0.9：excess=0.9、0.9*0.6 + 0.9*1*0.6 = 1.08 → クランプ1.0
            Assert.AreEqual(1f, HeirDesignationRules.MeritOverLineage(0.9f, 1f), Eps);
            // 筆頭(r=0)では覆す必要なし＝0（資質0.9でも正統スコア1に届かずexcess0）
            Assert.AreEqual(0f, HeirDesignationRules.MeritOverLineage(0.9f, 0f), Eps);
            // 中位(r=0.5)・資質0.6：excess=0.1、0.1*0.6 + 0.6*0.5*0.6 = 0.06+0.18 = 0.24
            Assert.AreEqual(0.24f, HeirDesignationRules.MeritOverLineage(0.6f, 0.5f), Eps);
        }

        /// <summary>指名養子の成立度＝資質が血統距離のペナルティ(×0.7)を補う。</summary>
        [Test]
        public void AdoptionViability_資質が血の遠さを補う()
        {
            Assert.AreEqual(0.9f, HeirDesignationRules.AdoptionViability(0.9f, 0f), Eps);   // 血が近い
            Assert.AreEqual(0.2f, HeirDesignationRules.AdoptionViability(0.9f, 1f), Eps);   // 0.9-0.7
            Assert.AreEqual(0.15f, HeirDesignationRules.AdoptionViability(0.5f, 0.5f), Eps);// 0.5-0.35
        }

        /// <summary>正統性論争＝指名が法定後継を飛び越すほど負（飛び越さなければ0）。</summary>
        [Test]
        public void LegitimacyContest_法順位を飛び越すと論争()
        {
            Assert.AreEqual(-0.6f, HeirDesignationRules.LegitimacyContest(0.8f, 0.2f), Eps); // 劣後を指名
            Assert.AreEqual(0f, HeirDesignationRules.LegitimacyContest(0.2f, 0.8f), Eps);   // 法定上位を指名
            Assert.AreEqual(0f, HeirDesignationRules.LegitimacyContest(0.5f, 0.5f), Eps);
        }

        /// <summary>継承危機リスク＝正統性論争の大きさ×対抗者の強さ。</summary>
        [Test]
        public void SuccessionCrisisRisk_論争と対抗者の強さで危機()
        {
            Assert.AreEqual(0.3f, HeirDesignationRules.SuccessionCrisisRisk(-0.6f, 0.5f), Eps);
            Assert.AreEqual(0f, HeirDesignationRules.SuccessionCrisisRisk(0f, 1f), Eps); // 論争なし
        }

        /// <summary>指名の安定度＝後継の資質×宮廷の合意（どちらか欠ければ不安定）。</summary>
        [Test]
        public void DesignationStability_資質と宮廷合意の積()
        {
            Assert.AreEqual(0.4f, HeirDesignationRules.DesignationStability(0.8f, 0.5f), Eps);
            Assert.AreEqual(0f, HeirDesignationRules.DesignationStability(1f, 0f), Eps); // 宮廷の合意なし
        }

        /// <summary>有能さと正統性のトレードオフ＝資質の利得−正統性の損失。</summary>
        [Test]
        public void MeritVsStabilityTradeoff_利得と代償の差()
        {
            Assert.AreEqual(0.5f, HeirDesignationRules.MeritVsStabilityTradeoff(0.8f, 0.3f), Eps);
            Assert.AreEqual(-0.5f, HeirDesignationRules.MeritVsStabilityTradeoff(0.3f, 0.8f), Eps);
        }

        /// <summary>資質本位の継承か＝資質の重みが閾値以上（ローマ養子皇帝＝高/絶対王政＝低）。</summary>
        [Test]
        public void IsMeritBasedSuccession_閾値で判定()
        {
            Assert.IsTrue(HeirDesignationRules.IsMeritBasedSuccession(0.6f, 0.5f));
            Assert.IsFalse(HeirDesignationRules.IsMeritBasedSuccession(0.4f, 0.5f));
            Assert.IsTrue(HeirDesignationRules.IsMeritBasedSuccession(0.5f, 0.5f)); // 境界は以上
        }

        /// <summary>物語：有能な血統外候補を養子に指名すると成立はするが、法順位を飛び越して継承危機を招く。</summary>
        [Test]
        public void Story_有能な養子は成立するが正統性危機を招く()
        {
            float merit = HeirDesignationRules.CandidateScore(0.9f, 0.8f, 0.8f); // 0.85
            // 資質が高ければ血の遠さ(0.9)を補って養子継承が成立する
            float viable = HeirDesignationRules.AdoptionViability(merit, 0.9f); // 0.22
            Assert.Greater(viable, 0f);
            // しかし末位(0.9)を指名して法定筆頭(0.1)を飛び越すと正統性論争が大きく、対抗者が強ければ継承危機
            float contest = HeirDesignationRules.LegitimacyContest(0.9f, 0.1f); // -0.8
            float crisis = HeirDesignationRules.SuccessionCrisisRisk(contest, 0.7f); // 0.56
            Assert.Greater(crisis, 0.5f);
            // 有能さの利得と正統性の損失は拮抗＝資質本位は割に合うとは限らない
            float tradeoff = HeirDesignationRules.MeritVsStabilityTradeoff(merit, Mathf.Abs(contest));
            Assert.Greater(tradeoff, 0f);
            Assert.Less(tradeoff, 0.2f);
        }
    }
}
