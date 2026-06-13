using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍産複合体（MCN-4 #1389 / CAP-3 #204・<see cref="MilitaryIndustrialRules"/>）を固定する：政治圧力、補助金/腐敗、
    /// 過剰建艦、戦争バイアス・軍産複合化、平和の経済ショック。
    /// </summary>
    public class MilitaryIndustrialTests
    {
        // ===== 政治圧力（ロビー） =====
        [Test]
        public void LobbyingPressure_RisesWithScaleAndSectionalism()
        {
            // 造船所10（基準）×省益0.8 = 0.8
            Assert.AreEqual(0.8f, MilitaryIndustrialRules.LobbyingPressure(10f, 0.8f), 1e-4f);
            // 造船所5＝半分の規模
            Assert.AreEqual(0.4f, MilitaryIndustrialRules.LobbyingPressure(5f, 0.8f), 1e-4f);
            // 基準超は頭打ち
            Assert.AreEqual(0.8f, MilitaryIndustrialRules.LobbyingPressure(20f, 0.8f), 1e-4f);
        }

        // ===== 補助金・腐敗 =====
        [Test]
        public void Subsidy_AndCorruption()
        {
            Assert.AreEqual(0.2f, MilitaryIndustrialRules.ProductionSubsidy(0.4f), 1e-4f);  // 0.4×0.5
            Assert.AreEqual(0.12f, MilitaryIndustrialRules.CorruptionGain(0.4f), 1e-4f);    // 0.4×0.3
        }

        // ===== 過剰建艦 =====
        [Test]
        public void OverkillRisk_BeyondStrategicOptimum()
        {
            Assert.AreEqual(0.5f, MilitaryIndustrialRules.OverkillRisk(150f, 100f), 1e-4f); // 5割過剰
            Assert.AreEqual(0f, MilitaryIndustrialRules.OverkillRisk(80f, 100f), 1e-4f);    // 最適未満は過剰なし
            Assert.AreEqual(0f, MilitaryIndustrialRules.OverkillRisk(150f, 0f), 1e-4f);
        }

        // ===== 戦争バイアス・軍産複合化 =====
        [Test]
        public void WarBias_AndComplexFormation()
        {
            Assert.AreEqual(0.8f, MilitaryIndustrialRules.WarBias(0.8f), 1e-4f);
            // 圧力が閾値(0.6)以上で軍産複合化が成立
            Assert.IsTrue(MilitaryIndustrialRules.IsComplex(0.8f));
            Assert.IsFalse(MilitaryIndustrialRules.IsComplex(0.4f));
            Assert.IsTrue(MilitaryIndustrialRules.IsComplex(0.5f, 0.4f));
        }

        // ===== 平和の経済ショック =====
        [Test]
        public void PeaceEconomicShock_DisarmamentHurtsDefenseEconomy()
        {
            // 軍需依存30%×軍縮率50% = 0.15（平和が経済問題になる倒錯）
            Assert.AreEqual(0.15f, MilitaryIndustrialRules.PeaceEconomicShock(0.3f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, MilitaryIndustrialRules.PeaceEconomicShock(0.3f, 0f), 1e-4f); // 軍縮なし＝痛みなし
        }
    }
}
