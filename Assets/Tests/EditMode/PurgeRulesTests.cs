using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 粛清を固定する：統制利得（規模×敵対残存）、人材毀損・恐怖の萎縮（規模比例）、冤罪率
    /// （証拠の質が下げる）、表面忠誠と本音の乖離、純効果＝小規模精密だけが引き合う。境界を担保。
    /// </summary>
    public class PurgeRulesTests
    {
        private static readonly PurgeParams P = PurgeParams.Default;
        // 統制0.5/人材0.4/萎縮0.5/冤罪0.7

        [Test]
        public void ControlGain_NeedsOpposition()
        {
            Assert.AreEqual(0.5f, PurgeRules.ControlGain(1f, 1f, P), 1e-5f);
            // 敵対派閥が既にいなければ粛清しても統制は増えない
            Assert.AreEqual(0f, PurgeRules.ControlGain(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void Costs_ScaleWithPurgeSize()
        {
            Assert.AreEqual(0.4f, PurgeRules.TalentLoss(1f, P), 1e-5f);
            Assert.AreEqual(0.2f, PurgeRules.TalentLoss(0.5f, P), 1e-5f);
            Assert.AreEqual(0.5f, PurgeRules.FearParalysis(1f, P), 1e-5f);
            Assert.AreEqual(0f, PurgeRules.FearParalysis(0f, P), 1e-5f);
        }

        [Test]
        public void FalsePositiveRatio_EvidenceQualityMatters()
        {
            // 雑な大粛清＝冤罪0.7
            Assert.AreEqual(0.7f, PurgeRules.FalsePositiveRatio(1f, 0f, P), 1e-5f);
            // 固い証拠の摘発＝冤罪なし
            Assert.AreEqual(0f, PurgeRules.FalsePositiveRatio(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0.175f, PurgeRules.FalsePositiveRatio(0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void SurvivorLoyalty_ProfessedVsReal()
        {
            // 大粛清：表面忠誠は満点に近づくが、乖離（面従腹背）も最大
            float professed = PurgeRules.SurvivorProfessedLoyalty(1f, 0f, P, out float falsification);
            Assert.AreEqual(1f, professed, 1e-5f);
            Assert.AreEqual(0.7f, falsification, 1e-5f);
            // 粛清なし＝中立の表明・乖離なし
            float calm = PurgeRules.SurvivorProfessedLoyalty(0f, 1f, P, out float honest);
            Assert.AreEqual(0.5f, calm, 1e-5f);
            Assert.AreEqual(0f, honest, 1e-5f);
        }

        [Test]
        public void NetEffect_OnlySmallPrecisePurgesPay()
        {
            // 全面粛清は強い敵がいても割に合わない：0.5−0.4−0.5=−0.4
            Assert.AreEqual(-0.4f, PurgeRules.NetEffect(1f, 1f, P), 1e-5f);
            // 小規模（0.2）×強敵（1.0）＝0.1−0.08−0.1=−0.08…それでも負＝粛清は基本高くつく
            Assert.Less(PurgeRules.NetEffect(0.2f, 1f, P), 0f);
            // 何もしない＝ゼロが基準
            Assert.AreEqual(0f, PurgeRules.NetEffect(0f, 1f, P), 1e-5f);
        }
    }
}
