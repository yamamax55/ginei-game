using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 弾劾・不信任（CoupRules の制度版＝合法的な政権打倒経路）を固定する：
    /// 訴追の強さ（証拠×罪）、議席要件（特別多数2/3）、党派忠誠の壁（政治的裁判）、
    /// 決定論判定、失敗の逆風の非対称（弱い訴追ほど政権を強化＝魔女狩り）、罷免の正統性。
    /// </summary>
    public class ImpeachmentRulesTests
    {
        private static readonly ImpeachmentParams P = ImpeachmentParams.Default;
        // 必要議席2/3・党派の壁0.7・魔女狩り逆風0.5・燻り0.2

        [Test]
        public void CaseStrength_NeedsBothEvidenceAndSeverity()
        {
            // 完璧な証拠×大罪＝最強の訴追
            Assert.AreEqual(1f, ImpeachmentRules.CaseStrength(1f, 1f), 1e-5f);
            // 証拠が完璧でも軽微な罪では訴追は立たない
            Assert.AreEqual(0f, ImpeachmentRules.CaseStrength(1f, 0f), 1e-5f);
            // 大罪でも証拠が無ければ立たない
            Assert.AreEqual(0f, ImpeachmentRules.CaseStrength(0f, 1f), 1e-5f);
            Assert.AreEqual(0.4f, ImpeachmentRules.CaseStrength(0.8f, 0.5f), 1e-5f);
            // 入力クランプ（範囲外は0..1へ）
            Assert.AreEqual(1f, ImpeachmentRules.CaseStrength(2f, 3f), 1e-5f);
            Assert.AreEqual(0f, ImpeachmentRules.CaseStrength(-1f, 1f), 1e-5f);
        }

        [Test]
        public void VoteThresholdMet_TwoThirdsSupermajority()
        {
            // 既定の必要議席は2/3＝単純多数では弾劾できない
            Assert.IsFalse(ImpeachmentRules.VoteThresholdMet(0.51f));
            Assert.IsFalse(ImpeachmentRules.VoteThresholdMet(0.66f));
            // ちょうど2/3で成立（境界含む）
            Assert.IsTrue(ImpeachmentRules.VoteThresholdMet(2f / 3f));
            Assert.IsTrue(ImpeachmentRules.VoteThresholdMet(0.67f));
            // 要件は引数でも指定できる（単純多数の不信任など）
            Assert.IsTrue(ImpeachmentRules.VoteThresholdMet(0.51f, 0.5f));
        }

        [Test]
        public void ConvictionChance_PartisanLoyaltyIsTheWall()
        {
            // 議席要件を満たす前提（賛成0.7）：完璧な訴追×忠誠ゼロの議会＝確実に成立
            Assert.AreEqual(1f, ImpeachmentRules.ConvictionChance(1f, 0.7f, 0f, P), 1e-5f);
            // 証拠が完璧でも党派忠誠が満杯なら3割しか通らない＝政治的裁判
            Assert.AreEqual(0.3f, ImpeachmentRules.ConvictionChance(1f, 0.7f, 1f, P), 1e-5f);
            Assert.AreEqual(0.65f, ImpeachmentRules.ConvictionChance(1f, 0.7f, 0.5f, P), 1e-5f);
            // 議席要件未達なら証拠が完璧でも成立0＝数なくして弾劾なし
            Assert.AreEqual(0f, ImpeachmentRules.ConvictionChance(1f, 0.5f, 0f, P), 1e-5f);
        }

        [Test]
        public void Convicted_DeterministicRoll()
        {
            // roll < chance で成立＝同じ入力は常に同じ結果（決定論）
            Assert.IsTrue(ImpeachmentRules.Convicted(0.3f, 0.29f));
            Assert.IsFalse(ImpeachmentRules.Convicted(0.3f, 0.3f));
            Assert.IsFalse(ImpeachmentRules.Convicted(0f, 0f));
            Assert.IsTrue(ImpeachmentRules.Convicted(1f, 0.999f));
        }

        [Test]
        public void FailedImpeachmentBacklash_WeakCaseStrengthensTheGovernment()
        {
            // 根拠薄弱な弾劾の失敗＝「魔女狩り」と映り政権を最大限強化（+0.5）
            Assert.AreEqual(0.5f, ImpeachmentRules.FailedImpeachmentBacklash(0f, P), 1e-5f);
            // 強い訴追の失敗だけは世論に燻り、政権を蝕む（−0.2）
            Assert.AreEqual(-0.2f, ImpeachmentRules.FailedImpeachmentBacklash(1f, P), 1e-5f);
            // 中間でもなお政権は強くなる（0.5×0.5−0.2×0.5=+0.15）＝外科手術の掟：
            // 失敗すれば患者（政権）でなく執刀医（訴追側）が死ぬ
            Assert.AreEqual(0.15f, ImpeachmentRules.FailedImpeachmentBacklash(0.5f, P), 1e-5f);
        }

        [Test]
        public void FailedImpeachmentBacklash_AsymmetryFavorsThePatient()
        {
            // 非対称の担保：弱い訴追の強化幅(+0.5)＞強い訴追の燻り幅(0.2)
            float witchHunt = ImpeachmentRules.FailedImpeachmentBacklash(0f, P);
            float smolder = ImpeachmentRules.FailedImpeachmentBacklash(1f, P);
            Assert.Greater(witchHunt, 0f);
            Assert.Less(smolder, 0f);
            Assert.Greater(witchHunt, -smolder);
            // 損益分岐は0.5より上＝相当強い訴追でなければ失敗は政権の追い風
            Assert.Greater(ImpeachmentRules.FailedImpeachmentBacklash(0.7f, P), 0f);
        }

        [Test]
        public void LegitimacyOfRemoval_DueProcessIsTheFoundation()
        {
            // 強い訴追×正しい手続き＝最大の正統性＝次の政権の足場
            Assert.AreEqual(1f, ImpeachmentRules.LegitimacyOfRemoval(1f, 1f), 1e-5f);
            // 数の力で押し切った罷免（手続き0）はクーデターの法服版＝正統性ゼロ
            Assert.AreEqual(0f, ImpeachmentRules.LegitimacyOfRemoval(1f, 0f), 1e-5f);
            // 訴追が弱ければ手続きが完璧でも正統性は立たない
            Assert.AreEqual(0f, ImpeachmentRules.LegitimacyOfRemoval(0f, 1f), 1e-5f);
            Assert.AreEqual(0.4f, ImpeachmentRules.LegitimacyOfRemoval(0.8f, 0.5f), 1e-5f);
        }

        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            // 既定Params省略オーバーロードの一致
            Assert.AreEqual(
                ImpeachmentRules.ConvictionChance(0.8f, 0.7f, 0.6f, P),
                ImpeachmentRules.ConvictionChance(0.8f, 0.7f, 0.6f), 1e-6f);
            Assert.AreEqual(
                ImpeachmentRules.FailedImpeachmentBacklash(0.4f, P),
                ImpeachmentRules.FailedImpeachmentBacklash(0.4f), 1e-6f);
            Assert.AreEqual(
                ImpeachmentRules.VoteThresholdMet(0.7f, P.requiredShare),
                ImpeachmentRules.VoteThresholdMet(0.7f));
        }
    }
}
