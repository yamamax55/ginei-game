using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 猟官制を固定する：買えた忠誠（配るほど固い）、行政能力（縁故者の腕前次第）、実力者流出
    /// （閾値超で加速）、論功行賞の期待圧力（支持者＞ポストの不満）、改革抵抗（既得年数で固まる）、
    /// 政権損益＝「忠誠は買えるが有能は買えない」。境界とクランプを担保。
    /// </summary>
    public class PatronageRulesTests
    {
        private static readonly PatronageParams P = PatronageParams.Default;
        // 忠誠0.6/劣化0.5/流出閾値0.5・緩傾斜0.2・最大0.8/改革抵抗0.9・既得20年

        [Test]
        public void LoyaltyPurchased_ScalesWithShare()
        {
            // 配るほど政権基盤は固い（全ポスト猟官化で最大0.6）
            Assert.AreEqual(0.6f, PatronageRules.LoyaltyPurchased(1f, P), 1e-5f);
            Assert.AreEqual(0.3f, PatronageRules.LoyaltyPurchased(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, PatronageRules.LoyaltyPurchased(0f, P), 1e-5f);
            // 入力クランプ＝1超でも上限止まり
            Assert.AreEqual(0.6f, PatronageRules.LoyaltyPurchased(2f, P), 1e-5f);
        }

        [Test]
        public void AdministrativeQuality_DependsOnLoyalistCompetence()
        {
            // 無能な取り巻きで全ポストを埋める＝行政能力は最大劣化（0.5）
            Assert.AreEqual(0.5f, PatronageRules.AdministrativeQuality(1f, 0f, P), 1e-5f);
            // 有能な子分なら劣化なし（腕前1＝無傷）
            Assert.AreEqual(1f, PatronageRules.AdministrativeQuality(1f, 1f, P), 1e-5f);
            // 猟官ゼロなら腕前に関わらず満全
            Assert.AreEqual(1f, PatronageRules.AdministrativeQuality(0f, 0f, P), 1e-5f);
            Assert.AreEqual(0.875f, PatronageRules.AdministrativeQuality(0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void MeritocracyExodus_AcceleratesAboveThreshold()
        {
            // 閾値（0.5）以下は緩傾斜：才能はまだ我慢する
            Assert.AreEqual(0.05f, PatronageRules.MeritocracyExodus(0.25f, P), 1e-5f);
            Assert.AreEqual(0.1f, PatronageRules.MeritocracyExodus(0.5f, P), 1e-5f);
            // 閾値超で加速し、全面猟官化で最大0.8（見切りの雪崩）
            Assert.AreEqual(0.45f, PatronageRules.MeritocracyExodus(0.75f, P), 1e-5f);
            Assert.AreEqual(0.8f, PatronageRules.MeritocracyExodus(1f, P), 1e-5f);
            // 閾値の上の限界流出 > 閾値の下の限界流出（加速の担保）
            float below = PatronageRules.MeritocracyExodus(0.5f, P) - PatronageRules.MeritocracyExodus(0.4f, P);
            float above = PatronageRules.MeritocracyExodus(0.6f, P) - PatronageRules.MeritocracyExodus(0.5f, P);
            Assert.Greater(above, below);
        }

        [Test]
        public void SpoilsExpectation_UnrewardedSupportersResent()
        {
            // 支持者100にポスト30＝7割が報われない＝勝っても恨まれる
            Assert.AreEqual(0.7f, PatronageRules.SpoilsExpectation(100, 30), 1e-5f);
            // 全員に配れれば不満なし（ポスト余剰も同じ）
            Assert.AreEqual(0f, PatronageRules.SpoilsExpectation(10, 10), 1e-5f);
            Assert.AreEqual(0f, PatronageRules.SpoilsExpectation(10, 20), 1e-5f);
            // 支持者ゼロ・負数は圧力なし（クランプ）
            Assert.AreEqual(0f, PatronageRules.SpoilsExpectation(0, 5), 1e-5f);
            Assert.AreEqual(0f, PatronageRules.SpoilsExpectation(-3, -1), 1e-5f);
        }

        [Test]
        public void ReformResistance_HardensWithEntrenchment()
        {
            // 20年染み付いた全面猟官制＝抵抗最大0.9（試験制度を殺しに来る）
            Assert.AreEqual(0.9f, PatronageRules.ReformResistance(1f, 20f, P), 1e-5f);
            // 出来たての猟官制でも半分は抵抗する
            Assert.AreEqual(0.45f, PatronageRules.ReformResistance(1f, 0f, P), 1e-5f);
            // 猟官ゼロなら抵抗する者がいない
            Assert.AreEqual(0f, PatronageRules.ReformResistance(0f, 20f, P), 1e-5f);
            // 年数は上限でクランプ（40年でも0.9止まり）
            Assert.AreEqual(0.9f, PatronageRules.ReformResistance(1f, 40f, P), 1e-5f);
            Assert.AreEqual(0.3375f, PatronageRules.ReformResistance(0.5f, 10f, P), 1e-5f);
        }

        [Test]
        public void NetRegimeValue_LoyaltyCanBeBoughtCompetenceCannot()
        {
            // 無能な取り巻きの全面猟官化＝0.6−0.5−0.8＝−0.7（政権の自殺）
            Assert.AreEqual(-0.7f, PatronageRules.NetRegimeValue(1f, 0f, P), 1e-5f);
            // 縁故者が天才揃いでも全面猟官化は流出で赤字＝0.6−0−0.8＝−0.2
            // ＝忠誠は買えるが有能は買えない
            Assert.AreEqual(-0.2f, PatronageRules.NetRegimeValue(1f, 1f, P), 1e-5f);
            // 有能な子分への控えめな配分（3割・腕前0.5）だけがわずかに引き合う
            // ＝0.18−0.075−0.06＝+0.045
            Assert.AreEqual(0.045f, PatronageRules.NetRegimeValue(0.3f, 0.5f, P), 1e-5f);
            // 配らなければ損も得もない
            Assert.AreEqual(0f, PatronageRules.NetRegimeValue(0f, 0f, P), 1e-5f);
        }

        [Test]
        public void Params_CtorClampsInputs()
        {
            var p = new PatronageParams(2f, -1f, 1.5f, -0.2f, 9f, 9f, 0f);
            Assert.AreEqual(1f, p.loyaltyGainScale, 1e-5f);
            Assert.AreEqual(0f, p.qualityLossScale, 1e-5f);
            Assert.AreEqual(0.99f, p.exodusThreshold, 1e-5f);
            Assert.AreEqual(0f, p.exodusBaseRate, 1e-5f);
            Assert.AreEqual(1f, p.exodusMax, 1e-5f);
            Assert.AreEqual(1f, p.reformResistanceMax, 1e-5f);
            Assert.AreEqual(1f, p.entrenchYearsToMax, 1e-5f);
        }
    }
}
