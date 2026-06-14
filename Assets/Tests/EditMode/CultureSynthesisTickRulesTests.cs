using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 文化・宗教・イデオロギーの薄いオーケストレータ <see cref="CultureSynthesisTickRules"/> を固定する：
    /// 正統性/包摂が高いほど結束が年々上がり、結束→安定度modifierの符号と bounded、占領同化が
    /// integration と文化結束で動くこと、null安全 を担保。数式は委譲先（CultureRules/CivicFaithRules）で
    /// 別途固定済みのため、ここは合成・収束・符号・境界・null に集中する。
    /// </summary>
    public class CultureSynthesisTickRulesTests
    {
        // --- TickYear：正統性/包摂で結束が上がる ---
        [Test]
        public void TickYear_HighLegitimacyAndInclusiveness_RaisesCohesion()
        {
            var s = new CultureState(cohesion: 0.2f, faith: 0.2f, sincerity: 0.5f);
            // 目標＝1.0×1.0=1.0 へ収束＝結束が上がる
            CultureSynthesisTickRules.TickYear(s, legitimacy: 1f, inclusiveness: 1f, dt: 1f);
            Assert.Greater(s.cohesion, 0.2f, "高い正統性×包摂で結束は上がる");
            Assert.LessOrEqual(s.cohesion, 1f);
            // 信仰も製造で押し上げられる
            Assert.GreaterOrEqual(s.faith, 0.2f);
        }

        [Test]
        public void TickYear_LowLegitimacy_PullsCohesionDown()
        {
            var s = new CultureState(cohesion: 0.8f, faith: 0.5f, sincerity: 0.5f);
            // 目標＝0×0.5=0 へ収束＝結束が下がる（収奪・低正統性）
            CultureSynthesisTickRules.TickYear(s, legitimacy: 0f, inclusiveness: 0.5f, dt: 1f);
            Assert.Less(s.cohesion, 0.8f, "低い正統性で結束は下がる");
            Assert.GreaterOrEqual(s.cohesion, 0f);
        }

        // --- StabilityModifier：符号と bounded ---
        [Test]
        public void StabilityModifier_HighCohesion_IsPositive_AndBounded()
        {
            var s = new CultureState(cohesion: 1f, faith: 1f, sincerity: 1f);
            float m = CultureSynthesisTickRules.StabilityModifier(s);
            Assert.Greater(m, 0f, "一体な社会は安定度へ加点");
            Assert.LessOrEqual(m, CultureSynthesisTickRules.StabilityModifierScale + 1e-4f, "上限は ±Scale");
        }

        [Test]
        public void StabilityModifier_LowCohesion_IsNegative_AndBounded()
        {
            var s = new CultureState(cohesion: 0f, faith: 0f, sincerity: 0f);
            float m = CultureSynthesisTickRules.StabilityModifier(s);
            Assert.Less(m, 0f, "バラバラな社会は安定度を蝕む");
            Assert.GreaterOrEqual(m, -CultureSynthesisTickRules.StabilityModifierScale - 1e-4f, "下限は −Scale");
        }

        [Test]
        public void StabilityModifier_Midpoint_IsNearZero()
        {
            // 結束0.5・信奉0.5・真摯さ0.5 → civicCohesion=0.25、effective=(0.5+0.25)/2=0.375 < 0.5 → わずかに負
            var s = new CultureState(cohesion: 0.5f, faith: 0.5f, sincerity: 0.5f);
            float m = CultureSynthesisTickRules.StabilityModifier(s);
            // 中庸付近：Scale の範囲内でゼロ近傍（厳密ゼロは要求しない＝形骸化を割り引くため）
            Assert.Less(System.Math.Abs(m), CultureSynthesisTickRules.StabilityModifierScale);
        }

        // --- AssimilationRate：integration と文化結束で動く ---
        [Test]
        public void AssimilationRate_LowIntegrationStrongCulture_IsFaster()
        {
            var strong = new CultureState(cohesion: 1f, faith: 0.5f, sincerity: 0.5f);
            var weak = new CultureState(cohesion: 0.2f, faith: 0.5f, sincerity: 0.5f);
            float rStrong = CultureSynthesisTickRules.AssimilationRate(strong, integration: 0f);
            float rWeak = CultureSynthesisTickRules.AssimilationRate(weak, integration: 0f);
            Assert.Greater(rStrong, rWeak, "結束が強い文化ほど速く同化させる");
            Assert.GreaterOrEqual(rStrong, 0f);
        }

        [Test]
        public void AssimilationRate_FullIntegration_IsZero()
        {
            var s = new CultureState(cohesion: 1f, faith: 1f, sincerity: 1f);
            // 完全統合＝同化の余地なし
            Assert.AreEqual(0f, CultureSynthesisTickRules.AssimilationRate(s, integration: 1f), 1e-6f);
        }

        // --- null安全 ---
        [Test]
        public void NullState_IsSafe()
        {
            Assert.DoesNotThrow(() => CultureSynthesisTickRules.TickYear(null, 1f, 1f, 1f));
            Assert.AreEqual(0f, CultureSynthesisTickRules.StabilityModifier(null));
            Assert.AreEqual(0f, CultureSynthesisTickRules.AssimilationRate(null, 0f));
        }

        [Test]
        public void TickYear_ZeroOrNegativeDt_IsNoOp()
        {
            var s = new CultureState(cohesion: 0.3f, faith: 0.3f, sincerity: 0.3f);
            CultureSynthesisTickRules.TickYear(s, legitimacy: 1f, inclusiveness: 1f, dt: 0f);
            Assert.AreEqual(0.3f, s.cohesion, 1e-6f, "dt<=0 は何もしない");
        }
    }
}
