using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class HighGroundRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void OrbitalAdvantage_SaturatesAndClampsNegative()
        {
            var p = HighGroundParams.Default;
            // 差=scale(1000) で約0.5、差=3000 で0.75、低位(負)は0。
            Assert.AreEqual(0.5f, HighGroundRules.OrbitalAdvantage(1000f, p), Eps);
            Assert.AreEqual(0.75f, HighGroundRules.OrbitalAdvantage(3000f, p), Eps);
            Assert.AreEqual(0f, HighGroundRules.OrbitalAdvantage(-500f, p), Eps);
        }

        [Test]
        public void EnergyAdvantage_ScalesWithOrbital()
        {
            // a=0.5 -> 1.25, a=1 -> 1.5（位置エネルギーの射撃/突撃優位）。
            Assert.AreEqual(1.25f, HighGroundRules.EnergyAdvantage(0.5f), Eps);
            Assert.AreEqual(1.5f, HighGroundRules.EnergyAdvantage(1f), Eps);
        }

        [Test]
        public void GravityPenalty_HeavierShipSinksDeeper()
        {
            // 標準質量・半深 -> 0.7。重い艦が深い井戸 -> 0.4。無深度 -> 1.0。
            Assert.AreEqual(0.7f, HighGroundRules.GravityPenalty(0.5f, 1f), Eps);
            Assert.AreEqual(0.4f, HighGroundRules.GravityPenalty(1f, 3f), Eps);
            Assert.AreEqual(1f, HighGroundRules.GravityPenalty(0f, 1f), Eps);
        }

        [Test]
        public void EscapeFreedom_HighGroundCanDisengage()
        {
            // 上位ほど離脱が自由（base0.5 から 1 へ）。
            Assert.AreEqual(0.5f, HighGroundRules.EscapeFreedom(0f), Eps);
            Assert.AreEqual(0.75f, HighGroundRules.EscapeFreedom(0.5f), Eps);
            Assert.AreEqual(1f, HighGroundRules.EscapeFreedom(1f), Eps);
        }

        [Test]
        public void DescentCost_FuelEasesDescent()
        {
            // 深い井戸×燃料1 -> 0.5、半深×燃料0 -> 0.5。
            Assert.AreEqual(0.5f, HighGroundRules.DescentCost(1f, 1f), Eps);
            Assert.AreEqual(0.5f, HighGroundRules.DescentCost(0.5f, 0f), Eps);
        }

        [Test]
        public void BombardmentBonus_OneSidedFromOrbit()
        {
            // 完全上位×全曝露 -> 1.75、半々 -> 1.1875。
            Assert.AreEqual(1.75f, HighGroundRules.BombardmentBonus(1f, 1f), Eps);
            Assert.AreEqual(1.1875f, HighGroundRules.BombardmentBonus(0.5f, 0.5f), Eps);
        }

        [Test]
        public void LowGroundDesperation_DisadvantageBreedsResolve()
        {
            // 不利(0.4倍)×固い意志 -> 1.3、ペナルティ無し -> 1.0。
            Assert.AreEqual(1.3f, HighGroundRules.LowGroundDesperation(0.4f, 1f), Eps);
            Assert.AreEqual(1f, HighGroundRules.LowGroundDesperation(1f, 1f), Eps);
        }

        [Test]
        public void HasHighGround_AboveThreshold()
        {
            Assert.IsTrue(HighGroundRules.HasHighGround(500f, 100f));
            Assert.IsFalse(HighGroundRules.HasHighGround(-500f, 100f));
            Assert.IsFalse(HighGroundRules.HasHighGround(50f, 100f));
        }

        [Test]
        public void Narrative_OrbitalSuperiorityVersusDesperateLowGround()
        {
            var p = HighGroundParams.Default;

            // 軌道高位の攻撃艦：高度差3000、敵は大きく曝露(0.8)。
            float orbital = HighGroundRules.OrbitalAdvantage(3000f, p);
            Assert.AreEqual(0.75f, orbital, Eps);

            // 上位側は一方的砲撃と離脱の自由を得る。
            float bombard = HighGroundRules.BombardmentBonus(orbital, 0.8f, p);
            float escape = HighGroundRules.EscapeFreedom(orbital, p);
            Assert.AreEqual(1.45f, bombard, Eps);   // 1 + 0.75*0.8*0.75
            Assert.AreEqual(0.875f, escape, Eps);   // 0.5 + 0.75*0.5

            // 深い井戸に降りた重い防御艦は機動を縛られる。
            float defenderMobility = HighGroundRules.GravityPenalty(1f, 3f, p);
            Assert.AreEqual(0.4f, defenderMobility, Eps);

            // だが低位側は死力を尽くす（背水の陣）。
            float desperation = HighGroundRules.LowGroundDesperation(defenderMobility, 0.9f, p);
            Assert.AreEqual(1.27f, desperation, Eps); // 1 + (1-0.4)*0.9*0.5

            // 上位は撃ち負けないし退ける一方、低位は機動を失いつつも食い下がる。
            Assert.Greater(bombard, 1f);
            Assert.Greater(escape, 0.5f);
            Assert.Less(defenderMobility, 1f);
            Assert.Greater(desperation, 1f);
        }

        [Test]
        public void Params_AreClampedInConstructor()
        {
            var p = new HighGroundParams(
                altitudeScale: -10f,       // -> 0.01
                energyGain: 9f,            // -> 2
                maxGravityPenalty: 9f,     // -> 0.9
                massSensitivity: 9f,       // -> 4
                escapeBase: 9f,            // -> 1
                bombardGain: -9f,          // -> 0
                desperationGain: -9f,      // -> 0
                descentCostScale: 9f);     // -> 4
            Assert.AreEqual(0.01f, p.altitudeScale, Eps);
            Assert.AreEqual(2f, p.energyGain, Eps);
            Assert.AreEqual(0.9f, p.maxGravityPenalty, Eps);
            Assert.AreEqual(4f, p.massSensitivity, Eps);
            Assert.AreEqual(1f, p.escapeBase, Eps);
            Assert.AreEqual(0f, p.bombardGain, Eps);
            Assert.AreEqual(0f, p.desperationGain, Eps);
            Assert.AreEqual(4f, p.descentCostScale, Eps);
        }
    }
}
