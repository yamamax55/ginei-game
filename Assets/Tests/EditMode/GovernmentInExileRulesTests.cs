using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 亡命政権を固定する：持ち出し正統性は目減り、時間で痩せる（承認が忘却を遅らせ・十分なら止める）、
    /// レジスタンス支援＝正統性×故地の不満、忘却判定、帰還条件（不満＞新支配の安定）。境界を担保。
    /// </summary>
    public class GovernmentInExileRulesTests
    {
        private static readonly ExileParams P = ExileParams.Default;
        // 持ち出し0.7/減衰0.02/承認緩和0.2/忘却閾値0.15

        [Test]
        public void InitialExileLegitimacy_Discounted()
        {
            Assert.AreEqual(0.7f, GovernmentInExileRules.InitialExileLegitimacy(1f, P), 1e-5f);
            Assert.AreEqual(0.35f, GovernmentInExileRules.InitialExileLegitimacy(0.5f, P), 1e-5f);
        }

        [Test]
        public void LegitimacyTick_DecaysWithoutRecognition()
        {
            Assert.AreEqual(0.48f, GovernmentInExileRules.LegitimacyTick(0.5f, 0, 1f, P), 1e-5f); // −0.02
            Assert.AreEqual(0f, GovernmentInExileRules.LegitimacyTick(0.01f, 0, 1f, P), 1e-5f);   // 下限0
        }

        [Test]
        public void LegitimacyTick_RecognitionSlowsForgetting()
        {
            // 承認2国＝緩和0.4＝実効減衰0.012
            Assert.AreEqual(0.488f, GovernmentInExileRules.LegitimacyTick(0.5f, 2, 1f, P), 1e-5f);
            // 承認5国＝緩和1.0＝忘却停止
            Assert.AreEqual(0.5f, GovernmentInExileRules.LegitimacyTick(0.5f, 5, 1f, P), 1e-5f);
        }

        [Test]
        public void ResistanceSupport_NeedsBothFlagAndGrievance()
        {
            Assert.AreEqual(0.25f, GovernmentInExileRules.ResistanceSupport(0.5f, 0.5f), 1e-5f);
            // 故地が満足＝呼応なし
            Assert.AreEqual(0f, GovernmentInExileRules.ResistanceSupport(0.5f, 0f), 1e-5f);
            // 旗が無い＝支援なし
            Assert.AreEqual(0f, GovernmentInExileRules.ResistanceSupport(0f, 1f), 1e-5f);
        }

        [Test]
        public void IsForgotten_BelowThreshold()
        {
            Assert.IsTrue(GovernmentInExileRules.IsForgotten(0.14f, P));
            Assert.IsFalse(GovernmentInExileRules.IsForgotten(0.15f, P)); // 閾値ちょうど＝まだ旗
        }

        [Test]
        public void ReturnViable_UnrestMustExceedOccupierStability()
        {
            Assert.IsTrue(GovernmentInExileRules.ReturnViable(0.5f, 0.7f, 0.3f, P));
            // 新支配が安定＝帰還の芽なし（安定統治の完成が亡命政権の死）
            Assert.IsFalse(GovernmentInExileRules.ReturnViable(0.5f, 0.3f, 0.7f, P));
            // 忘れられた旗では立てない
            Assert.IsFalse(GovernmentInExileRules.ReturnViable(0.1f, 0.9f, 0.1f, P));
        }
    }
}
