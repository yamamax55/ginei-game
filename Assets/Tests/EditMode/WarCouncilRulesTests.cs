using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>作戦会議（軍議＝幕僚合議の質と意思決定）の純ロジックテスト。意見の多様性・議論・集団思考・異論・決断・機会損失を担保。</summary>
    public class WarCouncilRulesTests
    {
        private const float Tol = 1e-4f;
        private const float PowTol = 1e-3f;

        /// <summary>意見の多様性＝幕僚数×経歴の多様性で意見の幅が広がる／経歴が一様なら幅は出ない。</summary>
        [Test]
        public void OpinionDiversity_ScalesWithStaffAndVariety()
        {
            // 4×0.12×1.0 = 0.48
            Assert.AreEqual(0.48f, WarCouncilRules.OpinionDiversity(4, 1f), Tol);
            // 多様性ゼロ（経歴が一様）なら頭数だけ多くても幅0
            Assert.AreEqual(0f, WarCouncilRules.OpinionDiversity(5, 0f), Tol);
        }

        /// <summary>議論の質＝意見の幅×幕僚の有能さ（有能なら磨かれ、無能だと底上げが効かない）。</summary>
        [Test]
        public void DeliberationQuality_RaisedByCompetence()
        {
            // 有能max: gain=Lerp(0.5,1,1)=1 → 0.48
            Assert.AreEqual(0.48f, WarCouncilRules.DeliberationQuality(0.48f, 1f), Tol);
            // 無能: gain=Lerp(0.5,1,0)=0.5 → 0.6×0.5=0.3
            Assert.AreEqual(0.3f, WarCouncilRules.DeliberationQuality(0.6f, 0f), Tol);
            // 中庸: gain=0.75 → 0.6×0.75=0.45
            Assert.AreEqual(0.45f, WarCouncilRules.DeliberationQuality(0.6f, 0.5f), Tol);
        }

        /// <summary>集団思考＝主将の威圧×同調圧力（両方高いと議論が腐る・片方ゼロで崩れる）。</summary>
        [Test]
        public void Groupthink_RisesWithDominanceAndConformity()
        {
            // 1.0×1.0×0.6 = 0.6（最大弊害）
            Assert.AreEqual(0.6f, WarCouncilRules.Groupthink(1f, 1f), Tol);
            // 0.5×0.5×0.6 = 0.15
            Assert.AreEqual(0.15f, WarCouncilRules.Groupthink(0.5f, 0.5f), Tol);
            // 威圧ゼロなら集団思考なし
            Assert.AreEqual(0f, WarCouncilRules.Groupthink(0f, 1f), Tol);
        }

        /// <summary>異論の価値＝意見の幅×(1-集団思考)（集団思考が支配すれば異論は死ぬ）。</summary>
        [Test]
        public void DissentValue_KilledByGroupthink()
        {
            // 0.8×(1-0.6) = 0.32
            Assert.AreEqual(0.32f, WarCouncilRules.DissentValue(0.8f, 0.6f), Tol);
            // 集団思考なしなら意見の幅がそのまま異論として活きる
            Assert.AreEqual(0.8f, WarCouncilRules.DissentValue(0.8f, 0f), Tol);
        }

        /// <summary>作戦の質＝議論×異論の相乗（幾何平均）を主将の慧眼で底上げ。</summary>
        [Test]
        public void PlanQuality_GeometricCoreLiftedByInsight()
        {
            // core=sqrt(0.64×0.64)=0.64, 0.64×0.6 + 0×0.4 = 0.384
            Assert.AreEqual(0.384f, WarCouncilRules.PlanQuality(0.64f, 0.64f, 0f), PowTol);
            // 議論・異論ゼロでも慧眼が w ぶん拾う: 0×0.6 + 1×0.4 = 0.4
            Assert.AreEqual(0.4f, WarCouncilRules.PlanQuality(0f, 0f, 1f), PowTol);
            // すべて満点で1.0
            Assert.AreEqual(1f, WarCouncilRules.PlanQuality(1f, 1f, 1f), PowTol);
        }

        /// <summary>決定の速さ＝幕僚が多いほど遅く、主将の決断力で取り戻す。</summary>
        [Test]
        public void DecisionSpeed_SlowedByStaffSpedByDecisiveness()
        {
            // base=1-5×0.08=0.6, 決断力0 → 0.6
            Assert.AreEqual(0.6f, WarCouncilRules.DecisionSpeed(5, 0f), Tol);
            // 決断力max → Lerp(0.6,1,1)=1.0（即断で会議を裁く）
            Assert.AreEqual(1f, WarCouncilRules.DecisionSpeed(5, 1f), Tol);
            // 幕僚ゼロなら遅延なし
            Assert.AreEqual(1f, WarCouncilRules.DecisionSpeed(0, 0f), Tol);
        }

        /// <summary>機会損失＝決定の遅さ×状況の切迫（切迫していなければ遅くとも損失は小さい）。</summary>
        [Test]
        public void OpportunityCost_RisesWithSlownessAndUrgency()
        {
            // slow=1-0.6=0.4, urgency=Lerp(0,1,1)=1 → 0.4
            Assert.AreEqual(0.4f, WarCouncilRules.OpportunityCost(0.6f, 1f), Tol);
            // 切迫ゼロなら遅くとも損失なし
            Assert.AreEqual(0f, WarCouncilRules.OpportunityCost(0.6f, 0f), Tol);
            // 即決(speed=1)なら損失なし
            Assert.AreEqual(0f, WarCouncilRules.OpportunityCost(1f, 1f), Tol);
        }

        /// <summary>妥当判定＝作戦の質が既定しきい値(0.5)以上で真。</summary>
        [Test]
        public void IsPlanSound_UsesThreshold()
        {
            Assert.IsFalse(WarCouncilRules.IsPlanSound(0.384f));
            Assert.IsTrue(WarCouncilRules.IsPlanSound(0.6f));
            // 明示しきい値版
            Assert.IsTrue(WarCouncilRules.IsPlanSound(0.4f, 0.3f));
            Assert.IsFalse(WarCouncilRules.IsPlanSound(0.4f, 0.5f));
        }

        /// <summary>入力は全てクランプされる（負の幕僚数・範囲外の比率も安全）。</summary>
        [Test]
        public void Inputs_AreClamped()
        {
            // 負の幕僚数→0扱い、過剰な多様性→1扱い
            Assert.AreEqual(0f, WarCouncilRules.OpinionDiversity(-3, 2f), Tol);
            // 範囲外の集団思考→clamp（1.5→1扱い、異論は死ぬ）
            Assert.AreEqual(0f, WarCouncilRules.DissentValue(0.8f, 1.5f), Tol);
        }

        /// <summary>
        /// 物語テスト：ヤン提督の幕僚会議。多彩な経歴の有能な幕僚が忌憚なく進言し（威圧せず）、明察な主将が裁く＝
        /// 良作戦が速やかに固まり、急場でも機会を逃さない。対照に、無能な主将が威圧し同調圧力で固める軍議は集団思考に陥り、
        /// 異論が死に、作戦は妥当性を欠き、悠長な合議が好機を逃す。
        /// </summary>
        [Test]
        public void Narrative_OpenCouncilBeatsAuthoritarianGroupthink()
        {
            // ── 良い軍議（ヤン型）：多彩(4名・多様性高)・有能・威圧せず・明察・決断力あり ──
            float goodDiversity = WarCouncilRules.OpinionDiversity(4, 0.9f);          // 0.432
            float goodDelib = WarCouncilRules.DeliberationQuality(goodDiversity, 0.9f);
            float goodGroupthink = WarCouncilRules.Groupthink(0.2f, 0.2f);            // ほぼ0
            float goodDissent = WarCouncilRules.DissentValue(goodDiversity, goodGroupthink);
            float goodPlan = WarCouncilRules.PlanQuality(goodDelib, goodDissent, 0.9f);
            float goodSpeed = WarCouncilRules.DecisionSpeed(4, 0.9f);
            float goodLoss = WarCouncilRules.OpportunityCost(goodSpeed, 1f);

            // ── 悪い軍議（独断型）：同じ幕僚数だが威圧・同調圧力強・無能な主将 ──
            float badDelib = WarCouncilRules.DeliberationQuality(goodDiversity, 0.3f);
            float badGroupthink = WarCouncilRules.Groupthink(1f, 1f);                 // 0.6
            float badDissent = WarCouncilRules.DissentValue(goodDiversity, badGroupthink);
            float badPlan = WarCouncilRules.PlanQuality(badDelib, badDissent, 0.2f);
            float badSpeed = WarCouncilRules.DecisionSpeed(4, 0.1f);
            float badLoss = WarCouncilRules.OpportunityCost(badSpeed, 1f);

            // 開かれた軍議は集団思考が小さく異論が活きる
            Assert.Less(goodGroupthink, badGroupthink);
            Assert.Greater(goodDissent, badDissent);
            // 作戦の質が上回り、妥当である
            Assert.Greater(goodPlan, badPlan);
            Assert.IsTrue(WarCouncilRules.IsPlanSound(goodPlan));
            // 決断力ある主将は速く裁き、急場の機会損失が小さい
            Assert.Greater(goodSpeed, badSpeed);
            Assert.Less(goodLoss, badLoss);
        }
    }
}
