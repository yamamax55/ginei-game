using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 教育投資を固定する：学校の質は投資で上限1へ漸近・無投資で減衰、人材の質は世代遅延でゆっくり追従、
    /// 質→研究/産出ボーナス、教育負債（学校の劣化が将来の人材劣化を仕込む）判定。境界を担保。
    /// </summary>
    public class EducationRulesTests
    {
        private static readonly EducationParams P = EducationParams.Default;
        // 成長0.05/減衰0.02/世代遅延20/研究0.5/産出0.2

        [Test]
        public void SchoolQualityTick_GrowsWithInvestment()
        {
            // 投資1・質0：0.05×1×(1−0)×dt1=+0.05
            Assert.AreEqual(0.05f, EducationRules.SchoolQualityTick(0f, 1f, 1f, P), 1e-5f);
            // 上限へ漸近＝質が高いほど伸びが鈍る
            float lowGain = EducationRules.SchoolQualityTick(0.8f, 1f, 1f, P) - 0.8f;
            Assert.Less(lowGain, 0.05f);
            Assert.Greater(lowGain, 0f);
        }

        [Test]
        public void SchoolQualityTick_DecaysWithoutInvestment()
        {
            Assert.AreEqual(0.48f, EducationRules.SchoolQualityTick(0.5f, 0f, 1f, P), 1e-5f); // −0.02
            Assert.AreEqual(0f, EducationRules.SchoolQualityTick(0.01f, 0f, 1f, P), 1e-5f);   // 下限0
        }

        [Test]
        public void TalentQualityTick_LagsByGeneration()
        {
            // 遅延20：dt=1 で目標へ1/20だけ寄る＝0+(1−0)×0.05=0.05
            Assert.AreEqual(0.05f, EducationRules.TalentQualityTick(0f, 1f, 1f, P), 1e-5f);
            // dt=20（一世代）で目標へ到達
            Assert.AreEqual(1f, EducationRules.TalentQualityTick(0f, 1f, 20f, P), 1e-5f);
            // 劣化も同じく遅れて祟る
            Assert.AreEqual(0.95f, EducationRules.TalentQualityTick(1f, 0f, 1f, P), 1e-5f);
        }

        [Test]
        public void TalentQualityTick_NoLagIsImmediate()
        {
            var noLag = new EducationParams(0.05f, 0.02f, 0f, 0.5f, 0.2f);
            Assert.AreEqual(1f, EducationRules.TalentQualityTick(0f, 1f, 0.01f, noLag), 1e-5f);
        }

        [Test]
        public void Factors_ScaleWithTalent()
        {
            Assert.AreEqual(1.5f, EducationRules.ResearchFactor(1f, P), 1e-5f);
            Assert.AreEqual(1f, EducationRules.ResearchFactor(0f, P), 1e-5f);
            Assert.AreEqual(1.2f, EducationRules.OutputFactor(1f, P), 1e-5f);
            Assert.AreEqual(1.1f, EducationRules.OutputFactor(0.5f, P), 1e-5f);
        }

        [Test]
        public void HasEducationDebt_WarnsOfFutureDecline()
        {
            // 学校0.3・人材0.6＝差0.3≥0.2＝負債あり（今は回るが一世代後に枯れる）
            Assert.IsTrue(EducationRules.HasEducationDebt(0.3f, 0.6f));
            // 学校が人材に追いついている＝健全
            Assert.IsFalse(EducationRules.HasEducationDebt(0.6f, 0.6f));
            // 学校が先行（改革直後）＝負債なし
            Assert.IsFalse(EducationRules.HasEducationDebt(0.9f, 0.5f));
        }
    }
}
