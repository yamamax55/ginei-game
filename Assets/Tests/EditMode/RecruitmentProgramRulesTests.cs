using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 徴兵・動員・練度オーケストレータ：徴募源×動員率で新兵増／訓練質で戦力増／プール0で0／null安全。
    /// 数式は委譲先（MobilizationRules/SkillEffectRules/RecruitTrainingRules/VeterancyRules）で担保し、
    /// ここは合成（橋渡し）の結合だけを固定する。
    /// </summary>
    public class RecruitmentProgramRulesTests
    {
        [Test]
        public void AnnualRecruits_PoolTimesRateTimesDt()
        {
            // MobilizedPool(100,0.2)=20 ×dt1 = 20
            Assert.AreEqual(20f, RecruitmentProgramRules.AnnualRecruits(100f, 0.2f, 1f), 1e-4f);
            // dt=0.5 で半分
            Assert.AreEqual(10f, RecruitmentProgramRules.AnnualRecruits(100f, 0.2f, 0.5f), 1e-4f);
            // 動員率が高いほど新兵が増える
            Assert.Greater(
                RecruitmentProgramRules.AnnualRecruits(100f, 0.5f),
                RecruitmentProgramRules.AnnualRecruits(100f, 0.2f));
        }

        [Test]
        public void AnnualRecruits_ZeroPoolOrRateOrDt_Zero()
        {
            Assert.AreEqual(0f, RecruitmentProgramRules.AnnualRecruits(0f, 0.5f, 1f), 1e-4f);   // 徴募源0
            Assert.AreEqual(0f, RecruitmentProgramRules.AnnualRecruits(100f, 0f, 1f), 1e-4f);   // 動員率0
            Assert.AreEqual(0f, RecruitmentProgramRules.AnnualRecruits(100f, 0.5f, 0f), 1e-4f); // 経過0
            // 負値は0へクランプ（null安全＝引数防御）
            Assert.AreEqual(0f, RecruitmentProgramRules.AnnualRecruits(-50f, 0.5f, 1f), 1e-4f);
            Assert.AreEqual(0f, RecruitmentProgramRules.AnnualRecruits(100f, 0.5f, -1f), 1e-4f);
        }

        [Test]
        public void TrainedStrength_QualityIncreasesStrength()
        {
            // MilitaryQuality(q,1)=0.6+0.4q ：質0で0.6倍、質1で1.0倍
            Assert.AreEqual(12f, RecruitmentProgramRules.TrainedStrength(20f, 0f), 1e-4f);
            Assert.AreEqual(20f, RecruitmentProgramRules.TrainedStrength(20f, 1f), 1e-4f);
            // 訓練質が高いほど戦力が大きい（質 vs 量）
            Assert.Greater(
                RecruitmentProgramRules.TrainedStrength(20f, 0.9f),
                RecruitmentProgramRules.TrainedStrength(20f, 0.2f));
        }

        [Test]
        public void TrainedStrength_ZeroRecruits_Zero()
        {
            Assert.AreEqual(0f, RecruitmentProgramRules.TrainedStrength(0f, 1f), 1e-4f);
            Assert.AreEqual(0f, RecruitmentProgramRules.TrainedStrength(-10f, 1f), 1e-4f);
        }

        [Test]
        public void TrainedStrengthFromDepot_NullOrEmptyPool_Zero()
        {
            Assert.AreEqual(0f, RecruitmentProgramRules.TrainedStrengthFromDepot(null, 1000f, 0.5f), 1e-4f);
            var depot = new RecruitDepot(1, Faction.帝国, 200, 0.5f, 0.5f);
            Assert.AreEqual(0f, RecruitmentProgramRules.TrainedStrengthFromDepot(depot, 0f, 0.5f), 1e-4f);
            // 徴募源があれば正の戦力（修了者×練度）
            Assert.Greater(RecruitmentProgramRules.TrainedStrengthFromDepot(depot, 1000f, 0.2f), 0f);
        }

        [Test]
        public void VeterancyGain_AccumulatesWithIntensity()
        {
            // GainFromBattle(0,1)=xpPerBattle、激戦ほど多く積む
            float light = RecruitmentProgramRules.VeterancyGain(0f, 0.2f);
            float heavy = RecruitmentProgramRules.VeterancyGain(0f, 1f);
            Assert.Greater(heavy, light);
            Assert.AreEqual(VeterancyRules.GainFromBattle(5f, 0.5f), RecruitmentProgramRules.VeterancyGain(5f, 0.5f), 1e-4f);
        }
    }
}
