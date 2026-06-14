using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 研究プログラムの薄いオーケストレータ <see cref="ResearchProgramRules"/> を固定する：
    /// 資金×技能で progress が増える／閾値で techLevel↑（超過は繰り越し）／後発勢力は拡散で速い／
    /// 先進でない・接触ゼロは漏れない／革新の波で景気倍率が動く／null・境界の安全。決定論。
    /// </summary>
    public class ResearchProgramRulesTests
    {
        // ===== TickYear（資金×技能で progress が増える） =====

        [Test]
        public void TickYear_FundingAndSkillAdvanceProgress()
        {
            var rs = new ResearchState(0f, 100f);
            // 予算10×技能1.0（効率1.0）×基準1.0 = 産出10、dt=1 で progress=10。
            ResearchProgramRules.TickYear(rs, 10f, 1f, 1f);
            Assert.AreEqual(10f, rs.progress, 1e-3f);
            Assert.AreEqual(0f, rs.techLevel, 1e-3f);
        }

        [Test]
        public void TickYear_HigherSkillResearchesFaster()
        {
            var lowSkill = new ResearchState(0f, 100f);
            var highSkill = new ResearchState(0f, 100f);
            ResearchProgramRules.TickYear(lowSkill, 10f, 0f, 1f);   // 技能0＝基礎効率のみ
            ResearchProgramRules.TickYear(highSkill, 10f, 1f, 1f);  // 技能1.0＝満額
            Assert.Greater(highSkill.progress, lowSkill.progress);
            Assert.Greater(lowSkill.progress, 0f); // 技能ゼロでも止まらない
        }

        // ===== 閾値で techLevel↑（超過は繰り越し） =====

        [Test]
        public void TickYear_CrossesThresholdAndCarriesRemainder()
        {
            var rs = new ResearchState(0f, 5f);
            // 産出 = 10/dt、dt=1 で progress 10 → 5 で1段上がり、超過5を繰り越し → 残5でもう1段。
            ResearchProgramRules.TickYear(rs, 10f, 1f, 1f);
            Assert.AreEqual(2f, rs.techLevel, 1e-3f); // 5 と 5 で2段
            Assert.AreEqual(0f, rs.progress, 1e-3f);
        }

        [Test]
        public void TickYear_NullAndNonPositiveDtAreSafe()
        {
            Assert.DoesNotThrow(() => ResearchProgramRules.TickYear(null, 10f, 1f, 1f));
            var rs = new ResearchState(0f, 100f);
            ResearchProgramRules.TickYear(rs, 10f, 1f, 0f);   // dt=0 は無視
            ResearchProgramRules.TickYear(rs, 10f, 1f, -1f);  // 負 dt は無視
            Assert.AreEqual(0f, rs.progress, 1e-4f);
        }

        // ===== DiffusionGain（後発者利益） =====

        [Test]
        public void DiffusionGain_LaggardGainsFromLeader()
        {
            // 後発(own=2) が先進(leader=10) に接触(openness=1) → 正のゲイン。
            float gain = ResearchProgramRules.DiffusionGain(2f, 10f, 1f, 1f);
            Assert.Greater(gain, 0f);
        }

        [Test]
        public void DiffusionGain_LargerGapMovesFaster()
        {
            // 格差が大きいほど一歩が大きい（後発者利益）。
            float far = ResearchProgramRules.DiffusionGain(1f, 10f, 1f, 1f);
            float near = ResearchProgramRules.DiffusionGain(8f, 10f, 1f, 1f);
            Assert.Greater(far, near);
        }

        [Test]
        public void DiffusionGain_NoLeadOrNoContactGivesZero()
        {
            Assert.AreEqual(0f, ResearchProgramRules.DiffusionGain(10f, 10f, 1f, 1f), 1e-4f); // 並走＝漏れない
            Assert.AreEqual(0f, ResearchProgramRules.DiffusionGain(12f, 10f, 1f, 1f), 1e-4f); // 自勢力が先進
            Assert.AreEqual(0f, ResearchProgramRules.DiffusionGain(2f, 10f, 0f, 1f), 1e-4f);  // 接触ゼロ
            Assert.AreEqual(0f, ResearchProgramRules.DiffusionGain(2f, 10f, 1f, 0f), 1e-4f);  // dt=0
        }

        // ===== InnovationWaveFactor（景気倍率） =====

        [Test]
        public void InnovationWaveFactor_BoomOutpacesDepression()
        {
            // 繁栄期（波位置0.45・既定閾値で繁栄）は不況期（0.9）より研究が加速する。
            float boom = ResearchProgramRules.InnovationWaveFactor(0.45f, 0.8f);
            float depression = ResearchProgramRules.InnovationWaveFactor(0.9f, 0.8f);
            Assert.Greater(boom, depression);
            // 既定 floor..floor+span = 0.5..1.5 の範囲に収まる。
            Assert.GreaterOrEqual(boom, 0.5f);
            Assert.LessOrEqual(boom, 1.5f);
        }
    }
}
