using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 勢力の薄い国策レイヤー（統治政策#112・ハイブリッド内政#767）を固定する：
    /// 民生＝基準（従来と同値＝後方互換）／動員・弾圧・解放が安定度目標と統合速度へ波及する。
    /// </summary>
    public class GovernancePolicyTests
    {
        [Test]
        public void Civil_IsBaseline_BackwardCompatible()
        {
            // 民生は補正0＝政策なしの旧シグネチャと同値
            float legacy = GovernanceRules.EquilibriumStability(1f, 0f, true, false);
            float civil = GovernanceRules.EquilibriumStability(1f, 0f, true, false, GovernancePolicy.民生);
            Assert.AreEqual(legacy, civil, 1e-4f);
            Assert.AreEqual(0f, GovernanceRules.PolicyStabilityModifier(GovernancePolicy.民生), 1e-4f);
            Assert.AreEqual(1f, GovernanceRules.PolicyIntegrationMultiplier(GovernancePolicy.民生), 1e-4f);
        }

        [Test]
        public void Suppress_RaisesStability_Mobilize_Lowers()
        {
            float civil = GovernanceRules.EquilibriumStability(1f, 0f, true, false, GovernancePolicy.民生);
            float suppress = GovernanceRules.EquilibriumStability(1f, 0f, true, false, GovernancePolicy.弾圧);
            float mobilize = GovernanceRules.EquilibriumStability(1f, 0f, true, false, GovernancePolicy.動員);
            Assert.Greater(suppress, civil); // 弾圧＝力による秩序で安定の目標が上がる
            Assert.Less(mobilize, civil);    // 動員＝負担で下がる
        }

        [Test]
        public void Suppress_SlowsIntegration_Liberate_Speeds()
        {
            Assert.Less(GovernanceRules.PolicyIntegrationMultiplier(GovernancePolicy.弾圧), 1f);     // 弾圧＝統合が遅い
            Assert.Greater(GovernanceRules.PolicyIntegrationMultiplier(GovernancePolicy.解放), 1f);  // 解放＝統合が速い
        }

        [Test]
        public void Tick_Liberate_IntegratesFasterThanSuppress()
        {
            // 占領直後(integration=0)から同じ時間で進めると、解放のほうが統合が進む
            var pSup = new Province { integration = 0f, stability = 15f };
            var pLib = new Province { integration = 0f, stability = 15f };
            GovernanceRules.Tick(pSup, null, true, false, 1f, GovernancePolicy.弾圧);
            GovernanceRules.Tick(pLib, null, true, false, 1f, GovernancePolicy.解放);
            Assert.Greater(pLib.integration, pSup.integration);
        }

        [Test]
        public void Tick_LegacySignature_EqualsCivilPolicy()
        {
            var pLegacy = new Province { integration = 0.3f, stability = 40f };
            var pCivil = new Province { integration = 0.3f, stability = 40f };
            GovernanceRules.Tick(pLegacy, null, true, false, 2f);                       // 旧シグネチャ
            GovernanceRules.Tick(pCivil, null, true, false, 2f, GovernancePolicy.民生);  // 明示・民生
            Assert.AreEqual(pCivil.integration, pLegacy.integration, 1e-4f);
            Assert.AreEqual(pCivil.stability, pLegacy.stability, 1e-4f);
        }
    }
}
