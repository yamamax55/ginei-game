using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 要人暗殺を固定する：成功率＝基礎×浸透×(1−警護)、仕損じの露見は警護比例、1つの roll で
    /// 成功/失敗/露見を決定論解決、継承ショックは重要度×(1−制度化)、露見の正統性ダメージは重要度比例。
    /// </summary>
    public class AssassinationRulesTests
    {
        private static readonly AssassinationParams P = AssassinationParams.Default;
        // 基礎0.6/露見係数0.5/ショック幅0.5/露見ダメージ0.3

        [Test]
        public void SuccessChance_InfiltrationVsSecurity()
        {
            Assert.AreEqual(0.6f, AssassinationRules.SuccessChance(1f, 0f, P), 1e-5f);  // 警護なし＝基礎満額
            Assert.AreEqual(0f, AssassinationRules.SuccessChance(1f, 1f, P), 1e-5f);    // 完全警護＝不可能
            Assert.AreEqual(0f, AssassinationRules.SuccessChance(0f, 0f, P), 1e-5f);    // 浸透なし＝不可能
            Assert.AreEqual(0.15f, AssassinationRules.SuccessChance(0.5f, 0.5f, P), 1e-5f); // 0.6×0.5×0.5
        }

        [Test]
        public void ExposureChance_GrowsWithSecurity()
        {
            Assert.AreEqual(0f, AssassinationRules.ExposureChance(0f, P), 1e-5f);   // 警護なし＝辿られない
            Assert.AreEqual(0.5f, AssassinationRules.ExposureChance(1f, P), 1e-5f); // 固い警護＝黒幕まで辿る
        }

        [Test]
        public void Attempt_DeterministicThreeWaySplit()
        {
            // 浸透1・警護0.5：成功率=0.6×1×0.5=0.3、露見率=0.5×0.5=0.25
            Assert.AreEqual(AssassinationOutcome.成功, AssassinationRules.Attempt(1f, 0.5f, 0.29f, P));
            // 失敗域 [0.3,1) を正規化：roll=0.4 → sub=(0.4-0.3)/0.7≈0.143 < 0.25 ＝露見
            Assert.AreEqual(AssassinationOutcome.露見, AssassinationRules.Attempt(1f, 0.5f, 0.4f, P));
            // roll=0.9 → sub≈0.857 ≥ 0.25 ＝ただの失敗
            Assert.AreEqual(AssassinationOutcome.失敗, AssassinationRules.Attempt(1f, 0.5f, 0.9f, P));
        }

        [Test]
        public void Attempt_ImpossibleNeverSucceeds()
        {
            // 完全警護＝roll がいくつでも成功しない
            Assert.AreNotEqual(AssassinationOutcome.成功, AssassinationRules.Attempt(1f, 1f, 0f, P));
        }

        [Test]
        public void SuccessionShock_InstitutionsAbsorbTheBlow()
        {
            // 属人組織の柱を折る＝最大ショック0.5
            Assert.AreEqual(0.5f, AssassinationRules.SuccessionShock(1f, 0f, P), 1e-5f);
            // 完全に制度化された組織＝人が変わっても回る＝ショック0
            Assert.AreEqual(0f, AssassinationRules.SuccessionShock(1f, 1f, P), 1e-5f);
            // 小物を消しても揺れない
            Assert.AreEqual(0f, AssassinationRules.SuccessionShock(0f, 0f, P), 1e-5f);
        }

        [Test]
        public void ExposureLegitimacyLoss_ProportionalToImportance()
        {
            Assert.AreEqual(0.3f, AssassinationRules.ExposureLegitimacyLoss(1f, P), 1e-5f);
            Assert.AreEqual(0.15f, AssassinationRules.ExposureLegitimacyLoss(0.5f, P), 1e-5f);
        }
    }
}
