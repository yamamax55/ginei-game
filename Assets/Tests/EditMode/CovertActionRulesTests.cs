using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// CovertActionRules（秘密工作＝否認可能な非公然作戦）の EditMode テスト。
    /// 既定 Params で期待値固定。Pow を含む箇所のみ緩める。
    /// </summary>
    public class CovertActionRulesTests
    {
        const float Eps = 1e-4f;
        // 否認可能性の層 Pow を含む箇所は緩める。
        const float PowEps = 1e-3f;

        [Test]
        public void OperationSuccess_ResourcesSupportOverResilience()
        {
            // 0.8 × 0.6 / (0.6+0.4) = 0.48 / 1.0 = 0.48
            Assert.AreEqual(0.48f, CovertActionRules.OperationSuccess(0.8f, 0.6f, 0.4f), Eps);
        }

        [Test]
        public void Deniability_LayersAndFootprint_PowDecay()
        {
            // 0.6^2 = 0.36 → layerDen 0.64; footprintDen 1-0.2 = 0.8 → 0.64×0.8 = 0.512
            Assert.AreEqual(0.512f, CovertActionRules.Deniability(2, 0.2f), PowEps);
        }

        [Test]
        public void Deniability_NoLayers_NoCover()
        {
            // 0.6^0 = 1 → layerDen 0 → 否認可能性ゼロ（直接手を下せば言い逃れできない）。
            Assert.AreEqual(0f, CovertActionRules.Deniability(0, 0f), PowEps);
        }

        [Test]
        public void ExposureRisk_FootprintCounterIntelTimesUndeniability()
        {
            // 0.5 × (0.5×1.2=0.6) × (1-0.512=0.488) = 0.3 × 0.488 = 0.1464
            Assert.AreEqual(0.1464f, CovertActionRules.ExposureRisk(0.5f, 0.5f, 0.512f), Eps);
        }

        [Test]
        public void Blowback_ExposureTimesAggressiveness()
        {
            // 0.1464 × 0.8 × 1.0 = 0.11712
            Assert.AreEqual(0.11712f, CovertActionRules.Blowback(0.1464f, 0.8f), Eps);
        }

        [Test]
        public void PlausibleDenial_HighDeniability_LowExposure_True()
        {
            // 0.512 ≥ 0.5 かつ 0.1464 ≤ 0.512 → 言い逃れ成立
            Assert.IsTrue(CovertActionRules.PlausibleDenial(0.512f, 0.1464f));
            // 否認可能性が閾値割れ → 失敗
            Assert.IsFalse(CovertActionRules.PlausibleDenial(0.4f, 0.1f));
            // 暴露が否認可能性を上回る → 失敗
            Assert.IsFalse(CovertActionRules.PlausibleDenial(0.6f, 0.7f));
        }

        [Test]
        public void CovertCostEffectiveness_NetOverCost()
        {
            // (0.48 - 0.11712) / (1+1.0) = 0.36288 / 2 = 0.18144
            Assert.AreEqual(0.18144f, CovertActionRules.CovertCostEffectiveness(0.48f, 0.11712f, 1.0f), Eps);
        }

        [Test]
        public void EscalationToOvert_ScalesWithBlowback()
        {
            // 0.11712 × 0.8 = 0.093696
            Assert.AreEqual(0.093696f, CovertActionRules.EscalationToOvert(0.11712f), Eps);
        }

        [Test]
        public void IsWorthLaunching_AboveAndBelowThreshold()
        {
            Assert.IsTrue(CovertActionRules.IsWorthLaunching(0.18144f));   // ≥ 0.1
            Assert.IsFalse(CovertActionRules.IsWorthLaunching(0.05f));     // < 0.1
        }

        [Test]
        public void Story_DeniableCoupSupport_LaunchedAndDenied()
        {
            // 物語：現地に協力者多く（0.6）標的の抵抗弱い（0.4）政権へ、
            // 資源を投入し（0.8）、3層の代理人を隔て痕跡を抑えて（0.2）扶植する。
            var p = CovertActionRules.CovertActionParams.Default;

            float success = CovertActionRules.OperationSuccess(0.8f, 0.6f, 0.4f);
            Assert.AreEqual(0.48f, success, Eps);

            // 3層なら 0.6^3 = 0.216 → layerDen 0.784; footprintDen 0.8 → 0.6272
            float deniability = CovertActionRules.Deniability(3, 0.2f);
            Assert.AreEqual(0.6272f, deniability, PowEps);

            // 暴露：0.2 × (0.5×1.2=0.6) × (1-0.6272=0.3728) = 0.12 × 0.3728 = 0.044736
            float exposure = CovertActionRules.ExposureRisk(0.2f, 0.5f, deniability);
            Assert.AreEqual(0.044736f, exposure, Eps);

            // 過激さは低め（0.4）→ 反動 0.044736×0.4 = 0.0178944
            float blowback = CovertActionRules.Blowback(exposure, 0.4f);
            Assert.AreEqual(0.0178944f, blowback, Eps);

            // 否認可能性が高く暴露が低い → しらを切り通せる
            Assert.IsTrue(CovertActionRules.PlausibleDenial(deniability, exposure));

            // 費用対効果：(0.48 - 0.0178944) / (1+0.5) = 0.4621056/1.5 = 0.3080704
            float cce = CovertActionRules.CovertCostEffectiveness(success, blowback, 0.5f);
            Assert.AreEqual(0.3080704f, cce, Eps);

            // 公然衝突への転化圧は小さい（0.0178944×0.8 = 0.01431552）
            float escal = CovertActionRules.EscalationToOvert(blowback);
            Assert.AreEqual(0.01431552f, escal, Eps);

            // 採算が立つ → 実行する価値あり
            Assert.IsTrue(CovertActionRules.IsWorthLaunching(cce));
        }
    }
}
