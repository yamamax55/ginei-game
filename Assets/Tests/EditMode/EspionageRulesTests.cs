using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>諜報（ESP）を固定する：成功率/情報量/露見リスク/工作効果の代表・境界・クランプ・分岐。決定論（roll 注入）。</summary>
    public class EspionageRulesTests
    {
        // --- MissionSuccessChance ---

        [Test]
        public void Mission_HighSkill_NoCounter_NearCertain()
        {
            // 技量1・防諜0 → 下駄(0.1)＋残幅(0.9)＝1.0
            Assert.AreEqual(1f, EspionageRules.MissionSuccessChance(1f, 0f), 1e-4f);
        }

        [Test]
        public void Mission_HighCounter_Suppresses()
        {
            // 技量0・防諜1 → 0.1 − 0.9 = -0.8 → クランプで0
            Assert.AreEqual(0f, EspionageRules.MissionSuccessChance(0f, 1f), 1e-4f);
            // 防諜が上がるほど成功率は下がる（単調）
            Assert.Greater(
                EspionageRules.MissionSuccessChance(0.6f, 0.2f),
                EspionageRules.MissionSuccessChance(0.6f, 0.8f));
        }

        [Test]
        public void Mission_Clamped_And_RollDeterministic()
        {
            // 入力クランプ（範囲外でも[0,1]に収まる）
            float c = EspionageRules.MissionSuccessChance(5f, -5f);
            Assert.AreEqual(1f, c, 1e-4f);
            // roll 注入＝決定論：成功率1.0なら roll<1 は成功・roll=1 は失敗
            var p = EspionageRules.EspionageParams.Default;
            Assert.IsTrue(EspionageRules.MissionSucceeds(1f, 0f, p, 0.99f));
            Assert.IsFalse(EspionageRules.MissionSucceeds(1f, 0f, p, 1f));
        }

        // --- InfoGain ---

        [Test]
        public void InfoGain_ProportionalAndClamped()
        {
            Assert.AreEqual(0f, EspionageRules.InfoGain(0f), 1e-4f);
            Assert.AreEqual(0.5f, EspionageRules.InfoGain(0.5f), 1e-4f);   // infoScale=1 で比例
            Assert.AreEqual(1f, EspionageRules.InfoGain(1f), 1e-4f);
            Assert.AreEqual(1f, EspionageRules.InfoGain(3f), 1e-4f);       // 上限クランプ
            Assert.AreEqual(0f, EspionageRules.InfoGain(-2f), 1e-4f);      // 下限クランプ
        }

        // --- DetectionRisk ---

        [Test]
        public void DetectionRisk_DeepInfiltration_LowersRisk()
        {
            // 潜入0・防諜1 → 0.5×1×1 = 0.5（最大）
            Assert.AreEqual(0.5f, EspionageRules.DetectionRisk(0f, 1f), 1e-4f);
            // 潜入が深いほど露見しにくい（単調減）
            Assert.Greater(
                EspionageRules.DetectionRisk(0.2f, 1f),
                EspionageRules.DetectionRisk(0.8f, 1f));
            // 防諜0 なら露見しない
            Assert.AreEqual(0f, EspionageRules.DetectionRisk(0.5f, 0f), 1e-4f);
            // クランプ＆roll 決定論
            Assert.IsTrue(EspionageRules.IsDetected(0f, 1f, 0.49f));   // 0.49 < 0.5
            Assert.IsFalse(EspionageRules.IsDetected(0f, 1f, 0.5f));   // 0.5 は下回らない
        }

        // --- SabotageEffect ---

        [Test]
        public void SabotageEffect_ScalesWithSkill_AndCustomParams()
        {
            // 既定 sabotageScale=0.5：技量1で0.5
            Assert.AreEqual(0.5f, EspionageRules.SabotageEffect(1f), 1e-4f);
            Assert.AreEqual(0.25f, EspionageRules.SabotageEffect(0.5f), 1e-4f);
            Assert.AreEqual(0f, EspionageRules.SabotageEffect(-1f), 1e-4f); // 下限クランプ
            // カスタム Params で係数を上げる
            var p = new EspionageRules.EspionageParams(0.1f, 1f, 0.5f, 1f);
            Assert.AreEqual(1f, EspionageRules.SabotageEffect(1f, p), 1e-4f);
        }

        // --- Params clamp ---

        [Test]
        public void Params_ValuesClamped()
        {
            var p = new EspionageRules.EspionageParams(5f, 5f, 5f, 5f);
            Assert.AreEqual(1f, p.baseSuccess, 1e-4f);
            Assert.AreEqual(1f, p.infoScale, 1e-4f);
            Assert.AreEqual(1f, p.detectionBase, 1e-4f);
            Assert.AreEqual(1f, p.sabotageScale, 1e-4f);
            var n = new EspionageRules.EspionageParams(-1f, -1f, -1f, -1f);
            Assert.AreEqual(0f, n.baseSuccess, 1e-4f);
            Assert.AreEqual(0f, n.sabotageScale, 1e-4f);
        }

        // --- SpyNetwork data clamp ---

        [Test]
        public void SpyNetwork_ConstructorClamps()
        {
            var net = new SpyNetwork(2f, -3f);
            Assert.AreEqual(1f, net.infiltration, 1e-4f);
            Assert.AreEqual(0f, net.counterIntel, 1e-4f);
        }
    }
}
