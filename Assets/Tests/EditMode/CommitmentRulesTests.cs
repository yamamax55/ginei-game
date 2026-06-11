using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>背水の陣＝韓信型の決死のコミットメント（#1414）の純ロジックテスト。既定Params具体値で期待値を固定。</summary>
    public class CommitmentRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>決死の覚悟＝退路遮断×0.6＋生存本能×0.4。両者最大で1.0、退路があれば低い。</summary>
        [Test]
        public void NoRetreatResolve_退路と生存本能の加重和()
        {
            // 退路完全遮断＋生存本能最大＝0.6+0.4=1.0
            Assert.AreEqual(1f, CommitmentRules.NoRetreatResolve(1f, 1f), Eps);
            // 退路遮断のみ＝0.6
            Assert.AreEqual(0.6f, CommitmentRules.NoRetreatResolve(1f, 0f), Eps);
            // 半端＝0.6*0.5+0.4*0.5=0.5
            Assert.AreEqual(0.5f, CommitmentRules.NoRetreatResolve(0.5f, 0.5f), Eps);
            // 退路があれば（遮断0）覚悟は生存本能分のみ＝0.4
            Assert.AreEqual(0.4f, CommitmentRules.NoRetreatResolve(0f, 1f), Eps);
        }

        /// <summary>戦闘力ブースト＝1+覚悟×0.5。覚悟ゼロで1.0（ボーナスなし）、最大で1.5。</summary>
        [Test]
        public void CombatPowerBoost_決死の覚悟が戦闘力を最大化()
        {
            Assert.AreEqual(1f, CommitmentRules.CombatPowerBoost(0f), Eps);   // 退路あり＝決死にならない
            Assert.AreEqual(1.5f, CommitmentRules.CombatPowerBoost(1f), Eps); // 背水＝戦闘力最大化
            Assert.AreEqual(1.25f, CommitmentRules.CombatPowerBoost(0.5f), Eps);
        }

        /// <summary>敗北の壊滅＝退路なしで敗れると壊滅（上限0.9）。勝てば壊滅なし＝0＝諸刃の片刃。</summary>
        [Test]
        public void DefeatCatastrophe_退路なしの敗北は壊滅()
        {
            // 退路完全遮断で敗北＝1*0.9=0.9（全滅リスク最大）
            Assert.AreEqual(0.9f, CommitmentRules.DefeatCatastrophe(1f, true), Eps);
            // 退路完全遮断でも勝てば壊滅なし
            Assert.AreEqual(0f, CommitmentRules.DefeatCatastrophe(1f, false), Eps);
            // 退路半分遮断で敗北＝0.5*0.9=0.45
            Assert.AreEqual(0.45f, CommitmentRules.DefeatCatastrophe(0.5f, true), Eps);
        }

        /// <summary>背水の士気＝初動は高揚するが膠着で恐慌へ転じる。短期は高く長期は崩れる。</summary>
        [Test]
        public void MoraleUnderCommitment_高揚から恐慌への転化()
        {
            // dt=0（開戦直後）＝1+1*0.4=1.4 をクランプで1.0＝高揚（士気上限）
            Assert.AreEqual(1f, CommitmentRules.MoraleUnderCommitment(1f, 0f), Eps);
            // 膠着 dt=5＝1+0.4-(1*0.2*5)=1.4-1.0=0.4＝恐慌へ転じる
            Assert.AreEqual(0.4f, CommitmentRules.MoraleUnderCommitment(1f, 5f), Eps);
            // 覚悟0＝1+0-0=1.0（普通の士気・恐慌も高揚もなし）
            Assert.AreEqual(1f, CommitmentRules.MoraleUnderCommitment(0f, 5f), Eps);
        }

        /// <summary>背水のタイミング＝敵強大×自軍決死。勝算薄く決死しかない時に高く、余力があれば愚策＝低い。</summary>
        [Test]
        public void CommitmentTiming_勝算薄い時にのみ有効()
        {
            // 強敵×決死しかない＝1*1=1.0（背水が正解）
            Assert.AreEqual(1f, CommitmentRules.CommitmentTiming(1f, 1f), Eps);
            // 強敵だが余力あり＝1*0=0（退路を残せるのに断つのは愚策）
            Assert.AreEqual(0f, CommitmentRules.CommitmentTiming(1f, 0f), Eps);
            // 弱敵なら決死でも不要＝0*1=0
            Assert.AreEqual(0f, CommitmentRules.CommitmentTiming(0f, 1f), Eps);
            // 半端＝0.6*0.5=0.3
            Assert.AreEqual(0.3f, CommitmentRules.CommitmentTiming(0.6f, 0.5f), Eps);
        }

        /// <summary>心理的限界＝覚悟が限界（breakingPoint と既定0.85 の小さい方）を超えると崩壊＝諸刃。</summary>
        [Test]
        public void PsychologicalThreshold_追い詰めすぎると崩壊()
        {
            // 限界内（覚悟0.5 ≤ 限界min(0.85,0.8)=0.8）＝崩壊なし＝0
            Assert.AreEqual(0f, CommitmentRules.PsychologicalThreshold(0.5f, 0.8f), Eps);
            // 限界（min(0.85,0.8)=0.8）超過＝覚悟1.0 → (1.0-0.8)/(1-0.8)=1.0＝完全崩壊（逃散）
            Assert.AreEqual(1f, CommitmentRules.PsychologicalThreshold(1f, 0.8f), Eps);
            // 限界ちょうど＝崩壊なし
            Assert.AreEqual(0f, CommitmentRules.PsychologicalThreshold(0.8f, 0.8f), Eps);
            // 限界0.9指定でも既定0.85で頭打ち＝覚悟0.85ちょうどは崩壊なし
            Assert.AreEqual(0f, CommitmentRules.PsychologicalThreshold(0.85f, 0.9f), Eps);
        }

        /// <summary>敵の油断＝背水を侮るとこちらの決死の反撃が効く（韓信の罠）。侮らなければ1.0。</summary>
        [Test]
        public void EnemyOverconfidence_侮った敵が決死の反撃を食らう()
        {
            // 背水が見えており敵が完全に油断＝1+1*1*0.5=1.5（罠が最大に効く）
            Assert.AreEqual(1.5f, CommitmentRules.EnemyOverconfidence(1f, 1f), Eps);
            // 敵が侮らなければ罠は成立しない＝1.0
            Assert.AreEqual(1f, CommitmentRules.EnemyOverconfidence(1f, 0f), Eps);
            // 背水が敵に見えていなければ＝1.0
            Assert.AreEqual(1f, CommitmentRules.EnemyOverconfidence(0f, 1f), Eps);
            // 半端＝1+0.5*0.5*0.5=1.125
            Assert.AreEqual(1.125f, CommitmentRules.EnemyOverconfidence(0.5f, 0.5f), Eps);
        }

        /// <summary>背水の陣判定＝覚悟が閾値（既定0.5）以上で決死の陣に入った。</summary>
        [Test]
        public void IsBackToTheWall_決死の陣の判定()
        {
            Assert.IsTrue(CommitmentRules.IsBackToTheWall(0.5f));   // 既定閾値ちょうど
            Assert.IsTrue(CommitmentRules.IsBackToTheWall(0.8f));
            Assert.IsFalse(CommitmentRules.IsBackToTheWall(0.4f));  // 退路があり決死に至らない
            // 明示閾値
            Assert.IsTrue(CommitmentRules.IsBackToTheWall(0.7f, 0.7f));
            Assert.IsFalse(CommitmentRules.IsBackToTheWall(0.6f, 0.7f));
        }
    }
}
