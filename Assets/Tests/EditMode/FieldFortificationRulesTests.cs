using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class FieldFortificationRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void FortificationLevel_GrowsWithEffortAndTimeAndClamps()
        {
            var p = FieldFortificationParams.Default;
            // 努力2×時間2.5 = 5 / scale10 -> 0.5。努力5×時間4 = 20 -> 飽和1.0。負入力 -> 0。
            Assert.AreEqual(0.5f, FieldFortificationRules.FortificationLevel(2f, 2.5f, p), Eps);
            Assert.AreEqual(1f, FieldFortificationRules.FortificationLevel(5f, 4f, p), Eps);
            Assert.AreEqual(0f, FieldFortificationRules.FortificationLevel(-1f, 5f, p), Eps);
        }

        [Test]
        public void DefenseBonus_ScalesWithLevel()
        {
            // 半築城 -> 1.3、完成 -> 1.6（被ダメージへ乗算する防御倍率）。
            Assert.AreEqual(1.3f, FieldFortificationRules.DefenseBonus(0.5f), Eps);
            Assert.AreEqual(1.6f, FieldFortificationRules.DefenseBonus(1f), Eps);
        }

        [Test]
        public void CoverFromFire_ReducesHitsWithLevel()
        {
            // 遮蔽は最大 maxCover(0.5)。半築城 -> 0.25。
            Assert.AreEqual(0.25f, FieldFortificationRules.CoverFromFire(0.5f), Eps);
            Assert.AreEqual(0.5f, FieldFortificationRules.CoverFromFire(1f), Eps);
        }

        [Test]
        public void ConstructionTime_FasterWithMoreEngineers()
        {
            // 完成まで scale10/能力2 = 5。半目標×能力1 -> 5。能力が高いほど早い。
            Assert.AreEqual(5f, FieldFortificationRules.ConstructionTime(1f, 2f), Eps);
            Assert.AreEqual(5f, FieldFortificationRules.ConstructionTime(0.5f, 1f), Eps);
            Assert.Greater(
                FieldFortificationRules.ConstructionTime(1f, 1f),
                FieldFortificationRules.ConstructionTime(1f, 4f));
        }

        [Test]
        public void MobilitySacrifice_FixedInPlaceWhenDugIn()
        {
            // 築くほど動けない。半 -> 0.35、完成 -> 0.7（最大損失）。
            Assert.AreEqual(0.35f, FieldFortificationRules.MobilitySacrifice(0.5f), Eps);
            Assert.AreEqual(0.7f, FieldFortificationRules.MobilitySacrifice(1f), Eps);
        }

        [Test]
        public void PreparedKillZone_AmbushFromEntrenchedFire()
        {
            // 完成陣地×良射界 -> 1.8、半築城×半射界 -> 1.2。
            Assert.AreEqual(1.8f, FieldFortificationRules.PreparedKillZone(1f, 1f), Eps);
            Assert.AreEqual(1.2f, FieldFortificationRules.PreparedKillZone(0.5f, 0.5f), Eps);
        }

        [Test]
        public void EntrenchmentDecay_AbandonedPositionsErode()
        {
            // 維持中は不変。放棄すると decayRate0.1×dt で低下、0 下限。
            Assert.AreEqual(1f, FieldFortificationRules.EntrenchmentDecay(1f, false, 3f), Eps);
            Assert.AreEqual(0.7f, FieldFortificationRules.EntrenchmentDecay(1f, true, 3f), Eps);
            Assert.AreEqual(0f, FieldFortificationRules.EntrenchmentDecay(0.2f, true, 5f), Eps);
        }

        [Test]
        public void IsWellEntrenched_AboveThreshold()
        {
            Assert.IsTrue(FieldFortificationRules.IsWellEntrenched(0.6f, 0.5f));
            Assert.IsFalse(FieldFortificationRules.IsWellEntrenched(0.3f, 0.5f));
        }

        [Test]
        public void Narrative_DefenderDigsInThenAbandonsTheLine()
        {
            var p = FieldFortificationParams.Default;

            // 防御側が工兵4を2.5時間投じ、完成した野戦陣地を築く。
            float level = FieldFortificationRules.FortificationLevel(4f, 2.5f, p);
            Assert.AreEqual(1f, level, Eps);
            Assert.IsTrue(FieldFortificationRules.IsWellEntrenched(level, 0.8f));

            // 固い守りと遮蔽、踏み込んだ敵への事前照準した射撃帯を得る。
            float defense = FieldFortificationRules.DefenseBonus(level, p);
            float cover = FieldFortificationRules.CoverFromFire(level, p);
            float killZone = FieldFortificationRules.PreparedKillZone(level, 0.8f, p);
            Assert.AreEqual(1.6f, defense, Eps);
            Assert.AreEqual(0.5f, cover, Eps);
            Assert.AreEqual(1.64f, killZone, Eps); // 1 + 1*0.8*0.8

            // だが陣地に固定され機動を大きく失う（時間との取引の代償）。
            float lostMobility = FieldFortificationRules.MobilitySacrifice(level, p);
            Assert.AreEqual(0.7f, lostMobility, Eps);

            // 戦線を放棄して4時間移動すると、せっかくの陣地の価値は失われていく。
            float decayed = FieldFortificationRules.EntrenchmentDecay(level, true, 4f, p);
            Assert.AreEqual(0.6f, decayed, Eps);

            Assert.Greater(defense, 1f);
            Assert.Greater(killZone, 1f);
            Assert.Less(decayed, level);
        }

        [Test]
        public void Params_AreClampedInConstructor()
        {
            var p = new FieldFortificationParams(
                constructionScale: -10f,   // -> 0.01
                defenseGain: 9f,           // -> 2
                maxCover: 9f,              // -> 0.9
                maxMobilityLoss: 9f,       // -> 1
                killZoneGain: -9f,         // -> 0
                decayRate: 9f);            // -> 4
            Assert.AreEqual(0.01f, p.constructionScale, Eps);
            Assert.AreEqual(2f, p.defenseGain, Eps);
            Assert.AreEqual(0.9f, p.maxCover, Eps);
            Assert.AreEqual(1f, p.maxMobilityLoss, Eps);
            Assert.AreEqual(0f, p.killZoneGain, Eps);
            Assert.AreEqual(4f, p.decayRate, Eps);
        }
    }
}
