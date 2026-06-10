using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 宗教の数値解決（#172-175・R-1 創発とPOP宗教＝#173）を固定する：改宗圧力（支配側が強いほど高い・住民優位で0・
    /// affinity底上げ）、均衡 devotion、Tick の均衡収束（占領しても即は変わらず時間で改宗）、異端判定、社会効果係数、
    /// 聖戦圧力（聖地係争で上乗せ）。境界・クランプ・各分岐を担保する。
    /// </summary>
    public class ReligionRulesTests
    {
        private static ReligionParams P => ReligionParams.Default;

        // --- ConversionPressure ---

        [Test]
        public void ConversionPressure_RulerStronger_IsPositive()
        {
            // 支配側 0.8 > 住民 0.2、affinity無し＝差0.6×max1
            float pr = ReligionRules.ConversionPressure(0.2f, 0.8f, false, P);
            Assert.AreEqual(0.6f, pr, 1e-4f);
        }

        [Test]
        public void ConversionPressure_LocalStrongerOrEqual_IsZero()
        {
            Assert.AreEqual(0f, ReligionRules.ConversionPressure(0.8f, 0.3f, false, P), 1e-6f); // 住民優位＝改宗しない
            Assert.AreEqual(0f, ReligionRules.ConversionPressure(0.5f, 0.5f, false, P), 1e-6f); // 同値＝圧力0
        }

        [Test]
        public void ConversionPressure_AffinityBoosts_AndClamps()
        {
            // 差0.6×boost1.5=0.9（クランプ前）
            Assert.AreEqual(0.9f, ReligionRules.ConversionPressure(0.2f, 0.8f, true, P), 1e-4f);
            // 差0.9×boost1.5=1.35 → 1 にクランプ
            Assert.AreEqual(1f, ReligionRules.ConversionPressure(0.05f, 0.95f, true, P), 1e-4f);
        }

        // --- EquilibriumDevotion ---

        [Test]
        public void EquilibriumDevotion_PressureZero_StaysLocal_PressureFull_ReachesRuler()
        {
            // 住民優位＝圧力0＝現状維持
            Assert.AreEqual(0.7f, ReligionRules.EquilibriumDevotion(0.7f, 0.1f, false, P), 1e-4f);
            // 圧力1（affinityで差1）＝支配信仰まで寄る
            float eq = ReligionRules.EquilibriumDevotion(0f, 1f, true, P);
            Assert.AreEqual(1f, eq, 1e-4f);
        }

        // --- Tick ---

        [Test]
        public void Tick_MovesDevotionTowardEquilibrium_NotInstant()
        {
            var r = new Religion("帝国教", 0.2f);
            float before = r.devotion;
            ReligionRules.Tick(r, 0.8f, false, 1f, P); // 1秒、速度0.05
            Assert.Greater(r.devotion, before);                 // 改宗が進む
            Assert.Less(r.devotion, 0.8f);                       // 即時には到達しない（#173 創発）
            Assert.AreEqual(before + P.conversionSpeed, r.devotion, 1e-4f);
        }

        [Test]
        public void Tick_NullOrNonPositiveDt_NoChange()
        {
            ReligionRules.Tick(null, 0.9f, true, 1f, P); // null安全（例外なし）
            var r = new Religion("教A", 0.4f);
            ReligionRules.Tick(r, 0.9f, false, 0f, P);
            Assert.AreEqual(0.4f, r.devotion, 1e-6f);    // dt=0 で不変
        }

        // --- IsHeresy ---

        [Test]
        public void IsHeresy_DifferentNamesAreHeresy_EmptyOrSameAreNot()
        {
            Assert.IsTrue(ReligionRules.IsHeresy("地母神", "皇帝崇拝"));   // 別物＝異端
            Assert.IsFalse(ReligionRules.IsHeresy("皇帝崇拝", "皇帝崇拝")); // 同名＝正統
            Assert.IsFalse(ReligionRules.IsHeresy("", "皇帝崇拝"));        // 無信仰＝対象外
            Assert.IsFalse(ReligionRules.IsHeresy("地母神", ""));          // 公式空＝対象外
        }

        // --- SocialEffect ---

        [Test]
        public void SocialEffect_ScalesWithDevotion()
        {
            Assert.AreEqual(P.socialBase, ReligionRules.SocialEffect(new Religion("教", 0f), P), 1e-4f);
            Assert.AreEqual(P.socialBase + P.socialGain, ReligionRules.SocialEffect(new Religion("教", 1f), P), 1e-4f);
            Assert.AreEqual(P.socialBase, ReligionRules.SocialEffect(null, P), 1e-4f); // null＝基準
        }

        // --- HolyWarPressure ---

        [Test]
        public void HolyWarPressure_ProductOfFaiths_PlusContestBonus()
        {
            // 0.6×0.5=0.3、聖地非係争
            Assert.AreEqual(0.3f, ReligionRules.HolyWarPressure(0.6f, 0.5f, false, P), 1e-4f);
            // 0.3 + holySiteBonus0.4 = 0.7、係争中
            Assert.AreEqual(0.7f, ReligionRules.HolyWarPressure(0.6f, 0.5f, true, P), 1e-4f);
        }

        [Test]
        public void HolyWarPressure_ClampsToOne()
        {
            // 1×1=1 + bonus0.4 → 1 にクランプ
            Assert.AreEqual(1f, ReligionRules.HolyWarPressure(1f, 1f, true, P), 1e-4f);
        }

        // --- Religion ctor クランプ ---

        [Test]
        public void Religion_Ctor_ClampsDevotionAndNullSafeStrings()
        {
            var over = new Religion("教", 5f);
            Assert.AreEqual(1f, over.devotion, 1e-6f);          // 上限クランプ
            var under = new Religion("教", -2f);
            Assert.AreEqual(0f, under.devotion, 1e-6f);         // 下限クランプ
            var nulls = new Religion(null, 0.5f, -1, null);
            Assert.AreEqual("", nulls.faithName);               // null→空
            Assert.AreEqual("", nulls.ideologyAffinity);
        }
    }
}
