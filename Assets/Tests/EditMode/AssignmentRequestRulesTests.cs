using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 配属希望の内示（TKO-5・<see cref="AssignmentRequestRules"/>・#2482）を固定する：高武勲/好人脈で内示、
    /// 中庸で代替（前線→後方）、低評価で棄却、在学中（配属不可）は無条件棄却、後方希望は代替帯でも内示。
    /// </summary>
    public class AssignmentRequestRulesTests
    {
        private static AssignmentRequestRules.AssignmentParams P => AssignmentRequestRules.AssignmentParams.Default;

        [Test]
        public void HighMeritAndFavor_GrantsPreference()
        {
            var d = AssignmentRequestRules.Adjudicate(PostingPreference.前線, deployable: true,
                merit01: 1f, favor: 1f, standing01: 1f, P);
            Assert.AreEqual(AssignmentVerdict.内示, d.verdict);
            Assert.AreEqual(PostingPreference.前線, d.granted);
        }

        [Test]
        public void MidScore_FrontlineWant_OffersAlternativeRear()
        {
            // merit0.4*0.5 + favor0(→0.5)*0.3 + standing0.4*0.2 = 0.2+0.15+0.08 = 0.43 ∈ [0.35,0.6)
            var d = AssignmentRequestRules.Adjudicate(PostingPreference.前線, deployable: true,
                merit01: 0.4f, favor: 0f, standing01: 0.4f, P);
            Assert.AreEqual(AssignmentVerdict.代替, d.verdict);
            Assert.AreEqual(PostingPreference.後方, d.granted);
        }

        [Test]
        public void MidScore_RearWant_GrantedSincePreferenceEqualsAlternative()
        {
            var d = AssignmentRequestRules.Adjudicate(PostingPreference.後方, deployable: true,
                merit01: 0.4f, favor: 0f, standing01: 0.4f, P);
            Assert.AreEqual(AssignmentVerdict.内示, d.verdict, "後方希望は代替先と同じ＝内示扱い");
            Assert.AreEqual(PostingPreference.後方, d.granted);
        }

        [Test]
        public void LowScore_Rejected()
        {
            var d = AssignmentRequestRules.Adjudicate(PostingPreference.参謀, deployable: true,
                merit01: 0f, favor: -1f, standing01: 0f, P);
            Assert.AreEqual(AssignmentVerdict.棄却, d.verdict);
        }

        [Test]
        public void NotDeployable_RejectedRegardlessOfMerit()
        {
            var d = AssignmentRequestRules.Adjudicate(PostingPreference.前線, deployable: false,
                merit01: 1f, favor: 1f, standing01: 1f, P);
            Assert.AreEqual(AssignmentVerdict.棄却, d.verdict, "在学中など配属不可は無条件棄却");
        }

        [Test]
        public void Score_NormalizesWeights()
        {
            // 全入力1.0 → 正規化後 1.0、全0かつfavor-1 → 0
            Assert.AreEqual(1f, AssignmentRequestRules.Score(1f, 1f, 1f, P), 1e-4);
            Assert.AreEqual(0f, AssignmentRequestRules.Score(0f, -1f, 0f, P), 1e-4);
        }
    }
}
