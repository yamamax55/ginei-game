using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 師弟（メルカッツ型）を固定する：教える力は技量×忍耐（強い武人≠良い師）、伝授は弟子の経験獲得を
    /// 加速（階段）、師の下で届く技量には上限（天井）、長居は型に嵌まり独創性が摩耗、師の死の遺産は
    /// 準備度で飛躍にも崩壊にもなる（二面性）。境界とクランプを担保。
    /// </summary>
    public class MentorshipRulesTests
    {
        private static readonly MentorshipParams P = MentorshipParams.Default;
        // 忍耐下限0.3/伝授最大+1.0/才能下限0.5/上限比0.9/独り立ち10年/摩耗0.05/年/摩耗上限0.5/分水嶺0.5/遺産スケール1

        [Test]
        public void TeachingQuality_NeedsBothSkillAndPatience()
        {
            // 技量100×忍耐1＝満点の師
            Assert.AreEqual(1f, MentorshipRules.TeachingQuality(100f, 1f, P), 1e-5f);
            // 技量100でも忍耐0＝0.3止まり（強い武人が良い師とは限らない）
            Assert.AreEqual(0.3f, MentorshipRules.TeachingQuality(100f, 0f, P), 1e-5f);
            // 技量50×忍耐1＝0.5（忍耐があっても技量が無ければ教えられない）
            Assert.AreEqual(0.5f, MentorshipRules.TeachingQuality(50f, 1f, P), 1e-5f);
            // 範囲外入力はクランプ
            Assert.AreEqual(0f, MentorshipRules.TeachingQuality(-10f, 2f, P), 1e-5f);
        }

        [Test]
        public void LearningMultiplier_AcceleratesGrowth()
        {
            // 質1×才能1＝最大2倍（GrowthRules.GainExperience の amount に掛ける想定）
            Assert.AreEqual(2f, MentorshipRules.LearningMultiplier(1f, 1f, P), 1e-5f);
            // 才能0でも下限0.5は受け取れる＝1.5倍
            Assert.AreEqual(1.5f, MentorshipRules.LearningMultiplier(1f, 0f, P), 1e-5f);
            // 師がいなければ等倍＝従来動作（後方互換）
            Assert.AreEqual(1f, MentorshipRules.LearningMultiplier(0f, 1f, P), 1e-5f);
        }

        [Test]
        public void SkillCeiling_MentorIsTheCeiling()
        {
            // 師80→弟子は72まで＝師の型をなぞる限り師には届かない
            Assert.AreEqual(72f, MentorshipRules.SkillCeiling(80f, P), 1e-5f);
            // 最高の師でも90まで＝独り立ちしなければ100には届かない
            Assert.AreEqual(90f, MentorshipRules.SkillCeiling(100f, P), 1e-5f);
            // 範囲外入力はクランプ
            Assert.AreEqual(90f, MentorshipRules.SkillCeiling(150f, P), 1e-5f);
            Assert.AreEqual(0f, MentorshipRules.SkillCeiling(-5f, P), 1e-5f);
        }

        [Test]
        public void IndependenceTest_CeilingReachedOrOverstayed()
        {
            // 天井（師80→72）に達した＝もう学ぶものがない＝独り立ちの時
            Assert.IsTrue(MentorshipRules.IndependenceTest(72f, 80f, 3f, P));
            // 天井未満かつ在籍5年＝まだ学ぶことがある
            Assert.IsFalse(MentorshipRules.IndependenceTest(50f, 80f, 5f, P));
            // 天井未満でも在籍10年＝長居は型に嵌まる＝出るべき時
            Assert.IsTrue(MentorshipRules.IndependenceTest(50f, 80f, 10f, P));
        }

        [Test]
        public void OverstayPenalty_ErodesOriginalityAfterGrace()
        {
            // 10年までは無傷
            Assert.AreEqual(0f, MentorshipRules.OverstayPenalty(0f, P), 1e-5f);
            Assert.AreEqual(0f, MentorshipRules.OverstayPenalty(10f, P), 1e-5f);
            // 超過4年＝0.05×4＝0.2 の摩耗
            Assert.AreEqual(0.2f, MentorshipRules.OverstayPenalty(14f, P), 1e-5f);
            // 上限0.5で頭打ち（どれほど長居しても潰れきりはしない）
            Assert.AreEqual(0.5f, MentorshipRules.OverstayPenalty(30f, P), 1e-5f);
        }

        [Test]
        public void LegacyOnMentorDeath_TwoSided()
        {
            // 準備のできた弟子（readiness=1）＝師の死で飛躍（+0.5＝遺志を継ぐ）
            Assert.AreEqual(0.5f, MentorshipRules.LegacyOnMentorDeath(1f, 1f, P), 1e-5f);
            // 未熟な弟子（readiness=0）＝支えを失って崩れる（−0.5）＝二面性
            Assert.AreEqual(-0.5f, MentorshipRules.LegacyOnMentorDeath(1f, 0f, P), 1e-5f);
            // 分水嶺ちょうど＝差し引きゼロ
            Assert.AreEqual(0f, MentorshipRules.LegacyOnMentorDeath(1f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void LegacyOnMentorDeath_BondScalesTheSwing()
        {
            // 絆0＝何も残らない（喪失も飛躍もない）
            Assert.AreEqual(0f, MentorshipRules.LegacyOnMentorDeath(0f, 1f, P), 1e-5f);
            // 絆0.5＝振れ幅も半分（+0.25／−0.25）
            Assert.AreEqual(0.25f, MentorshipRules.LegacyOnMentorDeath(0.5f, 1f, P), 1e-5f);
            Assert.AreEqual(-0.25f, MentorshipRules.LegacyOnMentorDeath(0.5f, 0f, P), 1e-5f);
            // 範囲外入力はクランプ（絆2→1扱い）
            Assert.AreEqual(0.5f, MentorshipRules.LegacyOnMentorDeath(2f, 1.5f, P), 1e-5f);
        }
    }
}
