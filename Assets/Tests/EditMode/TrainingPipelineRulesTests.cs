using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>訓練パイプライン（兵卒の新兵を戦力化する流れ）の純ロジック検証。既定 Params で期待値固定。</summary>
    public class TrainingPipelineRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void TrainingThroughput_LimitedByInstructors()
        {
            // 教官5人×20=100 で律速（入校200は捌けない）
            Assert.AreEqual(100f, TrainingPipelineRules.TrainingThroughput(5f, 200f), Eps);
            // 教官が十分なら入校数で律速
            Assert.AreEqual(200f, TrainingPipelineRules.TrainingThroughput(20f, 200f), Eps);
        }

        [Test]
        public void RushTrainingPenalty_ZeroAtStandard_HalfDurationPenalized()
        {
            // 標準30日に到達＝ペナルティなし
            Assert.AreEqual(0f, TrainingPipelineRules.RushTrainingPenalty(30f), Eps);
            // 15日＝不足率0.5×scale0.6＝0.3
            Assert.AreEqual(0.3f, TrainingPipelineRules.RushTrainingPenalty(15f), Eps);
        }

        [Test]
        public void TraineeQuality_AppliesRushPenalty()
        {
            // 標準期間＝質そのまま
            Assert.AreEqual(0.8f, TrainingPipelineRules.TraineeQuality(30f, 0.8f), Eps);
            // 半分の期間＝0.8×(1-0.3)=0.56
            Assert.AreEqual(0.56f, TrainingPipelineRules.TraineeQuality(15f, 0.8f), Eps);
        }

        [Test]
        public void GraduationRate_RatioClampedToOne()
        {
            Assert.AreEqual(0.5f, TrainingPipelineRules.GraduationRate(100f, 200f), Eps);
            Assert.AreEqual(1f, TrainingPipelineRules.GraduationRate(300f, 200f), Eps);
            Assert.AreEqual(0f, TrainingPipelineRules.GraduationRate(50f, 0f), Eps); // 入校0
        }

        [Test]
        public void CombatExperienceGain_RaisesQualityAndClamps()
        {
            // 生き残った3会戦＝0.5+0.3=0.8
            Assert.AreEqual(0.8f, TrainingPipelineRules.CombatExperienceGain(3, 0.5f), Eps);
            // 多数の会戦でも上限1.0
            Assert.AreEqual(1f, TrainingPipelineRules.CombatExperienceGain(10, 0.5f), Eps);
            // 負数は0扱い
            Assert.AreEqual(0.5f, TrainingPipelineRules.CombatExperienceGain(-3, 0.5f), Eps);
        }

        [Test]
        public void InstructorDrain_RemainingRatio()
        {
            // 10人中4人を前線へ＝残り6/10=0.6
            Assert.AreEqual(0.6f, TrainingPipelineRules.InstructorDrain(4f, 10f), Eps);
            // 全員前線＝0、総教官0＝0
            Assert.AreEqual(0f, TrainingPipelineRules.InstructorDrain(10f, 10f), Eps);
            Assert.AreEqual(0f, TrainingPipelineRules.InstructorDrain(0f, 0f), Eps);
        }

        [Test]
        public void PipelineCapacity_LimitedBySmallerOfInstructorsAndFacility()
        {
            // 教官5×20=100 と 施設1×100=100＝100
            Assert.AreEqual(100f, TrainingPipelineRules.PipelineCapacity(5f, 1f), Eps);
            // 教官2×20=40（施設5×100=500より小）＝40で律速
            Assert.AreEqual(40f, TrainingPipelineRules.PipelineCapacity(2f, 5f), Eps);
        }

        [Test]
        public void IsFullyTrained_ThresholdBoundary()
        {
            Assert.IsTrue(TrainingPipelineRules.IsFullyTrained(0.8f));   // 既定しきい0.6以上
            Assert.IsFalse(TrainingPipelineRules.IsFullyTrained(0.5f));
            Assert.IsTrue(TrainingPipelineRules.IsFullyTrained(0.6f));   // 境界＝以上で真
        }

        [Test]
        public void Narrative_RushedRecruitsThenSeasonedByBattle()
        {
            // 物語：教官不足の中、急造で新兵を前線へ → 練度不足 → だが生き残って実戦で鍛えられる。
            var p = TrainingPipelineParams.Default;

            // 教官3人で入校150名 → 60名しか捌けない（3×20=60）
            float throughput = TrainingPipelineRules.TrainingThroughput(3f, 150f, p);
            Assert.AreEqual(60f, throughput, Eps);

            // 戦力化できたのは150中60＝40%
            float gradRate = TrainingPipelineRules.GraduationRate(throughput, 150f);
            Assert.AreEqual(0.4f, gradRate, Eps);

            // 標準30日の半分(15日)で送り出した急造練度＝質0.7×(1-0.3)=0.49
            float rushedQuality = TrainingPipelineRules.TraineeQuality(15f, 0.7f, p);
            Assert.AreEqual(0.49f, rushedQuality, Eps);
            // この段階ではまだ戦力化基準(0.6)に届かない
            Assert.IsFalse(TrainingPipelineRules.IsFullyTrained(rushedQuality));

            // しかし2会戦を生き延びて経験を積む＝0.49+0.2=0.69
            float seasoned = TrainingPipelineRules.CombatExperienceGain(2, rushedQuality, p);
            Assert.AreEqual(0.69f, seasoned, Eps);
            // 実戦を経て十分な練度に達した
            Assert.IsTrue(TrainingPipelineRules.IsFullyTrained(seasoned));
        }
    }
}
