using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 新兵教育（RECRUIT・米軍 accession→BCT→AIT モデル）の純ロジックを固定する：
    /// 募集（基準/動員で増減・訓練枠で上限）／脱落率／修了者数／練度／訓練所要／軍の質への委譲。
    /// 中核は<b>質 vs 量</b>のトレードオフ（厳選＝少数精鋭・総力戦＝頭数だが練度↓）。
    /// </summary>
    public class RecruitTrainingRulesTests
    {
        private static RecruitDepot Depot(int capacity = 1000, float cadre = 0.5f, float standards = 0.5f)
            => new RecruitDepot(1, Faction.帝国, capacity, cadre, standards);

        // ===== Accessions =====

        [Test]
        public void Accessions_Null_OrEmptyPool_IsZero()
        {
            Assert.AreEqual(0, RecruitTrainingRules.Accessions(null, 1000f, 0f));
            Assert.AreEqual(0, RecruitTrainingRules.Accessions(Depot(), 0f, 0f));
            Assert.AreEqual(0, RecruitTrainingRules.Accessions(Depot(), -5f, 0f));
        }

        [Test]
        public void Accessions_BaseFraction_AtMidStandards()
        {
            // standards=0.5 → fraction = 0.20×(1−0.5×0.5) = 0.15 → 1000×0.15 = 150
            Assert.AreEqual(150, RecruitTrainingRules.Accessions(Depot(standards: 0.5f), 1000f, 0f));
        }

        [Test]
        public void Accessions_HigherStandards_FewerRecruits()
        {
            int loose = RecruitTrainingRules.Accessions(Depot(standards: 0.0f), 1000f, 0f); // 200
            int strict = RecruitTrainingRules.Accessions(Depot(standards: 1.0f), 1000f, 0f); // 100
            Assert.AreEqual(200, loose);
            Assert.AreEqual(100, strict);
            Assert.Greater(loose, strict);
        }

        [Test]
        public void Accessions_Mobilization_RaisesIntake()
        {
            int peace = RecruitTrainingRules.Accessions(Depot(standards: 0.5f), 1000f, 0f); // 150
            int surge = RecruitTrainingRules.Accessions(Depot(standards: 0.5f), 1000f, 1f); // 300
            Assert.AreEqual(300, surge);
            Assert.Greater(surge, peace);
        }

        [Test]
        public void Accessions_CappedByCapacity()
        {
            // desired 150 but capacity 50 → 50
            Assert.AreEqual(50, RecruitTrainingRules.Accessions(Depot(capacity: 50, standards: 0.5f), 1000f, 0f));
        }

        // ===== Washout =====

        [Test]
        public void Washout_LowWithGoodCadreAndStandards()
        {
            // 0.15 − 0.10 − 0.10 = −0.05 → 0
            Assert.AreEqual(0f, RecruitTrainingRules.WashoutFraction(Depot(cadre: 1f, standards: 1f), 0f), 1e-5f);
        }

        [Test]
        public void Washout_RisesWithMobilization()
        {
            // cadre0 std0 mob1 → 0.15 + 0.15 = 0.30
            Assert.AreEqual(0.30f, RecruitTrainingRules.WashoutFraction(Depot(cadre: 0f, standards: 0f), 1f), 1e-5f);
            Assert.Greater(RecruitTrainingRules.WashoutFraction(Depot(), 1f),
                           RecruitTrainingRules.WashoutFraction(Depot(), 0f));
        }

        // ===== Graduates =====

        [Test]
        public void Graduates_AppliesWashout()
        {
            Assert.AreEqual(80, RecruitTrainingRules.Graduates(100, 0.2f));
            Assert.AreEqual(100, RecruitTrainingRules.Graduates(100, 0f));
            Assert.AreEqual(0, RecruitTrainingRules.Graduates(0, 0.2f));
        }

        [Test]
        public void Graduates_OneShot_MatchesComposition()
        {
            var d = Depot();
            int acc = RecruitTrainingRules.Accessions(d, 1000f, 0f);
            float w = RecruitTrainingRules.WashoutFraction(d, 0f);
            Assert.AreEqual(RecruitTrainingRules.Graduates(acc, w),
                            RecruitTrainingRules.Graduates(d, 1000f, 0f));
        }

        // ===== Proficiency =====

        [Test]
        public void Proficiency_MaxedAtBestInputs()
        {
            // 0.35 + 0.40 + 0.25 = 1.0
            Assert.AreEqual(1f, RecruitTrainingRules.Proficiency(Depot(cadre: 1f, standards: 1f), 0f), 1e-5f);
        }

        [Test]
        public void Proficiency_FloorWhenSurgedAndUntrained()
        {
            // 0.35 − 0.30 = 0.05
            Assert.AreEqual(0.05f, RecruitTrainingRules.Proficiency(Depot(cadre: 0f, standards: 0f), 1f), 1e-5f);
        }

        [Test]
        public void Proficiency_DropsUnderMobilization()
        {
            Assert.Greater(RecruitTrainingRules.Proficiency(Depot(), 0f),
                           RecruitTrainingRules.Proficiency(Depot(), 1f));
        }

        // ===== TrainingMonths =====

        [Test]
        public void TrainingMonths_ShortenedByMobilizationWithFloor()
        {
            Assert.AreEqual(6f, RecruitTrainingRules.TrainingMonths(0f), 1e-5f);
            Assert.AreEqual(2.4f, RecruitTrainingRules.TrainingMonths(1f), 1e-5f); // max(2, 6×0.4)
            Assert.GreaterOrEqual(RecruitTrainingRules.TrainingMonths(1f), RecruitTrainingRules.MinMonths);
            Assert.Greater(RecruitTrainingRules.TrainingMonths(0f), RecruitTrainingRules.TrainingMonths(1f));
        }

        // ===== MilitaryQuality (delegation) =====

        [Test]
        public void MilitaryQuality_DelegatesToSkillEffectRules()
        {
            var d = Depot(cadre: 0.7f, standards: 0.6f);
            float p = RecruitTrainingRules.Proficiency(d, 0f);
            Assert.AreEqual(SkillEffectRules.MilitaryQuality(p, 0.5f),
                            RecruitTrainingRules.MilitaryQuality(d, 0f, 0.5f), 1e-5f);
        }

        // ===== 質 vs 量 のトレードオフ（設計の核） =====

        [Test]
        public void QualityVsQuantity_StrictDepotFewerButBetter()
        {
            var loose = Depot(capacity: 100000, cadre: 0.5f, standards: 0.2f);
            var strict = Depot(capacity: 100000, cadre: 0.5f, standards: 0.9f);
            // 厳選は受入が少ないが練度が高い
            Assert.Greater(RecruitTrainingRules.Accessions(loose, 10000f, 0f),
                           RecruitTrainingRules.Accessions(strict, 10000f, 0f));
            Assert.Greater(RecruitTrainingRules.Proficiency(strict, 0f),
                           RecruitTrainingRules.Proficiency(loose, 0f));
        }

        [Test]
        public void TotalWar_MoreBodiesLowerQuality()
        {
            var d = Depot(capacity: 100000, cadre: 0.5f, standards: 0.5f);
            // 総力戦動員＝頭数は増えるが練度は落ちる
            Assert.Greater(RecruitTrainingRules.Graduates(d, 10000f, 1f),
                           RecruitTrainingRules.Graduates(d, 10000f, 0f));
            Assert.Less(RecruitTrainingRules.Proficiency(d, 1f),
                        RecruitTrainingRules.Proficiency(d, 0f));
        }
    }
}
