using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// GrowthRules（提督の成長アーキタイプ #537-543）の EditMode テスト。
    /// 経験の時間発展・アーキタイプ別曲線・実効ボーナスの上限/天井/クランプを決定論で担保する。
    /// </summary>
    public class GrowthRulesTests
    {
        // ForArchetype：各アーキタイプの設計意図（早咲き/出世遅/晩成/希少）を区別できる。
        [Test]
        public void ForArchetype_DistinguishesArchetypes()
        {
            var top = GrowthRules.ForArchetype(GrowthArchetype.首席型);
            var hidden = GrowthRules.ForArchetype(GrowthArchetype.在野俊英型);
            var veteran = GrowthRules.ForArchetype(GrowthArchetype.老練型);
            var risen = GrowthRules.ForArchetype(GrowthArchetype.叩き上げ);

            // 首席型は初期補正が最も高い。
            Assert.Greater(top.initialBonus, risen.initialBonus);
            Assert.Greater(top.initialBonus, veteran.initialBonus);
            // 首席型は昇進適性が最高、在野俊英型は最低（高天井だが出世が遅い）。
            Assert.Greater(top.promotionAptitude, hidden.promotionAptitude);
            Assert.Greater(hidden.ceiling, top.ceiling);
            // 老練型は晩成＝speed が遅いが天井が高い。
            Assert.Less(veteran.speed, top.speed);
            Assert.Greater(veteran.ceiling, risen.ceiling);
        }

        // GainExperience：時間発展で経験が成長速度ぶん加算される。
        [Test]
        public void GainExperience_AddsScaledBySpeed()
        {
            var top = new Growth(GrowthArchetype.首席型);
            var risen = new Growth(GrowthArchetype.叩き上げ);

            GrowthRules.GainExperience(top, 10f, 1f);
            GrowthRules.GainExperience(risen, 10f, 1f);

            // 速度倍率で増分が変わる（首席1.3 > 叩き上げ0.8）。
            Assert.Greater(top.experience, risen.experience);
            Assert.AreEqual(10f * 1.3f, top.experience, 1e-4f);
        }

        // GainExperience：負の amount/dt は0扱い（経験は減らない・増えない）。
        [Test]
        public void GainExperience_NegativeArgsClampedToZero()
        {
            var g = new Growth(GrowthArchetype.老練型, 5f);

            GrowthRules.GainExperience(g, -100f, 1f);
            Assert.AreEqual(5f, g.experience, 1e-4f, "負の amount は加算0");

            GrowthRules.GainExperience(g, 100f, -1f);
            Assert.AreEqual(5f, g.experience, 1e-4f, "負の dt は加算0");
        }

        // GainExperience：null セーフ（例外を投げない）。
        [Test]
        public void GainExperience_NullSafe()
        {
            Assert.DoesNotThrow(() => GrowthRules.GainExperience(null, 10f, 1f));
        }

        // EffectiveStatBonus：経験0でも初期補正が乗る／経験で単調増加する。
        [Test]
        public void EffectiveStatBonus_InitialBonusAndMonotonic()
        {
            var fresh = new Growth(GrowthArchetype.首席型, 0f);
            int bonus0 = GrowthRules.EffectiveStatBonus(fresh, 50);
            Assert.AreEqual(12, bonus0, "首席型の初期補正がそのまま乗る");

            var experienced = new Growth(GrowthArchetype.首席型, 100f);
            int bonusExp = GrowthRules.EffectiveStatBonus(experienced, 50);
            Assert.Greater(bonusExp, bonus0, "経験を積むとボーナスが増える");
        }

        // EffectiveStatBonus：base+bonus が MaxStatValue を超えない（基準非破壊のクランプ）。
        [Test]
        public void EffectiveStatBonus_DoesNotExceedMaxStatValue()
        {
            var g = new Growth(GrowthArchetype.在野俊英型, 100000f);
            int high = AdmiralData.MaxStatValue - 5; // 基準が高い

            int bonus = GrowthRules.EffectiveStatBonus(g, high);

            Assert.LessOrEqual(high + bonus, AdmiralData.MaxStatValue);
            Assert.AreEqual(5, bonus, "残り枠ぴったりまでで頭打ち");
            // 基準が上限なら追加ボーナス0。
            Assert.AreEqual(0, GrowthRules.EffectiveStatBonus(g, AdmiralData.MaxStatValue));
        }

        // EffectiveStatBonus：アーキタイプ天井でクランプ（飽和しても ceiling を超えない）。
        [Test]
        public void EffectiveStatBonus_ClampedToArchetypeCeiling()
        {
            // 首席型は飽和カーブの漸近値(initialBonus+peak=37)が天井(30)を上回るため、
            // 巨大経験で天井にクランプされる＝ceiling が実際に効くケース。
            // （叩き上げ等は漸近値が天井未満でクランプが発動しない＝自然に天井下で頭打ち＝設計どおり）
            var risen = new Growth(GrowthArchetype.首席型, 1_000_000f);
            int bonus = GrowthRules.EffectiveStatBonus(risen, 0);

            var p = GrowthRules.ForArchetype(GrowthArchetype.首席型);
            Assert.LessOrEqual(bonus, p.ceiling, "アーキタイプ天井を超えない");
            Assert.AreEqual(p.ceiling, bonus, "巨大経験では天井に張り付く");
        }

        // EffectiveStatBonus：null セーフ＝0。
        [Test]
        public void EffectiveStatBonus_NullReturnsZero()
        {
            Assert.AreEqual(0, GrowthRules.EffectiveStatBonus(null, 50));
        }

        // 晩成の体現：老練型は経験を十分積むと首席型を実効ボーナスで追い越せる（高天井）。
        [Test]
        public void EffectiveStatBonus_VeteranOvertakesTopWhenExperienced()
        {
            var veteran = new Growth(GrowthArchetype.老練型, 100000f);
            var top = new Growth(GrowthArchetype.首席型, 100000f);

            int vBonus = GrowthRules.EffectiveStatBonus(veteran, 0);
            int tBonus = GrowthRules.EffectiveStatBonus(top, 0);

            Assert.Greater(vBonus, tBonus, "晩成は長期で早咲きを上回る");
        }
    }
}
