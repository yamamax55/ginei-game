using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// ShipArmorRules（装甲＝防御力と貫通モデル）の EditMode テスト。
    /// 既定 Params の期待値を手計算で固定。
    /// </summary>
    public class ShipArmorRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void EffectiveArmor_Slope_IncreasesThickness()
        {
            // 直角(0)=×1、極浅角(1)=×1.5、中間(0.5)=×1.25
            Assert.AreEqual(100f, ShipArmorRules.EffectiveArmor(100f, 0f), Eps);
            Assert.AreEqual(150f, ShipArmorRules.EffectiveArmor(100f, 1f), Eps);
            Assert.AreEqual(125f, ShipArmorRules.EffectiveArmor(100f, 0.5f), Eps);
        }

        [Test]
        public void PenetrationChance_RatioOfPenToTotal()
        {
            // 60/(60+40)=0.6
            Assert.AreEqual(0.6f, ShipArmorRules.PenetrationChance(60f, 40f), Eps);
            // 拮抗=0.5
            Assert.AreEqual(0.5f, ShipArmorRules.PenetrationChance(50f, 50f), Eps);
            // 両方0は貫通不能
            Assert.AreEqual(0f, ShipArmorRules.PenetrationChance(0f, 0f), Eps);
        }

        [Test]
        public void DamageReduction_ClampedToCap()
        {
            // 100/(100+100)=0.5（cap 0.85 未満）
            Assert.AreEqual(0.5f, ShipArmorRules.DamageReduction(100f, 100f), Eps);
            // 1000/1010≈0.990 → cap 0.85 で頭打ち
            Assert.AreEqual(0.85f, ShipArmorRules.DamageReduction(1000f, 10f), Eps);
        }

        [Test]
        public void FacingArmor_FrontThickRearThin()
        {
            // 正面(0)=×1.5、側面(0.5)=×1.0、背面(1)=×0.5
            Assert.AreEqual(150f, ShipArmorRules.FacingArmor(100f, 0f), Eps);
            Assert.AreEqual(100f, ShipArmorRules.FacingArmor(100f, 0.5f), Eps);
            Assert.AreEqual(50f, ShipArmorRules.FacingArmor(100f, 1f), Eps);
        }

        [Test]
        public void OverPenetration_CapsWhenPenFarExceedsArmor()
        {
            // ratio 300/100=3 ≥ 2 → 過貫通 0.6
            Assert.AreEqual(0.6f, ShipArmorRules.OverPenetration(300f, 100f), Eps);
            // ratio 1.5 < 2 → フルダメージ 1.0
            Assert.AreEqual(1f, ShipArmorRules.OverPenetration(150f, 100f), Eps);
            // 装甲皆無は素通り（頭打ち）
            Assert.AreEqual(0.6f, ShipArmorRules.OverPenetration(100f, 0f), Eps);
        }

        [Test]
        public void ArmorAblation_DegradesWithHits()
        {
            // 損耗 0.05*3=0.15 → 残存 85
            Assert.AreEqual(85f, ShipArmorRules.ArmorAblation(100f, 3), Eps);
            // 0被弾なら無傷
            Assert.AreEqual(100f, ShipArmorRules.ArmorAblation(100f, 0), Eps);
            // 多被弾でも残存は0下限（loss は 0..1 clamp）
            Assert.AreEqual(0f, ShipArmorRules.ArmorAblation(100f, 1000), Eps);
        }

        [Test]
        public void ShellShatter_ShallowAndWeakBounces()
        {
            // 浅角(1.0)×低貫通(10<30) → 弾き
            Assert.IsTrue(ShipArmorRules.ShellShatter(1.0f, 10f));
            // 直角(0.0) → 弾かない
            Assert.IsFalse(ShipArmorRules.ShellShatter(0.0f, 10f));
            // 浅角でも高貫通(50≥30) → 弾かない
            Assert.IsFalse(ShipArmorRules.ShellShatter(1.0f, 50f));
        }

        [Test]
        public void IsPenetrated_DeterministicRoll()
        {
            // roll ≤ 確率 で貫通
            Assert.IsTrue(ShipArmorRules.IsPenetrated(0.6f, 0.5f));
            Assert.IsTrue(ShipArmorRules.IsPenetrated(0.6f, 0.6f));
            Assert.IsFalse(ShipArmorRules.IsPenetrated(0.6f, 0.7f));
        }

        [Test]
        public void Params_CtorClampsInputs()
        {
            // slopeBonus<1 は1へ、各種は範囲へ clamp
            var p = new ShipArmorParams(
                slopeBonus: 0.2f, reductionCap: 2f, facingSpread: -1f,
                overPenThreshold: 0.1f, overPenCap: 5f, ablationPerHit: -3f,
                shatterAngle: 9f, shatterPenFloor: -10f);
            Assert.AreEqual(1f, p.slopeBonus, Eps);
            Assert.AreEqual(1f, p.reductionCap, Eps);
            Assert.AreEqual(0f, p.facingSpread, Eps);
            Assert.AreEqual(1f, p.overPenThreshold, Eps);
            Assert.AreEqual(1f, p.overPenCap, Eps);
            Assert.AreEqual(0f, p.ablationPerHit, Eps);
            Assert.AreEqual(1f, p.shatterAngle, Eps);
            Assert.AreEqual(0f, p.shatterPenFloor, Eps);
        }

        // 物語テスト：背面への浅角・低貫通の砲撃は弾かれ、正面装甲を貫通力で抜くと過貫通で頭打ちになる流れ。
        [Test]
        public void Narrative_RearShallowBounces_FrontHardPenetratesButOverPens()
        {
            var p = ShipArmorParams.Default;

            // 背面（薄い）に浅角・低貫通で砲撃 → まず弾く
            Assert.IsTrue(ShipArmorRules.ShellShatter(1.0f, 20f, p), "浅角×低貫通は弾く");

            // 正面（厚い）に直角・高貫通で砲撃
            float frontArmor = ShipArmorRules.FacingArmor(100f, 0f, p);      // 150
            float effective = ShipArmorRules.EffectiveArmor(frontArmor, 0f, p); // 直角→150
            Assert.AreEqual(150f, effective, Eps);

            // 高貫通(400)なら貫通確率は高い
            float chance = ShipArmorRules.PenetrationChance(400f, effective, p); // 400/550≈0.7273
            Assert.AreEqual(400f / 550f, chance, 1e-3f);
            Assert.IsTrue(ShipArmorRules.IsPenetrated(chance, 0.5f), "高貫通で抜く");

            // ただし ratio 400/150≈2.67 ≥ 2 → 過貫通で頭打ち（素通り）
            Assert.AreEqual(p.overPenCap, ShipArmorRules.OverPenetration(400f, effective, p), Eps);
        }
    }
}
