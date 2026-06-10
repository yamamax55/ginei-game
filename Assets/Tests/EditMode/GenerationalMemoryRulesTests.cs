using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦争記憶の世代風化を固定する：集合記憶の30年半減、証人世代の線形退場、
    /// 記憶→開戦閾値の抑止、美化の反比例進行、教育保存の上限、世代間ギャップ、
    /// 「平和の長さ」による実効閾値の単調低下。境界を担保。
    /// </summary>
    public class GenerationalMemoryRulesTests
    {
        private static readonly GenerationalMemoryParams P = GenerationalMemoryParams.Default;
        // 半減期30年/閾値上乗せ0.5/美化速度0.02/教育上限0.4/ギャップ最大0.5

        [Test]
        public void MemoryIntensity_HalfLife30Years()
        {
            Assert.AreEqual(1f, GenerationalMemoryRules.MemoryIntensity(0f, 1f, P), 1e-5f);    // 終戦直後＝満額
            Assert.AreEqual(0.5f, GenerationalMemoryRules.MemoryIntensity(30f, 1f, P), 1e-5f); // 30年で半減
            Assert.AreEqual(0.25f, GenerationalMemoryRules.MemoryIntensity(60f, 1f, P), 1e-5f); // 60年で1/4
            // 酷い戦争ほど初期値が高い（苛烈さに比例）
            Assert.AreEqual(0.25f, GenerationalMemoryRules.MemoryIntensity(30f, 0.5f, P), 1e-5f);
            // 負の年数・範囲外の苛烈さはクランプ
            Assert.AreEqual(1f, GenerationalMemoryRules.MemoryIntensity(-10f, 2f, P), 1e-5f);
        }

        [Test]
        public void WitnessShare_LinearDeparture()
        {
            Assert.AreEqual(1f, GenerationalMemoryRules.WitnessShare(0f, 70f), 1e-5f);    // 全員が証人
            Assert.AreEqual(0.5f, GenerationalMemoryRules.WitnessShare(35f, 70f), 1e-5f); // 半数が退場
            Assert.AreEqual(0f, GenerationalMemoryRules.WitnessShare(70f, 70f), 1e-5f);   // 誰も知らない
            Assert.AreEqual(0f, GenerationalMemoryRules.WitnessShare(100f, 70f), 1e-5f);  // 下限クランプ
        }

        [Test]
        public void WarThresholdModifier_MemoryDeters()
        {
            Assert.AreEqual(1.5f, GenerationalMemoryRules.WarThresholdModifier(1f, P), 1e-5f);  // 記憶最大＝最も慎重
            Assert.AreEqual(1.25f, GenerationalMemoryRules.WarThresholdModifier(0.5f, P), 1e-5f);
            Assert.AreEqual(1f, GenerationalMemoryRules.WarThresholdModifier(0f, P), 1e-5f);    // 忘却＝抑止消失
        }

        [Test]
        public void RomanticizationTick_InverseToMemory()
        {
            // 記憶が満ちている間は美化が進まない
            Assert.AreEqual(0f, GenerationalMemoryRules.RomanticizationTick(0f, 1f, 10f, P), 1e-5f);
            // 記憶ゼロなら満速で進む：0.02×10年＝0.2
            Assert.AreEqual(0.2f, GenerationalMemoryRules.RomanticizationTick(0f, 0f, 10f, P), 1e-5f);
            // 反比例＝記憶が薄いほど美化が速い
            float strongMemory = GenerationalMemoryRules.RomanticizationTick(0f, 0.8f, 10f, P);
            float fadedMemory = GenerationalMemoryRules.RomanticizationTick(0f, 0.2f, 10f, P);
            Assert.Greater(fadedMemory, strongMemory);
            // 上限1でクランプ
            Assert.AreEqual(1f, GenerationalMemoryRules.RomanticizationTick(0.95f, 0f, 10f, P), 1e-5f);
        }

        [Test]
        public void EducationPreservation_CappedFloor()
        {
            // 教育全力でも上限0.4＝体験の代わりにはならない
            Assert.AreEqual(0.4f, GenerationalMemoryRules.EducationPreservation(0.1f, 1f, P), 1e-5f);
            // 生きた記憶が上限を超えている間は何も足さない
            Assert.AreEqual(0.9f, GenerationalMemoryRules.EducationPreservation(0.9f, 1f, P), 1e-5f);
            // 努力ゼロ＝素の記憶のまま
            Assert.AreEqual(0.1f, GenerationalMemoryRules.EducationPreservation(0.1f, 0f, P), 1e-5f);
            // 半分の努力＝下限0.2
            Assert.AreEqual(0.2f, GenerationalMemoryRules.EducationPreservation(0.1f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void HawkishGenerationGap_UnknowingAreBold()
        {
            Assert.AreEqual(0f, GenerationalMemoryRules.HawkishGenerationGap(1f, P), 1e-5f);    // 全員が証人＝ギャップなし
            Assert.AreEqual(0.25f, GenerationalMemoryRules.HawkishGenerationGap(0.5f, P), 1e-5f);
            Assert.AreEqual(0.5f, GenerationalMemoryRules.HawkishGenerationGap(0f, P), 1e-5f);  // 誰も知らない＝最大
        }

        [Test]
        public void EffectiveWarThreshold_PeaceErodesDeterrence()
        {
            // 終戦直後：基準100×1.5＝150
            Assert.AreEqual(150f, GenerationalMemoryRules.EffectiveWarThreshold(100f, 0f, 1f, P), 1e-3f);
            // 30年後：100×1.25＝125
            Assert.AreEqual(125f, GenerationalMemoryRules.EffectiveWarThreshold(100f, 30f, 1f, P), 1e-3f);
            // 「平和の最大の敵は平和の長さ」＝年月とともに単調に下がり基準値へ漸近（基準値は割らない）
            float t0 = GenerationalMemoryRules.EffectiveWarThreshold(100f, 0f, 1f, P);
            float t60 = GenerationalMemoryRules.EffectiveWarThreshold(100f, 60f, 1f, P);
            float t300 = GenerationalMemoryRules.EffectiveWarThreshold(100f, 300f, 1f, P);
            Assert.Greater(t0, t60);
            Assert.Greater(t60, t300);
            Assert.GreaterOrEqual(t300, 100f);
        }
    }
}
