using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 「集合記憶×市民宗教」の薄いオーケストレータ <see cref="CulturalMemoryTickRules"/> を固定する：
    /// 包摂で信奉（情緒的紐帯）が年々上がり終戦からの年月が積む、紐帯→支持modifierの符号と bounded、
    /// 占領同化が integration と紐帯で動く、記憶の風化で実効開戦閾値が下がる、null安全 を担保。
    /// 数式は委譲先（CivicFaithRules/GenerationalMemoryRules）で別途固定済みのため、ここは合成・収束・符号・境界・null に集中する。
    /// </summary>
    public class CulturalMemoryTickRulesTests
    {
        // --- TickYear：包摂で信奉が上がる・年月が積む ---
        [Test]
        public void TickYear_HighInclusiveness_RaisesDevotion_AndAdvancesMemory()
        {
            var s = new CulturalMemoryState(devotion: 0.2f, sincerity: 0.5f, yearsSinceWar: 0f, warSeverity: 0.5f);
            CulturalMemoryTickRules.TickYear(s, legitimacy: 1f, inclusiveness: 1f, dt: 1f);
            Assert.Greater(s.devotion, 0.2f, "高い包摂で市民宗教の信奉は製造され上がる");
            Assert.LessOrEqual(s.devotion, 1f);
            Assert.AreEqual(1f, s.yearsSinceWar, 1e-6f, "終戦からの年月が dt ぶん積む");
        }

        // --- SupportModifier：符号と bounded ---
        [Test]
        public void SupportModifier_StrongBond_IsPositive_AndBounded()
        {
            var s = new CulturalMemoryState(devotion: 1f, sincerity: 1f, yearsSinceWar: 0f, warSeverity: 0f);
            float m = CulturalMemoryTickRules.SupportModifier(s);
            Assert.Greater(m, 0f, "厚い情緒的紐帯は民心へ加点");
            Assert.LessOrEqual(m, CulturalMemoryTickRules.SupportModifierScale + 1e-4f, "上限は ±Scale");
        }

        [Test]
        public void SupportModifier_WeakBond_IsNegative_AndBounded()
        {
            var s = new CulturalMemoryState(devotion: 0f, sincerity: 0f, yearsSinceWar: 0f, warSeverity: 0f);
            float m = CulturalMemoryTickRules.SupportModifier(s);
            Assert.Less(m, 0f, "紐帯の薄い社会は民心を失う");
            Assert.GreaterOrEqual(m, -CulturalMemoryTickRules.SupportModifierScale - 1e-4f, "下限は −Scale");
        }

        [Test]
        public void SupportModifier_HollowFaith_IsDiscounted()
        {
            // 信奉は高いが真摯さが低い（形骸化）＝実効紐帯は割り引かれ、真摯な信仰より支持が小さい
            var hollow = new CulturalMemoryState(devotion: 1f, sincerity: 0.2f, yearsSinceWar: 0f, warSeverity: 0f);
            var sincere = new CulturalMemoryState(devotion: 1f, sincerity: 1f, yearsSinceWar: 0f, warSeverity: 0f);
            Assert.Less(CulturalMemoryTickRules.SupportModifier(hollow),
                        CulturalMemoryTickRules.SupportModifier(sincere),
                        "形骸化した信仰は民心を生まない");
        }

        // --- AssimilationRate：integration と紐帯で動く ---
        [Test]
        public void AssimilationRate_LowIntegrationStrongBond_IsFaster()
        {
            var strong = new CulturalMemoryState(devotion: 1f, sincerity: 1f, yearsSinceWar: 0f, warSeverity: 0f);
            var weak = new CulturalMemoryState(devotion: 0.3f, sincerity: 0.3f, yearsSinceWar: 0f, warSeverity: 0f);
            float rStrong = CulturalMemoryTickRules.AssimilationRate(strong, integration: 0f);
            float rWeak = CulturalMemoryTickRules.AssimilationRate(weak, integration: 0f);
            Assert.Greater(rStrong, rWeak, "紐帯が強いほど速く同化させる");
            Assert.GreaterOrEqual(rStrong, 0f);
        }

        [Test]
        public void AssimilationRate_FullIntegration_IsZero()
        {
            var s = new CulturalMemoryState(devotion: 1f, sincerity: 1f, yearsSinceWar: 0f, warSeverity: 0f);
            Assert.AreEqual(0f, CulturalMemoryTickRules.AssimilationRate(s, integration: 1f), 1e-6f, "完全統合＝同化の余地なし");
        }

        // --- 記憶の風化：実効開戦閾値が下がる ---
        [Test]
        public void EffectiveWarThreshold_FadesWithTime()
        {
            var fresh = new CulturalMemoryState(devotion: 0.5f, sincerity: 0.5f, yearsSinceWar: 0f, warSeverity: 1f);
            var faded = new CulturalMemoryState(devotion: 0.5f, sincerity: 0.5f, yearsSinceWar: 120f, warSeverity: 1f);
            float baseThreshold = 100f;
            float tFresh = CulturalMemoryTickRules.EffectiveWarThreshold(fresh, baseThreshold);
            float tFaded = CulturalMemoryTickRules.EffectiveWarThreshold(faded, baseThreshold);
            Assert.Greater(tFresh, tFaded, "記憶が新しいほど開戦の敷居は高い（風化で下がる）");
            Assert.GreaterOrEqual(tFresh, baseThreshold, "記憶があるぶん基準以上");
            Assert.GreaterOrEqual(CulturalMemoryTickRules.MemoryIntensity(fresh),
                                  CulturalMemoryTickRules.MemoryIntensity(faded),
                                  "集合記憶は時間で薄れる");
        }

        // --- null安全 ---
        [Test]
        public void NullState_IsSafe()
        {
            Assert.DoesNotThrow(() => CulturalMemoryTickRules.TickYear(null, 1f, 1f, 1f));
            Assert.AreEqual(0f, CulturalMemoryTickRules.SupportModifier(null));
            Assert.AreEqual(0f, CulturalMemoryTickRules.AssimilationRate(null, 0f));
            Assert.AreEqual(0f, CulturalMemoryTickRules.MemoryIntensity(null));
            Assert.AreEqual(42f, CulturalMemoryTickRules.EffectiveWarThreshold(null, 42f), 1e-6f, "null は基準値そのまま");
        }

        [Test]
        public void TickYear_ZeroOrNegativeDt_IsNoOp()
        {
            var s = new CulturalMemoryState(devotion: 0.3f, sincerity: 0.3f, yearsSinceWar: 5f, warSeverity: 0.5f);
            CulturalMemoryTickRules.TickYear(s, legitimacy: 1f, inclusiveness: 1f, dt: 0f);
            Assert.AreEqual(0.3f, s.devotion, 1e-6f, "dt<=0 は何もしない");
            Assert.AreEqual(5f, s.yearsSinceWar, 1e-6f, "dt<=0 は年月も積まない");
        }
    }
}
