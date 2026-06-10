using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 星間地形を固定する：通常宙域は全倍率1・遮蔽0、星雲は探知/命中が落ち、重力圏は機動が最も落ち、小惑星帯は遮蔽最大。
    /// 被ダメ倍率＝1−遮蔽、守備有利指標の符号を担保。
    /// </summary>
    public class TerrainRulesTests
    {
        [Test]
        public void Normal_IsNeutralBaseline()
        {
            Assert.AreEqual(1f, TerrainRules.DetectionFactor(SpaceTerrain.通常宙域), 1e-5f);
            Assert.AreEqual(1f, TerrainRules.MovementFactor(SpaceTerrain.通常宙域), 1e-5f);
            Assert.AreEqual(1f, TerrainRules.AccuracyFactor(SpaceTerrain.通常宙域), 1e-5f);
            Assert.AreEqual(0f, TerrainRules.CoverBonus(SpaceTerrain.通常宙域), 1e-5f);
            Assert.AreEqual(1f, TerrainRules.DamageTakenFactor(SpaceTerrain.通常宙域), 1e-5f);
            Assert.AreEqual(0f, TerrainRules.DefensiveBias(SpaceTerrain.通常宙域), 1e-5f);
        }

        [Test]
        public void Nebula_HidesAndDegradesAccuracy()
        {
            // 星雲は探知が最も落ちる
            Assert.Less(TerrainRules.DetectionFactor(SpaceTerrain.星雲), TerrainRules.DetectionFactor(SpaceTerrain.小惑星帯));
            // 命中も落ちる
            Assert.Less(TerrainRules.AccuracyFactor(SpaceTerrain.星雲), 1f);
        }

        [Test]
        public void GravityWell_SlowsMovementMost()
        {
            float grav = TerrainRules.MovementFactor(SpaceTerrain.重力圏);
            Assert.Less(grav, TerrainRules.MovementFactor(SpaceTerrain.星雲));
            Assert.Less(grav, TerrainRules.MovementFactor(SpaceTerrain.小惑星帯));
            Assert.Less(grav, TerrainRules.MovementFactor(SpaceTerrain.恒星近傍));
        }

        [Test]
        public void Asteroids_GiveMostCover()
        {
            float cover = TerrainRules.CoverBonus(SpaceTerrain.小惑星帯);
            Assert.Greater(cover, TerrainRules.CoverBonus(SpaceTerrain.星雲));
            Assert.AreEqual(1f - cover, TerrainRules.DamageTakenFactor(SpaceTerrain.小惑星帯), 1e-5f);
        }

        [Test]
        public void DefensiveBias_PositiveInConcealingTerrain()
        {
            // 星雲・小惑星帯は命中低下＋遮蔽で守備有利（>0）
            Assert.Greater(TerrainRules.DefensiveBias(SpaceTerrain.星雲), 0f);
            Assert.Greater(TerrainRules.DefensiveBias(SpaceTerrain.小惑星帯), 0f);
        }

        [Test]
        public void Profile_AllFactorsNonNegative()
        {
            foreach (SpaceTerrain t in System.Enum.GetValues(typeof(SpaceTerrain)))
            {
                var p = TerrainRules.Profile(t);
                Assert.GreaterOrEqual(p.detection, 0f);
                Assert.GreaterOrEqual(p.movement, 0f);
                Assert.GreaterOrEqual(p.accuracy, 0f);
                Assert.GreaterOrEqual(p.cover, 0f);
                Assert.LessOrEqual(p.cover, 1f);
            }
        }
    }
}
