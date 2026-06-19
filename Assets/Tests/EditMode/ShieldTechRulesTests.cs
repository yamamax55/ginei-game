using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>防御スクリーン：強度生成・被弾吸収/漏れ・過負荷崩壊・再生・攻防の電力配分。</summary>
    public class ShieldTechRulesTests
    {
        [Test]
        public void ShieldStrength_OutputTimesEfficiency()
        {
            Assert.AreEqual(50f, ShieldTechRules.ShieldStrength(50f, 1f), 1e-4f);   // 50*1*1
            Assert.AreEqual(50f, ShieldTechRules.ShieldStrength(100f, 0.5f), 1e-4f); // 100*0.5*1
            Assert.AreEqual(0f, ShieldTechRules.ShieldStrength(0f, 1f), 1e-4f);      // 出力0＝強度0
        }

        [Test]
        public void EnergyAbsorption_FallsWithIncomingEnergy()
        {
            Assert.AreEqual(0.5f, ShieldTechRules.EnergyAbsorption(50f, 100f), 1e-4f);  // 容量50, 50/(50+50)
            Assert.AreEqual(1f, ShieldTechRules.EnergyAbsorption(0f, 100f), 1e-4f);     // 被弾0＝全吸収
            Assert.AreEqual(0.25f, ShieldTechRules.EnergyAbsorption(150f, 100f), 1e-4f); // 50/(50+150)
            Assert.AreEqual(0f, ShieldTechRules.EnergyAbsorption(100f, 0f), 1e-4f);     // 強度0＝吸収不能
        }

        [Test]
        public void OverloadThreshold_ScalesWithStrength()
        {
            Assert.AreEqual(100f, ShieldTechRules.OverloadThreshold(50f), 1e-4f); // 50*2
            Assert.AreEqual(0f, ShieldTechRules.OverloadThreshold(0f), 1e-4f);    // 強度0＝閾値0
        }

        [Test]
        public void ShieldCollapse_OnlyAboveThreshold()
        {
            Assert.AreEqual(0f, ShieldTechRules.ShieldCollapse(80f, 100f), 1e-4f);  // 閾値以下＝持つ
            Assert.AreEqual(1f, ShieldTechRules.ShieldCollapse(200f, 100f), 1e-4f); // 2倍＝完全崩壊
            Assert.AreEqual(0.5f, ShieldTechRules.ShieldCollapse(150f, 100f), 1e-4f); // (150-100)/100
        }

        [Test]
        public void RechargeRate_NeedsStrengthAndReserve()
        {
            Assert.AreEqual(1f, ShieldTechRules.RechargeRate(100f, 100f), 1e-4f);    // 100*100*0.0001
            Assert.AreEqual(0.25f, ShieldTechRules.RechargeRate(50f, 50f), 1e-4f);   // 50*50*0.0001
            Assert.AreEqual(0f, ShieldTechRules.RechargeRate(0f, 100f), 1e-4f);      // 強度0＝再生なし
        }

        [Test]
        public void PowerDiversion_WeaponDrawStarvesShield()
        {
            Assert.AreEqual(0.5f, ShieldTechRules.PowerDiversion(30f, 30f, 100f), 1e-4f);  // 拮抗
            Assert.AreEqual(0.25f, ShieldTechRules.PowerDiversion(60f, 20f, 100f), 1e-4f); // 攻撃多＝防御痩せ 20/80
            Assert.AreEqual(0.75f, ShieldTechRules.PowerDiversion(20f, 60f, 100f), 1e-4f); // 防御多 60/80
            Assert.AreEqual(0.5f, ShieldTechRules.PowerDiversion(50f, 50f, 0f), 1e-4f);    // 総電力0＝拮抗
        }

        [Test]
        public void BleedThrough_LeaksUnabsorbedEnergy()
        {
            Assert.AreEqual(25f, ShieldTechRules.BleedThrough(100f, 0.75f), 1e-4f); // 100*(1-0.75)
            Assert.AreEqual(0f, ShieldTechRules.BleedThrough(100f, 1f), 1e-4f);     // 全吸収＝漏れなし
            Assert.AreEqual(100f, ShieldTechRules.BleedThrough(100f, 0f), 1e-4f);   // 吸収0＝全量漏れ
        }

        [Test]
        public void IsShieldHolding_NeedsStrengthAndUnderload()
        {
            Assert.IsTrue(ShieldTechRules.IsShieldHolding(50f, 40f));   // 強度十分・被弾以下
            Assert.IsFalse(ShieldTechRules.IsShieldHolding(50f, 60f));  // 被弾が強度超＝持たない
            Assert.IsFalse(ShieldTechRules.IsShieldHolding(0.5f, 0f));  // 既定閾値1未満＝ダウン扱い
            Assert.IsTrue(ShieldTechRules.IsShieldHolding(50f, 50f));   // 境界＝持つ
        }

        [Test]
        public void Narrative_StrongScreenAbsorbsLightFireButCollapsesUnderConcentratedSalvo()
        {
            // 高出力・高効率のスクリーンは強い。
            float strength = ShieldTechRules.ShieldStrength(100f, 1f); // 100
            float threshold = ShieldTechRules.OverloadThreshold(strength); // 200

            // 軽い被弾はほぼ吸収し装甲への漏れは小さい＝スクリーンは持ちこたえる。
            float lightAbsorb = ShieldTechRules.EnergyAbsorption(20f, strength);
            float lightLeak = ShieldTechRules.BleedThrough(20f, lightAbsorb);
            Assert.AreEqual(0f, ShieldTechRules.ShieldCollapse(20f, threshold), 1e-4f);
            Assert.IsTrue(ShieldTechRules.IsShieldHolding(strength, 20f));
            Assert.Less(lightLeak, 20f); // 吸収ぶん漏れは減る

            // 集中砲火（閾値超）はスクリーンを崩壊させ、装甲への漏れも増える。
            float heavyAbsorb = ShieldTechRules.EnergyAbsorption(300f, strength);
            float heavyLeak = ShieldTechRules.BleedThrough(300f, heavyAbsorb);
            Assert.Greater(ShieldTechRules.ShieldCollapse(300f, threshold), 0f); // 崩壊する
            Assert.IsFalse(ShieldTechRules.IsShieldHolding(strength, 300f));     // 持たない
            Assert.Greater(heavyLeak, lightLeak); // 重い被弾ほど装甲へ抜ける

            // 攻撃へ電力を回すと防御配分が痩せる＝スクリーンが薄くなる（攻防のトレードオフ）。
            float defensive = ShieldTechRules.PowerDiversion(20f, 60f, 100f);
            float aggressive = ShieldTechRules.PowerDiversion(60f, 20f, 100f);
            Assert.Greater(defensive, aggressive);
        }
    }
}
