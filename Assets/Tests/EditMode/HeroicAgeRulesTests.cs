using NUnit.Framework;
using Ginei;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>英雄時代／英雄なき時代（キングダム）：時代局面・個の増幅・数の支配・英雄密度による時代漂流。</summary>
    public class HeroicAgeRulesTests
    {
        private static AdmiralData Admiral(int stat = 80, bool transcendent = false)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.leadership = stat; a.attack = stat; a.defense = stat; a.mobility = stat;
            a.isTranscendent = transcendent;
            a.staffOfficers = new AdmiralData[0];
            return a;
        }

        [Test]
        public void EraFor_Thresholds()
        {
            Assert.AreEqual(HeroicEra.英雄時代, HeroicAgeRules.EraFor(0.7f));
            Assert.AreEqual(HeroicEra.英雄時代, HeroicAgeRules.EraFor(0.66f));
            Assert.AreEqual(HeroicEra.移行期, HeroicAgeRules.EraFor(0.5f));
            Assert.AreEqual(HeroicEra.移行期, HeroicAgeRules.EraFor(0.33f));
            Assert.AreEqual(HeroicEra.英雄なき時代, HeroicAgeRules.EraFor(0.2f));
            Assert.AreEqual(HeroicEra.移行期, new HeroicAgeState().Era); // 既定0.5
            Assert.AreEqual(HeroicEra.英雄時代, new HeroicAgeState(0.7f).Era);
        }

        [Test]
        public void InfluenceFactors_AreInverseAcrossEra()
        {
            Assert.AreEqual(1.0f, HeroicAgeRules.HeroInfluenceFactor(0f), 1e-4f);
            Assert.AreEqual(1.5f, HeroicAgeRules.HeroInfluenceFactor(1f), 1e-4f);
            Assert.AreEqual(1.3f, HeroicAgeRules.MassInfluenceFactor(0f), 1e-4f);
            Assert.AreEqual(1.0f, HeroicAgeRules.MassInfluenceFactor(1f), 1e-4f);
            Assert.AreEqual(0.5f, HeroicAgeRules.EmergenceMultiplier(0f), 1e-4f);
            Assert.AreEqual(1.5f, HeroicAgeRules.EmergenceMultiplier(1f), 1e-4f);
        }

        [Test]
        public void EraAdjustedAbilityFactor_AmplifiesTalentEdgeOnly()
        {
            // 傑物（攻撃80＝edge0.3）：英雄時代で増幅、英雄なき時代は素のまま。
            Assert.AreEqual(1.45f, HeroicAgeRules.EraAdjustedAbilityFactor(80f, 1f), 1e-4f);
            Assert.AreEqual(1.30f, HeroicAgeRules.EraAdjustedAbilityFactor(80f, 0f), 1e-4f);
            // 凡将（50＝edge0）は時代に左右されない。
            Assert.AreEqual(1.0f, HeroicAgeRules.EraAdjustedAbilityFactor(50f, 1f), 1e-4f);
            // 無能（30＝edge-0.2）は英雄時代に露呈（さらに低下）。
            Assert.AreEqual(0.7f, HeroicAgeRules.EraAdjustedAbilityFactor(30f, 1f), 1e-4f);
        }

        [Test]
        public void EraAdjustedLanchester_NumbersDominateInHerolessAge()
        {
            Assert.AreEqual(0.65f, HeroicAgeRules.EraAdjustedLanchesterExponent(0.5f, 0f), 1e-4f); // 英雄なき＝数が二乗で効く
            Assert.AreEqual(0.5f, HeroicAgeRules.EraAdjustedLanchesterExponent(0.5f, 1f), 1e-4f);  // 英雄時代＝素のまま

            var p = new LanchesterParams(0.5f, 0.5f, 2.0f);
            var adj = HeroicAgeRules.EraAdjustedLanchesterParams(p, 0f);
            Assert.AreEqual(0.65f, adj.exponent, 1e-4f);
            Assert.AreEqual(0.5f, adj.minFactor, 1e-4f); // min/max は据え置き
            Assert.AreEqual(2.0f, adj.maxFactor, 1e-4f);
        }

        [Test]
        public void IsHero_TranscendentOrHighMartial()
        {
            Assert.IsTrue(HeroicAgeRules.IsHero(Admiral(transcendent: true)));   // 軍神
            Assert.IsTrue(HeroicAgeRules.IsHero(Admiral(stat: 95)));             // 平均武勲95≥90
            Assert.IsFalse(HeroicAgeRules.IsHero(Admiral(stat: 80)));            // 凡将
            Assert.IsFalse(HeroicAgeRules.IsHero(null));
            Assert.AreEqual(95f, HeroicAgeRules.MartialAverage(Admiral(stat: 95)), 1e-4f);
        }

        [Test]
        public void HeroDensity_And_HeroismTarget()
        {
            Assert.AreEqual(0.15f, HeroicAgeRules.HeroDensity(3, 20), 1e-4f);
            Assert.AreEqual(0f, HeroicAgeRules.HeroDensity(0, 20), 1e-4f);
            Assert.AreEqual(0f, HeroicAgeRules.HeroDensity(5, 0), 1e-4f); // 総数0
            // 参照密度0.15で目標1.0へ飽和（一握りの傑物が時代を満たす）。
            Assert.AreEqual(1.0f, HeroicAgeRules.HeroismTarget(0.15f), 1e-4f);
            Assert.AreEqual(0.5f, HeroicAgeRules.HeroismTarget(0.075f), 1e-4f);
            Assert.AreEqual(1.0f, HeroicAgeRules.HeroismTarget(0.30f), 1e-4f); // 超過はクランプ
            Assert.AreEqual(0f, HeroicAgeRules.HeroismTarget(0f), 1e-4f);
        }

        [Test]
        public void Drift_ConvergesTowardHeroDensity()
        {
            // 英雄が興る → 英雄時代へ寄る。
            Assert.AreEqual(0.6f, HeroicAgeRules.Drift(0.2f, 1.0f, 0.5f, 1.0f), 1e-4f);
            // 英雄が死に絶える → 英雄なき時代へ（目標0へ）。
            Assert.AreEqual(0.0f, HeroicAgeRules.Drift(0.6f, 0.0f, 1.0f, 1.0f), 1e-4f);
            // オーバーシュートしない（rate×dt>1 でも目標で止まる）。
            Assert.AreEqual(1.0f, HeroicAgeRules.Drift(0.5f, 1.0f, 5f, 1.0f), 1e-4f);
        }
    }
}
