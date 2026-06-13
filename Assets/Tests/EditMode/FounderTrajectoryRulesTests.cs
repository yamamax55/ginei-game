using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 建国者の自己廃絶テスト（#1493・マキャヴェッリ『ディスコルシ』ロムルス型）の純ロジック検証。
    /// 既定Params（制度投資0.5・権力集中0.5・軌道閾値1.0・移譲閾値0.5・罠係数1.0）で期待値を固定。
    /// </summary>
    public class FounderTrajectoryRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>制度投資tick＝努力×制度投資傾き×dt が制度を育てる（0.2 + 0.8×0.5×1 = 0.6）。</summary>
        [Test]
        public void InstitutionInvestmentTick_GrowsInstitution()
        {
            float v = FounderTrajectoryRules.InstitutionInvestmentTick(0.2f, 0.8f, 1f);
            Assert.AreEqual(0.6f, v, Eps);
        }

        /// <summary>権力集中tick＝野心×権力集中傾き×dt が個人権力を膨らます（0.1 + 1.0×0.5×2 = 1.0でクランプ）。</summary>
        [Test]
        public void PowerConcentrationTick_GrowsPersonalPower()
        {
            float v = FounderTrajectoryRules.PowerConcentrationTick(0.1f, 1f, 2f);
            Assert.AreEqual(1f, v, Eps); // 0.1+1.0=1.1 → クランプ1.0
        }

        /// <summary>軌道バランス＝制度／個人権力の比。制度が勝てば&gt;1（共和制側）、個人が勝てば&lt;1（専制側）。</summary>
        [Test]
        public void TrajectoryBalance_RatioOfInstitutionToPower()
        {
            Assert.AreEqual(2f, FounderTrajectoryRules.TrajectoryBalance(0.8f, 0.4f), Eps); // 制度優位
            Assert.AreEqual(0.5f, FounderTrajectoryRules.TrajectoryBalance(0.3f, 0.6f), Eps); // 権力優位
            // 個人権力ゼロ＝制度のみ（共和制側）
            Assert.AreEqual(float.MaxValue, FounderTrajectoryRules.TrajectoryBalance(0.5f, 0f));
        }

        /// <summary>自己廃絶テスト＝個人権力×自発的移譲。握った権力を進んで返すほど高い（0.8×1.0=0.8）。</summary>
        [Test]
        public void SelfAbnegationTest_PowerTimesHandover()
        {
            Assert.AreEqual(0.8f, FounderTrajectoryRules.SelfAbnegationTest(0.8f, 1f), Eps);
            // 権力を握っても手放さなければゼロ＝試金石に落ちる
            Assert.AreEqual(0f, FounderTrajectoryRules.SelfAbnegationTest(0.9f, 0f), Eps);
        }

        /// <summary>結末の弁別＝制度優位＋自己廃絶で共和制軌道、権力優位＋固執で専制固定、中間は過渡。</summary>
        [Test]
        public void OutcomeOf_DiscriminatesTrajectory()
        {
            // 制度が権力を超え（bal≥1）かつ移譲≥0.5＝共和制軌道
            Assert.AreEqual(FounderOutcome.共和制軌道, FounderTrajectoryRules.OutcomeOf(1.5f, 0.8f, 1f));
            // 制度が権力に劣り（bal<1）かつ移譲<0.5＝専制固定
            Assert.AreEqual(FounderOutcome.専制固定, FounderTrajectoryRules.OutcomeOf(0.4f, 0.2f, 1f));
            // 制度優位だが手放さない＝過渡（まだ確定しない）
            Assert.AreEqual(FounderOutcome.過渡, FounderTrajectoryRules.OutcomeOf(1.5f, 0.2f, 1f));
            // 制度劣位だが手放す＝過渡
            Assert.AreEqual(FounderOutcome.過渡, FounderTrajectoryRules.OutcomeOf(0.4f, 0.8f, 1f));
        }

        /// <summary>建国者の罠＝個人権力が制度を上回る差×権力。権力固定で独裁化の罠（(0.9−0.2)×0.9=0.63）。</summary>
        [Test]
        public void FounderTrapRisk_PowerOverInstitution()
        {
            float risk = FounderTrajectoryRules.FounderTrapRisk(0.9f, 0.2f);
            Assert.AreEqual(0.63f, risk, Eps);
            // 制度が権力以上なら罠はゼロ（自己廃絶できている）
            Assert.AreEqual(0f, FounderTrajectoryRules.FounderTrapRisk(0.3f, 0.5f), Eps);
        }

        /// <summary>遺産の持続＝死後は制度のみ残り、存命中は個人が半ば補う＝制度投資が建国者を超えて続く。</summary>
        [Test]
        public void LegacyDurability_InstitutionSurvivesFounderDeath()
        {
            // 死後＝制度のみ
            Assert.AreEqual(0.6f, FounderTrajectoryRules.LegacyDurability(0.6f, true), Eps);
            // 存命中＝制度0.6 + (1-0.6)×0.5 = 0.8（個人が半ば補う）
            Assert.AreEqual(0.8f, FounderTrajectoryRules.LegacyDurability(0.6f, false), Eps);
            // 制度ゼロの属人組織は死後に何も残らない
            Assert.AreEqual(0f, FounderTrajectoryRules.LegacyDurability(0f, true), Eps);
        }

        /// <summary>共和制建国判定＝OutcomeOf が共和制軌道を返すのと同値（制度優位＋自己廃絶）。</summary>
        [Test]
        public void IsRepublicanFounding_MatchesOutcome()
        {
            Assert.IsTrue(FounderTrajectoryRules.IsRepublicanFounding(1.5f, 0.8f, 1f));
            Assert.IsFalse(FounderTrajectoryRules.IsRepublicanFounding(0.4f, 0.2f, 1f)); // 専制固定
            Assert.IsFalse(FounderTrajectoryRules.IsRepublicanFounding(1.5f, 0.2f, 1f)); // 過渡
        }
    }
}
