using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>集団安全保障（国際連盟型）の純ロジックを既定Paramsの具体値で固定する。</summary>
    public class CollectiveSecurityRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>対岸の火事＝遠くて高くつく制裁には参加意欲が低い（依存1・脅威遠1で0）。</summary>
        [Test]
        public void SanctionParticipation_対岸の火事は不参加()
        {
            // 自己負担最大(依存1)・脅威最遠(遠さ1)＝安さ0×0.6＋近さ0×0.4＝0
            Assert.AreEqual(0f, CollectiveSecurityRules.SanctionParticipation(1f, 1f), Eps);
            // 損なし(依存0)・脅威至近(遠さ0)＝1×0.6＋1×0.4＝1
            Assert.AreEqual(1f, CollectiveSecurityRules.SanctionParticipation(0f, 0f), Eps);
            // 中間：依存0.5(安さ0.5)・遠さ0.5(近さ0.5)＝0.5×0.6＋0.5×0.4＝0.5
            Assert.AreEqual(0.5f, CollectiveSecurityRules.SanctionParticipation(0.5f, 0.5f), Eps);
        }

        /// <summary>足並みが閾値(0.5)未満なら集団対応は無力(0)、超えれば平均が出る。</summary>
        [Test]
        public void CollectiveResponse_足並み閾値()
        {
            // 平均0.4＜0.5＝無力
            Assert.AreEqual(0f, CollectiveSecurityRules.CollectiveResponse(new[] { 0.4f, 0.4f }), Eps);
            // 平均0.7≥0.5＝そのまま
            Assert.AreEqual(0.7f, CollectiveSecurityRules.CollectiveResponse(new[] { 0.6f, 0.8f }), Eps);
            // 空/nullは0
            Assert.AreEqual(0f, CollectiveSecurityRules.CollectiveResponse(new float[0]), Eps);
            Assert.AreEqual(0f, CollectiveSecurityRules.CollectiveResponse(null), Eps);
        }

        /// <summary>見過ごし(成功0)は信頼性を急落させ、成功(1)はゆっくり回復＝崩壊≫回復。</summary>
        [Test]
        public void CredibilityTick_崩壊は回復より速い()
        {
            // 成功0：drive=-0.5→-0.5×2×0.3×1=-0.3。0.8→0.5
            Assert.AreEqual(0.5f, CollectiveSecurityRules.CredibilityTick(0.8f, 0f, 1f), Eps);
            // 成功1：drive=0.5→0.5×2×0.05×1=0.05。0.5→0.55
            Assert.AreEqual(0.55f, CollectiveSecurityRules.CredibilityTick(0.5f, 1f, 1f), Eps);
            // 成功0.5：分岐点＝変化なし
            Assert.AreEqual(0.6f, CollectiveSecurityRules.CredibilityTick(0.6f, 0.5f, 1f), Eps);
        }

        /// <summary>抑止力＝信頼性×実効力の積。どちらかゼロなら抑止ゼロ＝建前だけでは止められない。</summary>
        [Test]
        public void DeterrenceValue_積でどちらか欠けると無効()
        {
            Assert.AreEqual(0.48f, CollectiveSecurityRules.DeterrenceValue(0.6f, 0.8f), Eps);
            // 信頼性は高いが足並み0＝抑止0
            Assert.AreEqual(0f, CollectiveSecurityRules.DeterrenceValue(1f, 0f), Eps);
            // 足並みは揃うが信頼性0＝抑止0
            Assert.AreEqual(0f, CollectiveSecurityRules.DeterrenceValue(0f, 1f), Eps);
        }

        /// <summary>離脱の誘因＝コスト−便益（負はクランプ）＝払う分が得る分を上回ると抜けたがる。</summary>
        [Test]
        public void FreeRiderDefection_コストが便益を超えると離脱()
        {
            // コスト0.7・便益0.2＝0.5
            Assert.AreEqual(0.5f, CollectiveSecurityRules.FreeRiderDefection(0.7f, 0.2f), Eps);
            // 便益がコストを上回る＝離脱誘因なし(0)
            Assert.AreEqual(0f, CollectiveSecurityRules.FreeRiderDefection(0.2f, 0.7f), Eps);
        }

        /// <summary>体制崩壊＝信頼性が閾値(0.3)以下かつ大国離脱1件以上で死ぬ。健在なら持ちこたえる。</summary>
        [Test]
        public void SystemCollapse_信頼性低下と大国離脱の連鎖()
        {
            // 信頼性0.2≤0.3＋大国離脱1＝崩壊
            Assert.IsTrue(CollectiveSecurityRules.SystemCollapse(0.2f, 1));
            // 信頼性は低いが大国離脱0＝持ちこたえる
            Assert.IsFalse(CollectiveSecurityRules.SystemCollapse(0.2f, 0));
            // 信頼性健在(0.5)＝大国が抜けても崩壊しない
            Assert.IsFalse(CollectiveSecurityRules.SystemCollapse(0.5f, 2));
        }

        /// <summary>既定Paramsのctorクランプ＝範囲外入力が丸められる。</summary>
        [Test]
        public void Params_既定値とクランプ()
        {
            var d = CollectiveSecurityParams.Default;
            Assert.AreEqual(0.6f, d.selfCostWeight, Eps);
            Assert.AreEqual(0.4f, d.proximityWeight, Eps);
            Assert.AreEqual(0.5f, d.participationFloor, Eps);
            Assert.AreEqual(0.3f, d.credibilityErosionRate, Eps);
            Assert.AreEqual(0.05f, d.credibilityRecoveryRate, Eps);
            Assert.AreEqual(0.3f, d.collapseCredibilityThreshold, Eps);

            var clamped = new CollectiveSecurityParams(2f, -1f, 5f, -3f, -1f, 9f);
            Assert.AreEqual(1f, clamped.selfCostWeight, Eps);
            Assert.AreEqual(0f, clamped.proximityWeight, Eps);
            Assert.AreEqual(1f, clamped.participationFloor, Eps);
            Assert.AreEqual(0f, clamped.credibilityErosionRate, Eps);
            Assert.AreEqual(0f, clamped.credibilityRecoveryRate, Eps);
            Assert.AreEqual(1f, clamped.collapseCredibilityThreshold, Eps);
        }
    }
}
