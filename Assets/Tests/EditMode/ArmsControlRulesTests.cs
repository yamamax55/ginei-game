using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍縮条約を固定する：超過分＝Max(0,実建艦−上限)、発覚率＝超過比×査察アクセスの積、
    /// 発覚判定＝roll未満（決定論）、裏切りの誘惑＝脅威感×不信の積、
    /// 発覚時の信頼崩壊＝下限0.5＋超過比例、軍縮の配当＝厳しさ×経済×0.3、
    /// 検証のジレンマ＝アクセス×0.6。クランプを担保。
    /// </summary>
    public class ArmsControlRulesTests
    {
        private static readonly ArmsControlParams P = ArmsControlParams.Default;
        // 発覚飽和超過量100/信頼損失下限0.5/配当率0.3/透明性コスト係数0.6

        [Test]
        public void ComplianceGap_ExcessOverCap()
        {
            Assert.AreEqual(50f, ArmsControlRules.ComplianceGap(150f, 100f), 1e-5f); // 50隠している
            Assert.AreEqual(0f, ArmsControlRules.ComplianceGap(80f, 100f), 1e-5f);   // 上限以下＝遵守
            Assert.AreEqual(0f, ArmsControlRules.ComplianceGap(100f, 100f), 1e-5f);  // ちょうど＝遵守
            Assert.AreEqual(0f, ArmsControlRules.ComplianceGap(-5f, 100f), 1e-5f);   // 入力クランプ
            Assert.AreEqual(50f, ArmsControlRules.ComplianceGap(50f, -10f), 1e-5f);  // 負の上限＝0扱い
        }

        [Test]
        public void DetectionChance_GapTimesAccess()
        {
            Assert.AreEqual(0.5f, ArmsControlRules.DetectionChance(50f, 1f, P), 1e-5f);   // 半分の嘘×完全査察
            Assert.AreEqual(1f, ArmsControlRules.DetectionChance(100f, 1f, P), 1e-5f);    // 大きな嘘は隠しきれない
            Assert.AreEqual(1f, ArmsControlRules.DetectionChance(300f, 1f, P), 1e-5f);    // 飽和で頭打ち
            Assert.AreEqual(0f, ArmsControlRules.DetectionChance(100f, 0f, P), 1e-5f);    // 査察ゼロ＝大違反も見えない
            Assert.AreEqual(0.25f, ArmsControlRules.DetectionChance(50f, 0.5f, P), 1e-5f);
            Assert.AreEqual(0f, ArmsControlRules.DetectionChance(0f, 1f, P), 1e-5f);      // 遵守国に査察は何も出さない
        }

        [Test]
        public void Caught_RollBelowChance_Deterministic()
        {
            Assert.IsTrue(ArmsControlRules.Caught(100f, 1f, 0.5f, P));   // 発覚率1.0＝必ず露見
            Assert.IsTrue(ArmsControlRules.Caught(50f, 1f, 0.49f, P));   // 0.49 < 0.5
            Assert.IsFalse(ArmsControlRules.Caught(50f, 1f, 0.5f, P));   // 率ちょうど＝逃げ切り（未満のみ）
            Assert.IsFalse(ArmsControlRules.Caught(50f, 0f, 0f, P));     // 査察ゼロは絶対に捕まらない
        }

        [Test]
        public void CheatingTemptation_PressureTimesDistrust()
        {
            Assert.AreEqual(1f, ArmsControlRules.CheatingTemptation(1f, 0f), 1e-5f);     // 最大脅威×完全不信＝必ず破る
            Assert.AreEqual(0f, ArmsControlRules.CheatingTemptation(1f, 1f), 1e-5f);     // 相手を信じ切れば誘惑は消える
            Assert.AreEqual(0f, ArmsControlRules.CheatingTemptation(0f, 0f), 1e-5f);     // 脅威がなければ破る理由がない
            Assert.AreEqual(0.4f, ArmsControlRules.CheatingTemptation(0.8f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, ArmsControlRules.CheatingTemptation(2f, -1f), 1e-5f);    // 入力クランプ
        }

        [Test]
        public void ExposureFallout_BaseFloorPlusProportional()
        {
            Assert.AreEqual(0f, ArmsControlRules.ExposureFallout(0f, P), 1e-5f);      // 違反なし＝崩壊なし
            Assert.AreEqual(0.55f, ArmsControlRules.ExposureFallout(10f, P), 1e-5f);  // 小違反でも下限0.5は死ぬ
            Assert.AreEqual(0.75f, ArmsControlRules.ExposureFallout(50f, P), 1e-5f);  // 0.5+0.5×0.5
            Assert.AreEqual(1f, ArmsControlRules.ExposureFallout(100f, P), 1e-5f);    // 大違反＝条約死
            Assert.AreEqual(1f, ArmsControlRules.ExposureFallout(500f, P), 1e-5f);    // 飽和で頭打ち
        }

        [Test]
        public void MutualSavings_StrictCapPaysDividend()
        {
            Assert.AreEqual(300f, ArmsControlRules.MutualSavings(1f, 1000f, P), 1e-4f);  // 全面禁止＝最大配当
            Assert.AreEqual(150f, ArmsControlRules.MutualSavings(0.5f, 1000f, P), 1e-4f);
            Assert.AreEqual(0f, ArmsControlRules.MutualSavings(0f, 1000f, P), 1e-5f);    // 制限なし＝配当なし
            Assert.AreEqual(300f, ArmsControlRules.MutualSavings(3f, 1000f, P), 1e-4f);  // 入力クランプ
            Assert.AreEqual(0f, ArmsControlRules.MutualSavings(1f, -100f, P), 1e-5f);    // 負の経済規模＝0扱い
        }

        [Test]
        public void VerificationDilemma_TransparencyHasCost()
        {
            Assert.AreEqual(0.6f, ArmsControlRules.VerificationDilemma(1f, P), 1e-5f);   // 完全査察＝手の内を最も晒す
            Assert.AreEqual(0.3f, ArmsControlRules.VerificationDilemma(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, ArmsControlRules.VerificationDilemma(0f, P), 1e-5f);     // 査察拒否＝コストもないが検証もない
            Assert.AreEqual(0.6f, ArmsControlRules.VerificationDilemma(3f, P), 1e-5f);   // 入力クランプ
        }

        [Test]
        public void Story_TrustButVerify_NoInspectionInvitesCheating()
        {
            // 同じ秘密再軍備（上限100に対し150建艦＝超過50）でも：
            // 査察なき条約は絶対に露見せず（検証なき信頼）、深い査察は半々で捕まえる
            float gap = ArmsControlRules.ComplianceGap(150f, 100f); // 50
            Assert.IsFalse(ArmsControlRules.Caught(gap, 0f, 0f, P));            // 査察拒否＝裏切りはノーリスク
            Assert.AreEqual(0.5f, ArmsControlRules.DetectionChance(gap, 1f, P), 1e-5f); // 完全査察＝発覚率50%

            // 露見すれば信頼は0.75崩壊＝以後の条約が結べない＝裏切りの期待値が変わる
            Assert.AreEqual(0.75f, ArmsControlRules.ExposureFallout(gap, P), 1e-5f);
        }
    }
}
