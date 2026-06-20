using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 小惑星帯戦を固定する：航行危険＝密度×サイズ/技量、機動制限＝密度×サイズ、射線遮蔽＝密度×距離、
    /// 衝突＝危険×速度、遮蔽価値（上限0.8）、小型優位＝(1−サイズ)×密度、待伏＝遮蔽×密度、通過可否。
    /// 既定 Params の期待値と境界・clamp を担保。
    /// </summary>
    public class AsteroidFieldRulesTests
    {
        private static readonly AsteroidFieldParams P = AsteroidFieldParams.Default;
        // 危険1/機動1/遮蔽1(基準1)/衝突1/遮蔽価値0.8/小型1/待伏1/操艦軽減0.5

        [Test]
        public void NavigationHazard_DensityTimesSizeOverSkill()
        {
            Assert.AreEqual(0.6f, AsteroidFieldRules.NavigationHazard(0.5f, 0.6f, 0.5f, P), 1e-4f); // 0.5*0.6/0.5
            Assert.AreEqual(0.3f, AsteroidFieldRules.NavigationHazard(0.5f, 0.6f, 1f, P), 1e-4f);   // 巧者で半減
            Assert.AreEqual(1f, AsteroidFieldRules.NavigationHazard(1f, 1f, 0f, P), 1e-4f);          // 技量0→下限0.1→clamp1
        }

        [Test]
        public void MobilityRestriction_LargeShipsStuck()
        {
            Assert.AreEqual(0.4f, AsteroidFieldRules.MobilityRestriction(0.5f, 0.8f, P), 1e-4f);
            Assert.AreEqual(1f, AsteroidFieldRules.MobilityRestriction(1f, 1f, P), 1e-4f);   // 巨艦×濃密＝身動き不能
            Assert.AreEqual(0f, AsteroidFieldRules.MobilityRestriction(0f, 1f, P), 1e-4f);   // 岩塊なし＝制限なし
        }

        [Test]
        public void LineOfSightBlockage_DensityTimesRange()
        {
            Assert.AreEqual(0.5f, AsteroidFieldRules.LineOfSightBlockage(0.5f, 1f, P), 1e-4f);
            Assert.AreEqual(1f, AsteroidFieldRules.LineOfSightBlockage(0.5f, 2f, P), 1e-4f); // 遠距離で飽和
            Assert.AreEqual(0f, AsteroidFieldRules.LineOfSightBlockage(0.5f, 0f, P), 1e-4f); // 至近は素通し
        }

        [Test]
        public void CollisionRisk_HazardTimesSpeed()
        {
            Assert.AreEqual(0.3f, AsteroidFieldRules.CollisionRisk(0.6f, 0.5f, P), 1e-4f);
            Assert.AreEqual(1f, AsteroidFieldRules.CollisionRisk(1f, 1f, P), 1e-4f);   // 危険を全速＝必ずぶつかる
            Assert.AreEqual(0f, AsteroidFieldRules.CollisionRisk(0.6f, 0f, P), 1e-4f); // 停止なら衝突せず
        }

        [Test]
        public void CoverValue_ScaledByCoverCap()
        {
            Assert.AreEqual(0.4f, AsteroidFieldRules.CoverValue(0.5f, P), 1e-4f);
            Assert.AreEqual(0.8f, AsteroidFieldRules.CoverValue(1f, P), 1e-4f);   // 上限0.8（完全には隠れない）
            Assert.AreEqual(0f, AsteroidFieldRules.CoverValue(0f, P), 1e-4f);
        }

        [Test]
        public void SmallShipAdvantage_FavorsSmallInDenseField()
        {
            Assert.AreEqual(0.8f, AsteroidFieldRules.SmallShipAdvantage(0.2f, 1f, P), 1e-4f); // (1-0.2)*1
            Assert.AreEqual(0f, AsteroidFieldRules.SmallShipAdvantage(1f, 1f, P), 1e-4f);     // 巨艦に優位なし
            Assert.AreEqual(0.5f, AsteroidFieldRules.SmallShipAdvantage(0f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void AmbushPosition_CoverTimesDensity()
        {
            Assert.AreEqual(0.25f, AsteroidFieldRules.AmbushPosition(0.5f, 0.5f, P), 1e-4f);
            Assert.AreEqual(0.8f, AsteroidFieldRules.AmbushPosition(0.8f, 1f, P), 1e-4f);
            Assert.AreEqual(0f, AsteroidFieldRules.AmbushPosition(0f, 1f, P), 1e-4f); // 隠れられねば伏撃地でない
        }

        [Test]
        public void IsPassable_SkilledPilotTamesHazard()
        {
            Assert.IsTrue(AsteroidFieldRules.IsPassable(0.6f, 0.5f, 0.6f));   // 0.6*0.75=0.45<=0.6
            Assert.IsTrue(AsteroidFieldRules.IsPassable(0.6f, 0f, 0.6f));    // 0.6<=0.6 ぎりぎり可
            Assert.IsFalse(AsteroidFieldRules.IsPassable(0.9f, 0f, 0.6f));   // 素人に危険宙域は通れない
            Assert.IsTrue(AsteroidFieldRules.IsPassable(0.9f, 1f, 0.6f));    // 名手なら同じ宙域も御す(0.9*0.5=0.45)
            Assert.IsTrue(AsteroidFieldRules.IsPassable(0.6f, 0.5f));         // 既定閾値委譲版
        }

        [Test]
        public void ClampInputs_NoOutOfRange()
        {
            // 過大入力でも 0..1 に収まる
            Assert.AreEqual(1f, AsteroidFieldRules.NavigationHazard(5f, 5f, -1f, P), 1e-4f);
            Assert.AreEqual(1f, AsteroidFieldRules.MobilityRestriction(9f, 9f, P), 1e-4f);
            Assert.AreEqual(0f, AsteroidFieldRules.CollisionRisk(-3f, 9f, P), 1e-4f);
            Assert.AreEqual(0.8f, AsteroidFieldRules.CoverValue(9f, P), 1e-4f);
        }

        [Test]
        public void Story_LightCruiserAmbushesBattleshipInAsteroidBelt()
        {
            // 濃密な小惑星帯（密度0.9）。巨大戦艦（サイズ1.0）と軽巡（サイズ0.2、操艦巧者0.9）が相対する。
            const float density = 0.9f;

            // 巨艦は身動きが取れず、軽巡は小回りで優位。
            float bigMobility = AsteroidFieldRules.MobilityRestriction(density, 1.0f, P);
            float lightAdvantage = AsteroidFieldRules.SmallShipAdvantage(0.2f, density, P);
            Assert.Greater(bigMobility, 0.8f);     // 戦艦はほぼ動けない
            Assert.Greater(lightAdvantage, 0.7f);  // 軽巡が圧倒的に有利

            // 巨艦の航行は危険（操艦下手0.3）、軽巡は名手で抑える。
            float bigHazard = AsteroidFieldRules.NavigationHazard(density, 1.0f, 0.3f, P);
            float lightHazard = AsteroidFieldRules.NavigationHazard(density, 0.2f, 0.9f, P);
            Assert.Greater(bigHazard, lightHazard);

            // 軽巡は岩塊に隠れて待ち伏せ拠点を確保（遠距離 range1.0）。
            float blockage = AsteroidFieldRules.LineOfSightBlockage(density, 1.0f, P);
            float cover = AsteroidFieldRules.CoverValue(blockage, P);
            float ambush = AsteroidFieldRules.AmbushPosition(cover, density, P);
            Assert.Greater(cover, 0.5f);    // 十分に隠れられる
            Assert.Greater(ambush, 0.5f);   // 好適な伏撃地

            // 名手の軽巡はこの宙域を通過できるが、下手な巨艦は通れない。
            Assert.IsTrue(AsteroidFieldRules.IsPassable(lightHazard, 0.9f));
            Assert.IsFalse(AsteroidFieldRules.IsPassable(bigHazard, 0.3f));
        }
    }
}
