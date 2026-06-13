using NUnit.Framework;
using Ginei;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>軍神＝限界突破（上杉謙信型）：天地人の整合・上限引き上げ・100超成長・登場の希少性。</summary>
    public class TenchijinRulesTests
    {
        private static AdmiralData Admiral(bool transcendent)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.isTranscendent = transcendent;
            a.staffOfficers = new AdmiralData[0];
            return a;
        }

        [Test]
        public void Alignment_RequiresAllThree_EqualFactorsGiveThatValue()
        {
            Assert.AreEqual(1f, TenchijinRules.Alignment(Tenchijin.Ideal), 1e-4f);
            Assert.AreEqual(0f, TenchijinRules.Alignment(Tenchijin.None), 1e-4f);
            // 同値なら指数の和=1ゆえその値（0.8,0.8,0.8→0.8）。
            Assert.AreEqual(0.8f, TenchijinRules.Alignment(new Tenchijin(0.8f, 0.8f, 0.8f)), 1e-4f);
            // 一つでも欠ければ崩壊（人の和が0）。
            Assert.AreEqual(0f, TenchijinRules.Alignment(new Tenchijin(1f, 1f, 0f)), 1e-4f);
        }

        [Test]
        public void IsAligned_Threshold()
        {
            Assert.IsTrue(TenchijinRules.IsAligned(new Tenchijin(0.8f, 0.8f, 0.8f)));
            Assert.IsFalse(TenchijinRules.IsAligned(new Tenchijin(0.7f, 0.7f, 0.7f)));
        }

        [Test]
        public void EffectiveCeiling_OnlyTranscendentExceeds100_ScaledByAlignment()
        {
            Assert.AreEqual(120, TenchijinRules.EffectiveCeiling(true, 1.0f));
            Assert.AreEqual(110, TenchijinRules.EffectiveCeiling(true, 0.5f));
            Assert.AreEqual(100, TenchijinRules.EffectiveCeiling(true, 0.0f)); // 天地人が無ければ突破しない
            Assert.AreEqual(100, TenchijinRules.EffectiveCeiling(false, 1.0f)); // 並の提督は揃っても100
        }

        [Test]
        public void EffectiveStat_TranscendentGrowsBeyond100_WhenAligned()
        {
            var growth = new Growth(GrowthArchetype.老練型, 100000f); // 飽和＝ボーナスはアーキタイプ天井近く（>30）
            int baseStat = 90;

            // 軍神＋天地人が揃う → 100を超える（120でクランプ）。
            int gunshin = TenchijinRules.EffectiveStat(baseStat, growth, Admiral(true), Tenchijin.Ideal);
            Assert.AreEqual(120, gunshin);
            Assert.AreEqual(20, TenchijinRules.TranscendenceAmount(baseStat, growth, Admiral(true), Tenchijin.Ideal));

            // 軍神でも天地人が無ければ100で頭打ち。
            Assert.AreEqual(100, TenchijinRules.EffectiveStat(baseStat, growth, Admiral(true), Tenchijin.None));

            // 並の提督は揃っても100で頭打ち＝超越量0（従来動作）。
            Assert.AreEqual(100, TenchijinRules.EffectiveStat(baseStat, growth, Admiral(false), Tenchijin.Ideal));
            Assert.AreEqual(0, TenchijinRules.TranscendenceAmount(baseStat, growth, Admiral(false), Tenchijin.Ideal));
        }

        [Test]
        public void Emergence_OnlyWhenAligned_Deterministic()
        {
            Assert.AreEqual(0f, TenchijinRules.EmergenceLikelihood(0.7f), 1e-4f);  // 未達
            Assert.AreEqual(0f, TenchijinRules.EmergenceLikelihood(0.8f), 1e-4f);  // しきい値ちょうどは0
            Assert.AreEqual(0.5f, TenchijinRules.EmergenceLikelihood(0.9f), 1e-4f);
            Assert.AreEqual(1f, TenchijinRules.EmergenceLikelihood(1.0f), 1e-4f);

            Assert.IsTrue(TenchijinRules.Emerges(0.9f, 0.4f));   // roll<0.5
            Assert.IsFalse(TenchijinRules.Emerges(0.9f, 0.6f));
            Assert.IsFalse(TenchijinRules.Emerges(0.7f, 0.0f));  // 揃わなければ現れない
            Assert.IsTrue(TenchijinRules.Emerges(1.0f, 0.99f));
        }

        [Test]
        public void GrowthRules_CeilingOverload_PreservesLegacyBehavior()
        {
            var growth = new Growth(GrowthArchetype.老練型, 100000f);
            // 上限を明示しない既定＝100上限と一致（従来挙動を壊さない）。
            Assert.AreEqual(GrowthRules.EffectiveStatBonus(growth, 90),
                            GrowthRules.EffectiveStatBonus(growth, 90, GrowthRules.StatCeiling));
            // 上限を100未満で渡しても従来上限（100）を下回らない＝弱体化しない。
            Assert.AreEqual(GrowthRules.EffectiveStatBonus(growth, 90, GrowthRules.StatCeiling),
                            GrowthRules.EffectiveStatBonus(growth, 90, 50));
        }
    }
}
