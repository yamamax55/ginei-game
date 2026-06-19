using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>惑星防御シールド：被覆生成・爆撃吸収/消費・局所過負荷と穴・反撃・発生器脆弱性・防護判定。</summary>
    public class PlanetaryShieldRulesTests
    {
        [Test]
        public void ShieldCoverage_GeneratorsOverPlanetSize()
        {
            // 50基*100出力*0.01 / 100サイズ = 50/100
            Assert.AreEqual(0.5f, PlanetaryShieldRules.ShieldCoverage(50f, 100f, 100f), 1e-4f);
            // 100*100*0.01 / 50 = 2 → clamp 1.0
            Assert.AreEqual(1f, PlanetaryShieldRules.ShieldCoverage(100f, 100f, 50f), 1e-4f);
            // 発生器0＝被覆0
            Assert.AreEqual(0f, PlanetaryShieldRules.ShieldCoverage(0f, 100f, 100f), 1e-4f);
        }

        [Test]
        public void BombardmentAbsorption_ScalesWithCoverage()
        {
            Assert.AreEqual(45f, PlanetaryShieldRules.BombardmentAbsorption(0.5f, 100f), 1e-4f); // 100*0.5*0.9
            Assert.AreEqual(90f, PlanetaryShieldRules.BombardmentAbsorption(1f, 100f), 1e-4f);   // 100*1*0.9
            Assert.AreEqual(0f, PlanetaryShieldRules.BombardmentAbsorption(0f, 100f), 1e-4f);    // 被覆0＝吸収0
        }

        [Test]
        public void EnergyDraw_CoverageTimesDuration()
        {
            Assert.AreEqual(2.5f, PlanetaryShieldRules.EnergyDraw(0.5f, 10f), 1e-4f); // 0.5*10*0.5
            Assert.AreEqual(0f, PlanetaryShieldRules.EnergyDraw(0f, 10f), 1e-4f);     // 被覆0＝消費0
            Assert.AreEqual(0f, PlanetaryShieldRules.EnergyDraw(0.5f, 0f), 1e-4f);    // 展開0＝消費0
        }

        [Test]
        public void LocalOverload_HigherCoverageResists()
        {
            // 薄い被覆＝過負荷しやすい: load=100, resistance=0.5*100=50 → 100/150
            Assert.AreEqual(0.6667f, PlanetaryShieldRules.LocalOverload(100f, 0.5f, 1f), 1e-3f);
            // 厚い被覆＝耐える: resistance=0.9*100=90 → 100/190
            Assert.AreEqual(0.5263f, PlanetaryShieldRules.LocalOverload(100f, 0.9f, 1f), 1e-3f);
            // 集中度0＝過負荷なし
            Assert.AreEqual(0f, PlanetaryShieldRules.LocalOverload(100f, 0.5f, 0f), 1e-4f);
        }

        [Test]
        public void ShieldGap_OpensWithoutRedundancy()
        {
            Assert.AreEqual(0.6f, PlanetaryShieldRules.ShieldGap(0.6f, 0f), 1e-4f);   // 冗長なし＝過負荷そのまま
            Assert.AreEqual(0.3f, PlanetaryShieldRules.ShieldGap(0.6f, 0.5f), 1e-4f); // 0.6*(1-0.5)
            Assert.AreEqual(0f, PlanetaryShieldRules.ShieldGap(0.6f, 1f), 1e-4f);     // 完全冗長＝穴なし
        }

        [Test]
        public void UnderShieldCounterfire_NeedsCoverage()
        {
            Assert.AreEqual(10f, PlanetaryShieldRules.UnderShieldCounterfire(10f, 0.5f), 1e-4f); // 10*0.5*2
            Assert.AreEqual(40f, PlanetaryShieldRules.UnderShieldCounterfire(20f, 1f), 1e-4f);   // 20*1*2
            Assert.AreEqual(0f, PlanetaryShieldRules.UnderShieldCounterfire(10f, 0f), 1e-4f);    // 被覆0＝反撃不能
        }

        [Test]
        public void GeneratorVulnerability_ExposedWhereCoverageThin()
        {
            Assert.AreEqual(0.75f, PlanetaryShieldRules.GeneratorVulnerability(1f, 0.25f), 1e-4f); // 1*(1-0.25)
            Assert.AreEqual(0f, PlanetaryShieldRules.GeneratorVulnerability(1f, 1f), 1e-4f);       // 被覆満＝防護
            Assert.AreEqual(0.5f, PlanetaryShieldRules.GeneratorVulnerability(1f, 0.5f), 1e-4f);   // 1*(1-0.5)
        }

        [Test]
        public void IsPlanetShielded_CoverageMustExceedGapAndThreshold()
        {
            Assert.IsTrue(PlanetaryShieldRules.IsPlanetShielded(0.8f, 0.2f));  // 被覆厚く穴小＝防護
            Assert.IsFalse(PlanetaryShieldRules.IsPlanetShielded(0.3f, 0.1f)); // 被覆が既定閾値0.5未満
            Assert.IsFalse(PlanetaryShieldRules.IsPlanetShielded(0.6f, 0.6f)); // 穴が被覆に並ぶ＝侵入可
        }

        [Test]
        public void Narrative_FortressShieldHoldsBombardmentButFocusedSalvoOpensAGapForOrbitalEntry()
        {
            // 多数の発生器を備えた要塞惑星はシールド被覆が厚い。
            float coverage = PlanetaryShieldRules.ShieldCoverage(100f, 80f, 100f); // 100*80*0.01/100=0.8
            Assert.AreEqual(0.8f, coverage, 1e-4f);

            // 通常の軌道爆撃は大部分が吸収され、シールド下から惑星砲で反撃できる。
            float absorbed = PlanetaryShieldRules.BombardmentAbsorption(coverage, 100f); // 100*0.8*0.9=72
            Assert.AreEqual(72f, absorbed, 1e-4f);
            float counter = PlanetaryShieldRules.UnderShieldCounterfire(30f, coverage); // 30*0.8*2=48
            Assert.Greater(counter, 0f);

            // 平時はしっかり防護されている。
            float lightOverload = PlanetaryShieldRules.LocalOverload(80f, coverage, 0.3f);
            float lightGap = PlanetaryShieldRules.ShieldGap(lightOverload, 0.5f);
            Assert.IsTrue(PlanetaryShieldRules.IsPlanetShielded(coverage, lightGap));

            // しかし一点へ集中砲火を束ねると局所過負荷が高まり、発生器の冗長が乏しいと穴が空く。
            float heavyOverload = PlanetaryShieldRules.LocalOverload(400f, coverage, 1f);
            Assert.Greater(heavyOverload, lightOverload); // 集中で過負荷が増える
            float heavyGap = PlanetaryShieldRules.ShieldGap(heavyOverload, 0f); // 冗長ゼロ
            Assert.Greater(heavyGap, lightGap); // 穴が広がる

            // 穴が被覆を上回るほど開けば軌道侵入が成立する。
            float thinCoverage = PlanetaryShieldRules.ShieldCoverage(20f, 60f, 100f); // 20*60*0.01/100=0.12
            float bigOverload = PlanetaryShieldRules.LocalOverload(500f, thinCoverage, 1f);
            float bigGap = PlanetaryShieldRules.ShieldGap(bigOverload, 0f);
            Assert.IsFalse(PlanetaryShieldRules.IsPlanetShielded(thinCoverage, bigGap)); // 侵入を許す

            // 被覆が薄い箇所では発生器が露出して脆弱になる。
            float vuln = PlanetaryShieldRules.GeneratorVulnerability(1f, thinCoverage); // 1*(1-0.12)=0.88
            Assert.AreEqual(0.88f, vuln, 1e-4f);
        }
    }
}
