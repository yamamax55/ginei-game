using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 偽装退却ルール（<see cref="FeignedRetreatRules"/>）の純ロジック検証。
    /// 既定 <see cref="FeignedRetreatParams"/> で期待値を固定（検算済み）。Pow を使わないので許容は全て 1e-4f。
    /// </summary>
    public class FeignedRetreatRulesTests
    {
        const float Eps = 1e-4f;

        // --- Convincingness（演技の真に迫る度） ---

        [Test]
        public void Convincingness_WeightsRealismAndDiscipline()
        {
            // 0.8*0.6 + (80/100)*0.4 = 0.48 + 0.32 = 0.8
            Assert.AreEqual(0.8f, FeignedRetreatRules.Convincingness(0.8f, 80f), Eps);
        }

        [Test]
        public void Convincingness_PerfectInputs_Saturate()
        {
            // 1*0.6 + 1*0.4 = 1.0
            Assert.AreEqual(1.0f, FeignedRetreatRules.Convincingness(1f, 100f), Eps);
        }

        // --- EnemyTakesBait（敵の食いつき） ---

        [Test]
        public void EnemyTakesBait_AggressiveUndisciplinedEnemy_Bites()
        {
            // 0.8 * 0.9 * (1-0.2) = 0.576
            Assert.AreEqual(0.576f, FeignedRetreatRules.EnemyTakesBait(0.8f, 0.9f, 0.2f), Eps);
        }

        [Test]
        public void EnemyTakesBait_DisciplinedEnemy_DoesNotBite()
        {
            // 規律が完璧（1.0）なら釣られない
            Assert.AreEqual(0f, FeignedRetreatRules.EnemyTakesBait(0.8f, 0.9f, 1.0f), Eps);
        }

        // --- PursuerDisorder（追撃側の崩れ） ---

        [Test]
        public void PursuerDisorder_DeeperPursuit_BreaksFormation()
        {
            // 0.5 * clamp01(4*0.1=0.4) = 0.2
            Assert.AreEqual(0.2f, FeignedRetreatRules.PursuerDisorder(0.5f, 4f), Eps);
        }

        [Test]
        public void PursuerDisorder_NoPursuit_NoDisorder()
        {
            // 追ってこなければ崩れない
            Assert.AreEqual(0f, FeignedRetreatRules.PursuerDisorder(0.9f, 0f), Eps);
        }

        // --- ReversalImpact（反転＋伏兵） ---

        [Test]
        public void ReversalImpact_AmbushAddsBonus()
        {
            // 0.5 * (1 + 0.4*1.0) = 0.7
            Assert.AreEqual(0.7f, FeignedRetreatRules.ReversalImpact(0.5f, 0.4f), Eps);
        }

        [Test]
        public void ReversalImpact_NoDisorder_NoImpact()
        {
            // 崩れていなければ伏兵があっても叩けない
            Assert.AreEqual(0f, FeignedRetreatRules.ReversalImpact(0f, 1f), Eps);
        }

        // --- DetectionRisk / BackfireLoss（見破りと逆効果） ---

        [Test]
        public void DetectionRisk_PoorActingSharpIntel_Higher()
        {
            // (1-0.4)*0.8*0.5 = 0.24
            Assert.AreEqual(0.24f, FeignedRetreatRules.DetectionRisk(0.4f, 0.8f), Eps);
        }

        [Test]
        public void DetectionRisk_PerfectActing_NotDetected()
        {
            Assert.AreEqual(0f, FeignedRetreatRules.DetectionRisk(1f, 0.9f), Eps);
        }

        [Test]
        public void BackfireLoss_ScalesWithCommitment()
        {
            // 0.24 * 0.5 = 0.12
            Assert.AreEqual(0.12f, FeignedRetreatRules.BackfireLoss(0.24f, 0.5f), Eps);
        }

        [Test]
        public void BackfireLoss_ClampedByMaxBackfire()
        {
            // clamp(1*1, 0, 0.9) = 0.9
            Assert.AreEqual(0.9f, FeignedRetreatRules.BackfireLoss(1f, 1f), Eps);
        }

        // --- FeintNetValue（正味価値） ---

        [Test]
        public void FeintNetValue_SubtractsBackfire()
        {
            // 0.7 - 0.12 = 0.58
            Assert.AreEqual(0.58f, FeignedRetreatRules.FeintNetValue(0.7f, 0.12f), Eps);
        }

        [Test]
        public void FeintNetValue_HighBackfire_CanBeNegative()
        {
            // 0.1 - 0.9 = -0.8（見破られると割に合わない）
            Assert.AreEqual(-0.8f, FeignedRetreatRules.FeintNetValue(0.1f, 0.9f), Eps);
        }

        // --- IsBaitTaken（罠にかかったか） ---

        [Test]
        public void IsBaitTaken_AboveThreshold_True()
        {
            Assert.IsTrue(FeignedRetreatRules.IsBaitTaken(0.576f));
        }

        [Test]
        public void IsBaitTaken_BelowThreshold_False()
        {
            Assert.IsFalse(FeignedRetreatRules.IsBaitTaken(0.2f));
        }

        // --- 物語テスト ---

        [Test]
        public void Narrative_SkilledFeintLuresAggressiveEnemyThenReverses()
        {
            // 巧みな偽装退却（演技0.9・統率85）が攻撃的で規律の低い敵（攻撃性0.95・規律0.15）を釣る。
            float conv = FeignedRetreatRules.Convincingness(0.9f, 85f);
            // 0.9*0.6 + 0.85*0.4 = 0.54 + 0.34 = 0.88
            Assert.AreEqual(0.88f, conv, Eps);

            float bait = FeignedRetreatRules.EnemyTakesBait(conv, 0.95f, 0.15f);
            // 0.88 * 0.95 * 0.85 = 0.7106
            Assert.AreEqual(0.7106f, bait, Eps);
            Assert.IsTrue(FeignedRetreatRules.IsBaitTaken(bait), "攻撃的な敵は罠にかかる");

            // 深く追ってきて隊形が崩れる（距離8）。
            float disorder = FeignedRetreatRules.PursuerDisorder(bait, 8f);
            // 0.7106 * clamp01(8*0.1=0.8) = 0.56848
            Assert.AreEqual(0.56848f, disorder, Eps);

            // 反転＋伏兵（規模0.6）で叩く。
            float impact = FeignedRetreatRules.ReversalImpact(disorder, 0.6f);
            // 0.56848 * (1 + 0.6) = 0.909568
            Assert.AreEqual(0.909568f, impact, Eps);

            // 演技が完璧に近く（0.9）敵の情報も鈍い（0.2）ので見破りリスクは低い。
            float detect = FeignedRetreatRules.DetectionRisk(0.9f, 0.2f);
            // (1-0.9)*0.2*0.5 = 0.01
            Assert.AreEqual(0.01f, detect, Eps);
            float backfire = FeignedRetreatRules.BackfireLoss(detect, 0.7f);
            // 0.01 * 0.7 = 0.007
            Assert.AreEqual(0.007f, backfire, Eps);

            float net = FeignedRetreatRules.FeintNetValue(impact, backfire);
            // 0.909568 - 0.007 = 0.902568
            Assert.AreEqual(0.902568f, net, Eps);
            Assert.Greater(net, 0.5f, "巧みな偽装退却は大きな正味利得を生む");

            // 対照：拙い偽装（演技0.2）は鋭い敵（情報0.9）に見破られ、深入り（commitment0.8）で逆効果が大きい。
            float poorDetect = FeignedRetreatRules.DetectionRisk(0.2f, 0.9f);
            // (1-0.2)*0.9*0.5 = 0.36
            Assert.AreEqual(0.36f, poorDetect, Eps);
            float poorBackfire = FeignedRetreatRules.BackfireLoss(poorDetect, 0.8f);
            // 0.36 * 0.8 = 0.288
            Assert.AreEqual(0.288f, poorBackfire, Eps);
            // 反転打撃が小さい（敵が浅く崩れただけ＝0.1）と正味は負＝仕掛けるべきでない。
            float poorNet = FeignedRetreatRules.FeintNetValue(0.1f, poorBackfire);
            // 0.1 - 0.288 = -0.188
            Assert.AreEqual(-0.188f, poorNet, Eps);
            Assert.Less(poorNet, 0f, "見破られた偽装退却は逆効果（本当に不利になる）");
        }
    }
}
