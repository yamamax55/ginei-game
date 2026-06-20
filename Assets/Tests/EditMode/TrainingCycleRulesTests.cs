using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ENDR-1 #2347：演習→実戦能力移転（TrainingCycleRules）。会戦結果→経験収率の純関数を検証
    /// （収率の単調性・実戦＞演習・反復逓減・逆境ボーナス・GrowthRules への配線・null安全）。
    /// </summary>
    public class TrainingCycleRulesTests
    {
        private static AutoBattleResult Result(double durationSeconds)
            => new AutoBattleResult(true, 100, durationSeconds);

        // --- TrainingFatigue：単調非増加・(0,1]・下限 ---

        [Test]
        public void Fatigue_ZeroReps_IsOne()
        {
            Assert.AreEqual(1f, TrainingCycleRules.TrainingFatigue(0), 1e-5f);
        }

        [Test]
        public void Fatigue_IsMonotonicallyNonIncreasing()
        {
            float prev = TrainingCycleRules.TrainingFatigue(0);
            for (int r = 1; r <= 50; r++)
            {
                float cur = TrainingCycleRules.TrainingFatigue(r);
                Assert.LessOrEqual(cur, prev + 1e-6f, $"fatigue rose at r={r}");
                prev = cur;
            }
        }

        [Test]
        public void Fatigue_StaysWithinBounds()
        {
            for (int r = 0; r <= 10000; r += 137)
            {
                float f = TrainingCycleRules.TrainingFatigue(r);
                Assert.GreaterOrEqual(f, TrainingCycleRules.MinFatigue - 1e-6f);
                Assert.LessOrEqual(f, 1f + 1e-6f);
            }
        }

        [Test]
        public void Fatigue_NegativeReps_TreatedAsZero()
        {
            Assert.AreEqual(TrainingCycleRules.TrainingFatigue(0), TrainingCycleRules.TrainingFatigue(-5), 1e-5f);
        }

        [Test]
        public void Fatigue_AtScale_IsAboutHalf()
        {
            // FatigueScale/(FatigueScale+r) で r=scale なら 0.5。
            int scale = (int)TrainingCycleRules.FatigueScale;
            Assert.AreEqual(0.5f, TrainingCycleRules.TrainingFatigue(scale), 1e-3f);
        }

        // --- AdversarialBonus：単調非減少・端点・クランプ ---

        [Test]
        public void Adversarial_Endpoints()
        {
            Assert.AreEqual(TrainingCycleRules.MinAdversarialBonus, TrainingCycleRules.AdversarialBonus(0f), 1e-5f);
            Assert.AreEqual(TrainingCycleRules.MaxAdversarialBonus, TrainingCycleRules.AdversarialBonus(1f), 1e-5f);
        }

        [Test]
        public void Adversarial_IsMonotonicallyNonDecreasing()
        {
            float prev = TrainingCycleRules.AdversarialBonus(0f);
            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f;
                float cur = TrainingCycleRules.AdversarialBonus(t);
                Assert.GreaterOrEqual(cur, prev - 1e-6f, $"adversarial fell at t={t}");
                prev = cur;
            }
        }

        [Test]
        public void Adversarial_ClampsOutOfRange()
        {
            Assert.AreEqual(TrainingCycleRules.MinAdversarialBonus, TrainingCycleRules.AdversarialBonus(-3f), 1e-5f);
            Assert.AreEqual(TrainingCycleRules.MaxAdversarialBonus, TrainingCycleRules.AdversarialBonus(9f), 1e-5f);
        }

        // --- ExperienceYield：実戦＞演習・反復逓減・非負・所要時間で増 ---

        [Test]
        public void Yield_RealBattle_ExceedsDrill()
        {
            var r = Result(30.0);
            float real = TrainingCycleRules.ExperienceYield(r, 0, true);
            float drill = TrainingCycleRules.ExperienceYield(r, 0, false);
            Assert.Greater(real, drill);
        }

        [Test]
        public void Yield_DiminishesWithRepetitions()
        {
            var r = Result(30.0);
            float few = TrainingCycleRules.ExperienceYield(r, 0, true);
            float many = TrainingCycleRules.ExperienceYield(r, 50, true);
            Assert.Greater(few, many);
        }

        [Test]
        public void Yield_IsNonNegative()
        {
            var r = Result(-10.0); // 異常な負の所要時間でも非負
            float y = TrainingCycleRules.ExperienceYield(r, 5, false);
            Assert.GreaterOrEqual(y, 0f);
        }

        [Test]
        public void Yield_LongerBattle_GivesMore()
        {
            float shortB = TrainingCycleRules.ExperienceYield(Result(5.0), 0, true);
            float longB = TrainingCycleRules.ExperienceYield(Result(300.0), 0, true);
            Assert.Greater(longB, shortB);
        }

        [Test]
        public void Yield_WithDifficulty_ScalesByAdversarialBonus()
        {
            var r = Result(30.0);
            float baseY = TrainingCycleRules.ExperienceYield(r, 0, true);
            float hard = TrainingCycleRules.ExperienceYield(r, 0, true, 1f);
            float easy = TrainingCycleRules.ExperienceYield(r, 0, true, 0f);
            Assert.AreEqual(baseY * TrainingCycleRules.MaxAdversarialBonus, hard, 1e-4f);
            Assert.AreEqual(baseY * TrainingCycleRules.MinAdversarialBonus, easy, 1e-4f);
            Assert.Greater(hard, easy);
        }

        // --- ApplyTraining：GrowthRules への配線・null安全 ---

        [Test]
        public void ApplyTraining_NullGrowth_IsSafe_AndReturnsYield()
        {
            float y = TrainingCycleRules.ApplyTraining(null, Result(30.0), 0, true, 0.5f);
            Assert.GreaterOrEqual(y, 0f);
        }

        [Test]
        public void ApplyTraining_IncreasesExperience()
        {
            var g = new Growth(GrowthArchetype.叩き上げ, 0f);
            float before = g.experience;
            float y = TrainingCycleRules.ApplyTraining(g, Result(60.0), 0, true, 1f);
            Assert.Greater(y, 0f);
            Assert.Greater(g.experience, before);
        }

        [Test]
        public void ApplyTraining_AccumulatesAcrossCycles()
        {
            var g = new Growth(GrowthArchetype.叩き上げ, 0f);
            TrainingCycleRules.ApplyTraining(g, Result(60.0), 0, true, 1f);
            float afterOne = g.experience;
            TrainingCycleRules.ApplyTraining(g, Result(60.0), 1, true, 1f);
            Assert.Greater(g.experience, afterOne);
        }

        [Test]
        public void ApplyTraining_HarderTraining_GrowsFaster()
        {
            var easy = new Growth(GrowthArchetype.叩き上げ, 0f);
            var hard = new Growth(GrowthArchetype.叩き上げ, 0f);
            TrainingCycleRules.ApplyTraining(easy, Result(60.0), 0, true, 0f);
            TrainingCycleRules.ApplyTraining(hard, Result(60.0), 0, true, 1f);
            Assert.Greater(hard.experience, easy.experience);
        }
    }
}
