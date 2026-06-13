using NUnit.Framework;
using Ginei;
using EP = Ginei.MeritEvaluationRules.EvaluationParams;
using AP = Ginei.CivilServiceRules.AppointmentParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 官僚制基盤（考課＝勤務評定／銓衡＝任用）を固定する。史実（唐の考課九等・律令の官位相当・
    /// 科挙〜蔭位の登用経路）を参考にした純ロジックの不変条件。宦官経路は採用しない（清廉度で表現）。
    /// </summary>
    public class BureaucracyFoundationTests
    {
        // ---- 考課（MeritEvaluationRules） ----

        [Test]
        public void MeritScore_HigherInputs_HigherScore_AndBounded()
        {
            var p = EP.Default;
            float low = MeritEvaluationRules.MeritScore(0.1f, 0.1f, 0, p);
            float high = MeritEvaluationRules.MeritScore(1f, 1f, 100, p);
            Assert.Greater(high, low);
            Assert.That(low, Is.InRange(0f, 1f));
            Assert.That(high, Is.InRange(0f, 1f));
        }

        [Test]
        public void RatingFromScore_MapsToNineGrades()
        {
            Assert.AreEqual(MeritRating.上上, MeritEvaluationRules.RatingFromScore(1f));
            Assert.AreEqual(MeritRating.上上, MeritEvaluationRules.RatingFromScore(0.99f));
            Assert.AreEqual(MeritRating.中中, MeritEvaluationRules.RatingFromScore(0.5f));
            Assert.AreEqual(MeritRating.下下, MeritEvaluationRules.RatingFromScore(0f));
        }

        [Test]
        public void Score_And_Rank_AreInverseOrders()
        {
            Assert.AreEqual(9, MeritEvaluationRules.Score(MeritRating.上上));
            Assert.AreEqual(1, MeritEvaluationRules.Score(MeritRating.下下));
            Assert.AreEqual(0, MeritEvaluationRules.Rank(MeritRating.上上));
            Assert.AreEqual(8, MeritEvaluationRules.Rank(MeritRating.下下));
            Assert.IsTrue(MeritEvaluationRules.IsTop(MeritRating.上下));
            Assert.IsFalse(MeritEvaluationRules.IsTop(MeritRating.中上));
            Assert.IsTrue(MeritEvaluationRules.IsPoor(MeritRating.下上));
            Assert.IsFalse(MeritEvaluationRules.IsPoor(MeritRating.中下));
        }

        [Test]
        public void Record_AccumulatesAndTracksStreaks()
        {
            var m = new OfficialMerit(1);
            MeritEvaluationRules.Record(m, MeritRating.上上);
            MeritEvaluationRules.Record(m, MeritRating.上中);
            Assert.AreEqual(2, m.evaluations);
            Assert.AreEqual(9 + 8, m.cumulativeScore);
            Assert.AreEqual(2, m.consecutiveTop);
            Assert.AreEqual(0, m.consecutivePoor);
            // 下系で好評リセット
            MeritEvaluationRules.Record(m, MeritRating.下下);
            Assert.AreEqual(0, m.consecutiveTop);
            Assert.AreEqual(1, m.consecutivePoor);
            // 中系で両方途切れる
            MeritEvaluationRules.Record(m, MeritRating.中中);
            Assert.AreEqual(0, m.consecutiveTop);
            Assert.AreEqual(0, m.consecutivePoor);
        }

        [Test]
        public void Promotable_And_Demote_NeedConsecutiveStreaks()
        {
            var p = EP.Default; // 3連続
            var m = new OfficialMerit(1);
            for (int i = 0; i < 3; i++) MeritEvaluationRules.Record(m, MeritRating.上上);
            Assert.IsTrue(MeritEvaluationRules.IsPromotable(m, p));
            Assert.IsFalse(MeritEvaluationRules.ShouldDemote(m, p));

            var m2 = new OfficialMerit(2);
            MeritEvaluationRules.Record(m2, MeritRating.下下);
            MeritEvaluationRules.Record(m2, MeritRating.下中);
            Assert.IsFalse(MeritEvaluationRules.ShouldDemote(m2, p)); // 2連続では足りない
            MeritEvaluationRules.Record(m2, MeritRating.下上);
            Assert.IsTrue(MeritEvaluationRules.ShouldDemote(m2, p));
        }

        [Test]
        public void PromotionTierDelta_OnlyExtremesMove()
        {
            Assert.AreEqual(1, MeritEvaluationRules.PromotionTierDelta(MeritRating.上上));
            Assert.AreEqual(-1, MeritEvaluationRules.PromotionTierDelta(MeritRating.下下));
            Assert.AreEqual(0, MeritEvaluationRules.PromotionTierDelta(MeritRating.中中));
            Assert.AreEqual(0, MeritEvaluationRules.PromotionTierDelta(MeritRating.上中));
        }

        [Test]
        public void StipendFactor_GoodRaises_PoorCuts()
        {
            Assert.AreEqual(1.2f, MeritEvaluationRules.StipendFactor(MeritRating.上上), 1e-4f);
            Assert.AreEqual(1.0f, MeritEvaluationRules.StipendFactor(MeritRating.中中), 1e-4f);
            Assert.AreEqual(0.8f, MeritEvaluationRules.StipendFactor(MeritRating.下下), 1e-4f);
        }

        [Test]
        public void EvaluateAndRecord_UsesStoredIntegrity()
        {
            var p = EP.Default;
            var clean = new OfficialMerit(1, integrity: 1f);
            var corrupt = new OfficialMerit(2, integrity: 0f);
            // 同じ能・勤続でも清廉な官のほうが良い考第（徳の重みぶん）
            MeritRating rc = MeritEvaluationRules.EvaluateAndRecord(clean, competence: 0.6f, tenureYears: 10, p);
            MeritRating rk = MeritEvaluationRules.EvaluateAndRecord(corrupt, competence: 0.6f, tenureYears: 10, p);
            Assert.LessOrEqual(MeritEvaluationRules.Rank(rc), MeritEvaluationRules.Rank(rk));
            Assert.AreEqual(1, clean.evaluations);
        }

        // ---- 銓衡・任用（CivilServiceRules） ----

        [Test]
        public void MeritWeight_ExamMostMeritocratic_OnReverseRoute()
        {
            Assert.Greater(CivilServiceRules.MeritWeight(CivilEntryRoute.科挙),
                           CivilServiceRules.MeritWeight(CivilEntryRoute.辟召));
            Assert.Greater(CivilServiceRules.MeritWeight(CivilEntryRoute.辟召),
                           CivilServiceRules.MeritWeight(CivilEntryRoute.蔭位));
        }

        [Test]
        public void Fitness_RankOfficeCorrespondence()
        {
            var prm = AP.Default; // 許容±1
            Assert.AreEqual(AppointmentFit.適任, CivilServiceRules.Fitness(7, 7, prm));
            Assert.AreEqual(AppointmentFit.適任, CivilServiceRules.Fitness(8, 7, prm));
            Assert.AreEqual(AppointmentFit.格上, CivilServiceRules.Fitness(9, 7, prm));
            Assert.AreEqual(AppointmentFit.格下, CivilServiceRules.Fitness(5, 7, prm));
        }

        [Test]
        public void CandidateScore_NoRecordIsNeutral_HigherTierWins()
        {
            var prm = AP.Default;
            float lowTier = CivilServiceRules.CandidateScore(3, null, prm);
            float highTier = CivilServiceRules.CandidateScore(9, null, prm);
            Assert.Greater(highTier, lowTier);
        }

        [Test]
        public void SelectForOffice_PrefersMeritWhenTiersEqual()
        {
            var prm = AP.Default;
            var good = new Person(1, "良吏", Faction.帝国, PersonRole.文民) { rankTier = 7, birthYear = 780 };
            var poor = new Person(2, "凡吏", Faction.帝国, PersonRole.文民) { rankTier = 7, birthYear = 780 };
            var merits = new System.Collections.Generic.Dictionary<int, OfficialMerit>
            {
                { 1, new OfficialMerit(1) },
                { 2, new OfficialMerit(2) },
            };
            for (int i = 0; i < 3; i++) MeritEvaluationRules.Record(merits[1], MeritRating.上上);
            for (int i = 0; i < 3; i++) MeritEvaluationRules.Record(merits[2], MeritRating.下下);

            var chosen = CivilServiceRules.SelectForOffice(
                new[] { good, poor }, requiredTier: 6,
                p => merits.TryGetValue(p.id, out var m) ? m : null, prm);
            Assert.AreSame(good, chosen);
        }

        [Test]
        public void SelectForOffice_RespectsRankGate_AndAvailability()
        {
            var prm = AP.Default;
            var underranked = new Person(1, "下位", Faction.同盟, PersonRole.文民) { rankTier = 4 };
            var captive = new Person(2, "捕虜", Faction.同盟, PersonRole.文民) { rankTier = 8, captiveStatus = CaptiveStatus.捕虜 };
            var ok = new Person(3, "適任", Faction.同盟, PersonRole.文民) { rankTier = 6 };
            var chosen = CivilServiceRules.SelectForOffice(
                new[] { underranked, captive, ok }, requiredTier: 6, p => null, prm);
            Assert.AreSame(ok, chosen);
        }

        [Test]
        public void SelectForOffice_NoEligible_ReturnsNull()
        {
            var prm = AP.Default;
            var lone = new Person(1, "下位", Faction.帝国, PersonRole.文民) { rankTier = 3 };
            Assert.IsNull(CivilServiceRules.SelectForOffice(new[] { lone }, requiredTier: 7, p => null, prm));
        }
    }
}
