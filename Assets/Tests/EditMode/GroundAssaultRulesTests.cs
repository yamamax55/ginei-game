using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class GroundAssaultRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void LandingForce_TroopsTimesAccuracy()
        {
            // 降下兵1000×降下精度0.8 -> 展開戦力800。負/過剰精度はclamp。
            Assert.AreEqual(800f, GroundAssaultRules.LandingForce(1000f, 0.8f), Eps);
            Assert.AreEqual(1000f, GroundAssaultRules.LandingForce(1000f, 2f), Eps);
            Assert.AreEqual(0f, GroundAssaultRules.LandingForce(-50f, 1f), Eps);
        }

        [Test]
        public void OrbitalFireSupport_BombardmentTimesTargeting()
        {
            // 軌道砲撃500×照準精度0.6 -> 火力支援300。精度0で0。
            Assert.AreEqual(300f, GroundAssaultRules.OrbitalFireSupport(500f, 0.6f), Eps);
            Assert.AreEqual(0f, GroundAssaultRules.OrbitalFireSupport(500f, 0f), Eps);
        }

        [Test]
        public void DefenseSuppression_AttackPressureOverDefense()
        {
            // (800+300)/(800+300+400)=1100/1500=0.733333。攻防ゼロは0（ゼロ割安全）。
            Assert.AreEqual(0.733333f, GroundAssaultRules.DefenseSuppression(800f, 300f, 400f), Eps);
            Assert.AreEqual(0f, GroundAssaultRules.DefenseSuppression(0f, 0f, 0f), Eps);
            // 敵防御0なら制圧1.0。
            Assert.AreEqual(1f, GroundAssaultRules.DefenseSuppression(800f, 300f, 0f), Eps);
        }

        [Test]
        public void TerrainEffect_CoverTimesDefenderAdvantage()
        {
            // 遮蔽0.5×防御側有利0.6×強度1.0 -> 0.3。遮蔽ゼロで0。
            Assert.AreEqual(0.3f, GroundAssaultRules.TerrainEffect(0.5f, 0.6f), Eps);
            Assert.AreEqual(0f, GroundAssaultRules.TerrainEffect(0f, 0.6f), Eps);
        }

        [Test]
        public void BeachheadSecure_SuppressionDamacedByTerrain()
        {
            // 制圧0.5×(1-地形0.3)=0.35。地形が無ければ制圧そのまま。
            Assert.AreEqual(0.35f, GroundAssaultRules.BeachheadSecure(0.5f, 0.3f), Eps);
            Assert.AreEqual(0.5f, GroundAssaultRules.BeachheadSecure(0.5f, 0f), Eps);
        }

        [Test]
        public void AssaultMomentum_ReinforcementsAddPush()
        {
            // 橋頭堡0.5×(1+0.5×増援0.4)=0.5×1.2=0.6。増援0なら橋頭堡そのまま。
            Assert.AreEqual(0.6f, GroundAssaultRules.AssaultMomentum(0.5f, 0.4f), Eps);
            Assert.AreEqual(0.5f, GroundAssaultRules.AssaultMomentum(0.5f, 0f), Eps);
        }

        [Test]
        public void AssaultAttrition_TerrainAmplifiesResistance()
        {
            // 敵抵抗0.4×(1+0.5×地形0.3)=0.4×1.15=0.46。地形なしなら抵抗そのまま。
            Assert.AreEqual(0.46f, GroundAssaultRules.AssaultAttrition(0.4f, 0.3f), Eps);
            Assert.AreEqual(0.4f, GroundAssaultRules.AssaultAttrition(0.4f, 0f), Eps);
        }

        [Test]
        public void GroundControlGained_MomentumMinusAttrition_Signed()
        {
            // 勢い0.6−消耗0.46=0.14。押し戻し（勢い<消耗）は負、-1..1にclamp。
            Assert.AreEqual(0.14f, GroundAssaultRules.GroundControlGained(0.6f, 0.46f), Eps);
            Assert.AreEqual(-0.5f, GroundAssaultRules.GroundControlGained(0.2f, 0.7f), Eps);
            Assert.AreEqual(1f, GroundAssaultRules.GroundControlGained(1f, 0f), Eps);
        }

        [Test]
        public void Narrative_OrbitalSupremacyCarriesTheBeachhead()
        {
            var p = GroundAssaultParams.Default;

            // 軌道優勢の攻撃側：1200兵を精度0.75で降下、軌道砲撃800を照準0.5で支援。
            float landing = GroundAssaultRules.LandingForce(1200f, 0.75f, p);   // 900
            float fire = GroundAssaultRules.OrbitalFireSupport(800f, 0.5f, p);  // 400
            Assert.AreEqual(900f, landing, Eps);
            Assert.AreEqual(400f, fire, Eps);

            // 敵要塞500を圧倒し地表防御を制圧。
            float suppress = GroundAssaultRules.DefenseSuppression(landing, fire, 500f, p); // 1300/1800
            Assert.AreEqual(0.722222f, suppress, Eps);

            // 軽い地形（遮蔽0.4×防御有利0.5）= 0.2 程度の不利。
            float terrain = GroundAssaultRules.TerrainEffect(0.4f, 0.5f, p); // 0.2
            Assert.AreEqual(0.2f, terrain, Eps);

            // 橋頭堡を確保し増援を流して突進。
            float beach = GroundAssaultRules.BeachheadSecure(suppress, terrain, p); // 0.722222*0.8=0.577778
            float momentum = GroundAssaultRules.AssaultMomentum(beach, 0.6f, p);    // *1.3=0.751111
            Assert.AreEqual(0.577778f, beach, Eps);
            Assert.AreEqual(0.751111f, momentum, Eps);

            // 中程度の敵抵抗0.45は軽い地形でわずかに増幅。
            float attrition = GroundAssaultRules.AssaultAttrition(0.45f, terrain, p); // 0.45*1.1=0.495
            Assert.AreEqual(0.495f, attrition, Eps);

            // 結果：勢いが消耗を上回り地上を着実に押し進める。
            float control = GroundAssaultRules.GroundControlGained(momentum, attrition, p); // 0.256111
            Assert.AreEqual(0.256111f, control, Eps);
            Assert.Greater(control, 0f);
        }

        [Test]
        public void Params_AreClampedInConstructor()
        {
            var p = new GroundAssaultParams(
                fireSupportWeight: -3f,      // -> 0
                terrainSeverity: 9f,         // -> 4
                reinforcementGain: -3f,      // -> 0
                attritionTerrainGain: 9f);   // -> 4
            Assert.AreEqual(0f, p.fireSupportWeight, Eps);
            Assert.AreEqual(4f, p.terrainSeverity, Eps);
            Assert.AreEqual(0f, p.reinforcementGain, Eps);
            Assert.AreEqual(4f, p.attritionTerrainGain, Eps);
        }
    }
}
