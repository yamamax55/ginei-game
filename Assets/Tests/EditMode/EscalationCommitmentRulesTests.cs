using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// エスカレーション・コミットメント＝サンクコストへの固執（#1378）の EditMode テスト。
    /// 既定 Params の具体値で期待値を固定する。
    /// </summary>
    public class EscalationCommitmentRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>固執の重さ＝投下犠牲×重み＋面子×重み。犠牲が大きく面子がかかるほど重い。</summary>
        [Test]
        public void SunkCostWeight_投じた犠牲と面子で固執が重くなる()
        {
            // 0.6*0.8 + 0.4*0.5 = 0.68
            Assert.AreEqual(0.68f, EscalationCommitmentRules.SunkCostWeight(0.8f, 0.5f), Eps);
            // 犠牲も面子もゼロなら固執は立ち上がらない
            Assert.AreEqual(0f, EscalationCommitmentRules.SunkCostWeight(0f, 0f), Eps);
        }

        /// <summary>コミットメントのロック＝固執の重さを合理的撤退価値が緩めるが、サンクコストが撤退を縛る。</summary>
        [Test]
        public void CommitmentLock_合理的撤退でも固執が残る()
        {
            // 0.68 - 0.3*0.6 = 0.5
            Assert.AreEqual(0.5f, EscalationCommitmentRules.CommitmentLock(0.68f, 0.3f), Eps);
            // 撤退価値が大きくてもサンクコストぶんは縛られ完全には0にならない（rationalPull=0.6＜1）
            Assert.AreEqual(0.28f, EscalationCommitmentRules.CommitmentLock(0.68f, 0.666666f), Eps);
        }

        /// <summary>損の追い銭＝ロックが強いほど追加投入で傷を深める。ロックゼロなら追い銭しない。</summary>
        [Test]
        public void ThrowGoodAfterBad_泥沼へ追加投入する()
        {
            // 0.5*0.6*0.5 = 0.15
            Assert.AreEqual(0.15f, EscalationCommitmentRules.ThrowGoodAfterBad(0.5f, 0.6f), Eps);
            // ロックがゼロ＝損切りできるなら追い銭はしない
            Assert.AreEqual(0f, EscalationCommitmentRules.ThrowGoodAfterBad(0f, 1f), Eps);
        }

        /// <summary>固執スパイラル＝ロックが時間で深まり引き返せなくなる（インパールの継続）。</summary>
        [Test]
        public void EscalationSpiral_固執が時間で深まる()
        {
            // 0.5 + 0.5*0.2*2 = 0.7
            Assert.AreEqual(0.7f, EscalationCommitmentRules.EscalationSpiral(0.5f, 2f), Eps);
            // ロックがゼロなら深まらない（撤退できる状態は泥沼化しない）
            Assert.AreEqual(0f, EscalationCommitmentRules.EscalationSpiral(0f, 5f), Eps);
        }

        /// <summary>面子の駆動＝面子に公約が上乗せされ撤退が一層難しくなる。</summary>
        [Test]
        public void FacePreservationDrive_公約が撤退を難しくする()
        {
            // 0.4 + 0.6*0.5 = 0.7
            Assert.AreEqual(0.7f, EscalationCommitmentRules.FacePreservationDrive(0.4f, 0.6f), Eps);
            // 公約ゼロなら面子のみ
            Assert.AreEqual(0.4f, EscalationCommitmentRules.FacePreservationDrive(0.4f, 0f), Eps);
        }

        /// <summary>逃した合理的撤退＝撤退の好機をロックぶんだけ逃す。ロックゼロなら逃さない。</summary>
        [Test]
        public void RationalExitForegone_好機を固執で逃す()
        {
            // 0.8*0.5 = 0.4
            Assert.AreEqual(0.4f, EscalationCommitmentRules.RationalExitForegone(0.5f, 0.8f), Eps);
            // ロックがゼロ＝損切りできるなら好機を逃さない
            Assert.AreEqual(0f, EscalationCommitmentRules.RationalExitForegone(0f, 1f), Eps);
        }

        /// <summary>ロック解除＝外的衝撃か指導者交代がロックを崩す。両者ゼロなら内側からは解けない。</summary>
        [Test]
        public void LockBreaker_外的衝撃か指導者交代が固執を解く()
        {
            // 1 - (1-0.5*0.7)(1-0.5*0.6) = 1 - 0.65*0.7 = 0.545
            Assert.AreEqual(0.545f, EscalationCommitmentRules.LockBreaker(0.5f, 0.5f), Eps);
            // 両者ゼロ＝固執は内側からは解けない
            Assert.AreEqual(0f, EscalationCommitmentRules.LockBreaker(0f, 0f), Eps);
            // 指導者交代が満ちれば leadershipBreak=0.6 ぶん解除
            Assert.AreEqual(0.6f, EscalationCommitmentRules.LockBreaker(0f, 1f), Eps);
        }

        /// <summary>サンクコストの罠＝ロックと固執の重さがともに閾値以上で泥沼。一方だけでは罠でない。</summary>
        [Test]
        public void IsSunkCostTrap_損切り不能で泥沼に陥る()
        {
            // ロック0.6・固執0.68 とも0.5以上＝罠
            Assert.IsTrue(EscalationCommitmentRules.IsSunkCostTrap(0.6f, 0.68f));
            // ロックが浅い＝撤退できるので罠ではない
            Assert.IsFalse(EscalationCommitmentRules.IsSunkCostTrap(0.4f, 0.68f));
            // 固執の重さが浅い＝犠牲が浅く抜けられるので罠ではない
            Assert.IsFalse(EscalationCommitmentRules.IsSunkCostTrap(0.6f, 0.4f));
        }
    }
}
