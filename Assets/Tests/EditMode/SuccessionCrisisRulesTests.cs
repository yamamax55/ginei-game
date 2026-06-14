using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 継承危機・内乱・クーデターの薄いオーケストレータ（<see cref="SuccessionCrisisRules"/>）のテスト。
    /// 委譲先の数式は各 Rules 側で担保済み＝ここは合成（紛争リスク↑/閾値超で内乱/橋渡し）と null 安全を担保。
    /// </summary>
    public class SuccessionCrisisRulesTests
    {
        /// <summary>後継者不在＋低正統性＋派閥対立で継承紛争リスクが高い。</summary>
        [Test]
        public void SuccessionDisputeRisk_NoHeirLowLegitimacy_IsHigh()
        {
            float risk = SuccessionCrisisRules.SuccessionDisputeRisk(false, 0.1f, 0.8f);
            Assert.Greater(risk, 0.5f, "後継不在・低正統性・高派閥対立は高リスク");
            Assert.LessOrEqual(risk, 1f);
        }

        /// <summary>指名された後継があれば曖昧さが解け、同条件でも紛争リスクが大きく下がる。</summary>
        [Test]
        public void SuccessionDisputeRisk_DesignatedHeir_LowersRisk()
        {
            float without = SuccessionCrisisRules.SuccessionDisputeRisk(false, 0.1f, 0.8f);
            float with = SuccessionCrisisRules.SuccessionDisputeRisk(true, 0.1f, 0.8f);
            Assert.Less(with, without, "後継指名は紛争リスクを下げる");
        }

        /// <summary>正統性が高ければ後継不在でも紛争リスクは低い（論争の素地が小さい）。</summary>
        [Test]
        public void SuccessionDisputeRisk_HighLegitimacy_IsLow()
        {
            float risk = SuccessionCrisisRules.SuccessionDisputeRisk(false, 1f, 0.5f);
            Assert.AreEqual(0f, risk, 1e-5f, "正統性満点なら論争の素地ゼロ＝紛争リスク0");
        }

        /// <summary>紛争リスクが閾値を超えると内乱が勃発する／下回れば起きない。</summary>
        [Test]
        public void WouldEruptCivilWar_RespectsThreshold()
        {
            Assert.IsTrue(SuccessionCrisisRules.WouldEruptCivilWar(0.7f, 0.5f), "閾値超で内乱");
            Assert.IsFalse(SuccessionCrisisRules.WouldEruptCivilWar(0.3f, 0.5f), "閾値未満は内乱なし");
            Assert.IsTrue(SuccessionCrisisRules.WouldEruptCivilWar(0.5f, 0.5f), "閾値ちょうどで勃発");
        }

        /// <summary>請求者強度配列の null/空に対し中央権威崩壊が安全（例外を投げず0）。</summary>
        [Test]
        public void CentralAuthorityCollapse_NullOrEmpty_IsSafe()
        {
            Assert.AreEqual(0f, SuccessionCrisisRules.CentralAuthorityCollapse(null), 1e-5f);
            Assert.AreEqual(0f, SuccessionCrisisRules.CentralAuthorityCollapse(new float[0]), 1e-5f);
            // 拮抗する請求者並立は崩壊度を高める（委譲先 AnarchyLevel 経由）
            float collapse = SuccessionCrisisRules.CentralAuthorityCollapse(new[] { 0.5f, 0.5f });
            Assert.Greater(collapse, 0f, "拮抗並立は中央権威を崩す");
        }

        /// <summary>軍の忠誠・支持が低いほどクーデター確率が高く、橋渡し先と一致する。</summary>
        [Test]
        public void CoupChance_DelegatesToCoupRules()
        {
            float lowLoyalty = SuccessionCrisisRules.CoupChance(0.1f, 0.5f, CoupType.軍部);
            float highLoyalty = SuccessionCrisisRules.CoupChance(0.9f, 0.5f, CoupType.軍部);
            Assert.Greater(lowLoyalty, highLoyalty, "軍の忠誠が低いほどクーデター確率↑");
            // 委譲先と完全一致（薄い橋渡し）
            Assert.AreEqual(CoupRules.CoupSuccessChance(0.1f, 0.5f, CoupType.軍部), lowLoyalty, 1e-6f);
        }

        /// <summary>軍政関係の CoupRisk/WouldCoup/ResolveCoup/簒奪 が委譲先と一致（橋渡しの健全性）。</summary>
        [Test]
        public void Bridges_MatchDelegateTargets()
        {
            Assert.AreEqual(
                CivilianControlRules.CoupRisk(CivilianControlType.軍部優位, 0.2f, 0.3f, true),
                SuccessionCrisisRules.CoupRisk(CivilianControlType.軍部優位, 0.2f, 0.3f, true), 1e-6f);
            Assert.AreEqual(
                CivilianControlRules.WouldCoup(CivilianControlType.軍部優位, 0.2f, 0.3f, true),
                SuccessionCrisisRules.WouldCoup(CivilianControlType.軍部優位, 0.2f, 0.3f, true));
            Assert.AreEqual(CoupRules.Resolve(0.6f, 0.1f), SuccessionCrisisRules.ResolveCoup(0.6f, 0.1f));
            Assert.AreEqual(
                RegencyRules.UsurpationLooms(0.9f, 0.9f, 8),
                SuccessionCrisisRules.RegentUsurpationLooms(0.9f, 0.9f, 8));
        }
    }
}
