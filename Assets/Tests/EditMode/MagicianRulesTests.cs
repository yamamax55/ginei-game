using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>魔術師（ヤン・ウェンリー）：逆転/不敗撤退/戦術ハック/先回り/生存/やる気/政治耐性/教師。</summary>
    public class MagicianRulesTests
    {
        [Test]
        public void Comeback_Retreat_Disruption_Ambush()
        {
            Assert.AreEqual(1.0f, MagicianRules.ComebackFactor(true, 0f), 1e-4f);
            Assert.AreEqual(1.5f, MagicianRules.ComebackFactor(true, 1f), 1e-4f);   // 絶体絶命で最大（バーミリオン）
            Assert.AreEqual(1.25f, MagicianRules.ComebackFactor(true, 0.5f), 1e-4f);
            Assert.AreEqual(1.0f, MagicianRules.ComebackFactor(false, 1f), 1e-4f);

            Assert.IsTrue(MagicianRules.GuaranteesRetreat(true));   // 不敗の撤退
            Assert.IsFalse(MagicianRules.GuaranteesRetreat(false));

            Assert.AreEqual(0.7f, MagicianRules.EnemyFormationDisruptionFactor(true), 1e-4f); // 戦術ハック
            Assert.AreEqual(1.0f, MagicianRules.EnemyFormationDisruptionFactor(false), 1e-4f);

            Assert.AreEqual(0.5f, MagicianRules.AmbushNegationFactor(true), 1e-4f); // 先回り
            Assert.AreEqual(1.0f, MagicianRules.AmbushNegationFactor(false), 1e-4f);
        }

        [Test]
        public void Survival_Growth_Politics_Mentor()
        {
            Assert.AreEqual(1.2f, MagicianRules.AllySurvivalFactor(true), 1e-4f); // ヒューベリオン
            Assert.AreEqual(1.0f, MagicianRules.AllySurvivalFactor(false), 1e-4f);

            // やる気ゼロ：平時は遅く、高難度戦で一気に伸びる。
            Assert.AreEqual(1.5f, MagicianRules.GrowthRateFactor(true, true), 1e-4f);
            Assert.AreEqual(0.5f, MagicianRules.GrowthRateFactor(true, false), 1e-4f);
            Assert.AreEqual(1.0f, MagicianRules.GrowthRateFactor(false, true), 1e-4f);

            Assert.AreEqual(0.7f, MagicianRules.PoliticalDebuffResistance(true), 1e-4f); // 民主主義の精神耐性
            Assert.AreEqual(0f, MagicianRules.PoliticalDebuffResistance(false), 1e-4f);

            Assert.IsTrue(MagicianRules.UpholdsCivilianControl(true));   // シビリアン・コントロール
            Assert.IsFalse(MagicianRules.UpholdsCivilianControl(false));

            Assert.AreEqual(2.0f, MagicianRules.MentorshipFactor(true), 1e-4f); // 偉大なる教師（ユリアン）
            Assert.AreEqual(1.0f, MagicianRules.MentorshipFactor(false), 1e-4f);
        }
    }
}
