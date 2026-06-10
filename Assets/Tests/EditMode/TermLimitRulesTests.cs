using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 任期制限（権力の時間制約）を固定する：危機×人気×在任で延長誘惑が立ち、
    /// 慣習は破られると一撃で痩せて完全には戻らず（非対称）、最初の違反が最も高くつき、
    /// 平和的政権交代は対数的に制度資本を積む。境界・クランプ・決定論。
    /// </summary>
    public class TermLimitRulesTests
    {
        private static TermLimitParams P => TermLimitParams.Default;

        // --- ExtensionTemptation ---

        [Test]
        public void ExtensionTemptation_NoCrisisOrNoPopularity_Zero()
        {
            // 危機か人気のどちらかが欠ければ延長の理屈は立たない（積モデル）
            Assert.AreEqual(0f, TermLimitRules.ExtensionTemptation(0f, 1f, 5, P), 1e-4f);
            Assert.AreEqual(0f, TermLimitRules.ExtensionTemptation(1f, 0f, 5, P), 1e-4f);
            // 負の入力・負の期数もクランプされてゼロ
            Assert.AreEqual(0f, TermLimitRules.ExtensionTemptation(-1f, -1f, -3, P), 1e-4f);
        }

        [Test]
        public void ExtensionTemptation_TenureAmplifies_DefaultValues()
        {
            // 0.5×0.8×(1+2×0.25)=0.6 ＝「余人をもって代えがたい」は在任が長いほど強まる
            Assert.AreEqual(0.6f, TermLimitRules.ExtensionTemptation(0.5f, 0.8f, 2, P), 1e-4f);
            // 在任ゼロ期なら増幅なし＝0.4
            Assert.AreEqual(0.4f, TermLimitRules.ExtensionTemptation(0.5f, 0.8f, 0, P), 1e-4f);
            // 上限は 1 にクランプ
            Assert.AreEqual(1f, TermLimitRules.ExtensionTemptation(1f, 1f, 10, P), 1e-4f);
        }

        // --- NormStrength（違反の非対称） ---

        [Test]
        public void NormStrength_Unbroken_IsOne()
        {
            // 無違反の慣習は満点＝完全な常識として機能する
            Assert.AreEqual(1f, TermLimitRules.NormStrength(0, 100f, P), 1e-4f);
        }

        [Test]
        public void NormStrength_ViolationCutsFast_RecoveryIsSlowAndCapped()
        {
            // 1回の違反で 1→0.4 へ一撃で痩せる
            Assert.AreEqual(0.4f, TermLimitRules.NormStrength(1, 0f, P), 1e-4f);
            // 10年で +0.1 しか戻らない＝0.5（破るのは一瞬・戻るのは僅か）
            Assert.AreEqual(0.5f, TermLimitRules.NormStrength(1, 10f, P), 1e-4f);
            // どれだけ時間が経っても天井 0.8 まで＝一度破られた慣習は二度と満点に戻らない（非対称の核）
            Assert.AreEqual(0.8f, TermLimitRules.NormStrength(1, 1000f, P), 1e-4f);
            // 2回目の違反は乗算で 0.4^2=0.16 ＝慣習は指数的に空洞化する
            Assert.AreEqual(0.16f, TermLimitRules.NormStrength(2, 0f, P), 1e-4f);
        }

        // --- ExtensionLegitimacyCost ---

        [Test]
        public void ExtensionLegitimacyCost_FirstViolationCostsMost()
        {
            // 満点の慣習を最初に破る者が最大コスト 0.5 を払う
            float first = TermLimitRules.ExtensionLegitimacyCost(TermLimitRules.NormStrength(0, 0f, P), P);
            Assert.AreEqual(0.5f, first, 1e-4f);
            // 一度破られた後（0.4）の延長は 0.2 ＝二人目の独裁者は安く済む
            float second = TermLimitRules.ExtensionLegitimacyCost(TermLimitRules.NormStrength(1, 0f, P), P);
            Assert.AreEqual(0.2f, second, 1e-4f);
            Assert.Less(second, first);
        }

        // --- PeacefulTransferValue ---

        [Test]
        public void PeacefulTransferValue_LogarithmicDiminishingGains()
        {
            // 交代ゼロは資本ゼロ
            Assert.AreEqual(0f, TermLimitRules.PeacefulTransferValue(0, P), 1e-4f);
            // 1回目＝0.3×ln2≈0.20794（最初の前例が最も大きく効く）
            Assert.AreEqual(0.20794f, TermLimitRules.PeacefulTransferValue(1, P), 1e-4f);
            // 増分は逓減する（対数的逓増）
            float v1 = TermLimitRules.PeacefulTransferValue(1, P);
            float v2 = TermLimitRules.PeacefulTransferValue(2, P);
            Assert.Less(v2 - v1, v1 - 0f);
            // 多数回でも 1 にクランプ
            Assert.AreEqual(1f, TermLimitRules.PeacefulTransferValue(100, P), 1e-4f);
        }

        // --- RepublicDecayTick ---

        [Test]
        public void RepublicDecayTick_ViolationCutsBig_PeaceAccruesLittle()
        {
            // 違反は一撃で −0.3（dt 非依存のイベント）
            Assert.AreEqual(0.5f, TermLimitRules.RepublicDecayTick(0.8f, violation: true, deltaTime: 0f, P), 1e-4f);
            // 平時は 10 年で +0.1 しか積めない
            Assert.AreEqual(0.6f, TermLimitRules.RepublicDecayTick(0.5f, violation: false, deltaTime: 10f, P), 1e-4f);
            // 1回の違反を取り戻すには 30 年かかる＝壊すのは一瞬・築くのは一生
            Assert.AreEqual(0.8f, TermLimitRules.RepublicDecayTick(0.5f, violation: false, deltaTime: 30f, P), 1e-4f);
            // dt<=0 の平時は不変、下限 0 にクランプ
            Assert.AreEqual(0.5f, TermLimitRules.RepublicDecayTick(0.5f, violation: false, deltaTime: 0f, P), 1e-4f);
            Assert.AreEqual(0f, TermLimitRules.RepublicDecayTick(0.1f, violation: true, deltaTime: 0f, P), 1e-4f);
        }

        // --- TermLimitParams ---

        [Test]
        public void Params_CtorClampsOutOfRangeValues()
        {
            // 範囲外の調整値は ctor でクランプされる（負→0・超過→1）
            var p = new TermLimitParams(
                tenureWeight: -1f, violationDecay: 2f, normRecoveryPerYear: -0.5f, normRecoveryCeiling: 1.5f,
                maxLegitimacyCost: -0.1f, transferLogScale: -3f, violationDamage: 9f, accrualRate: -1f);
            Assert.AreEqual(0f, p.TenureWeight, 1e-4f);
            Assert.AreEqual(1f, p.ViolationDecay, 1e-4f);
            Assert.AreEqual(0f, p.NormRecoveryPerYear, 1e-4f);
            Assert.AreEqual(1f, p.NormRecoveryCeiling, 1e-4f);
            Assert.AreEqual(0f, p.MaxLegitimacyCost, 1e-4f);
            Assert.AreEqual(0f, p.TransferLogScale, 1e-4f);
            Assert.AreEqual(1f, p.ViolationDamage, 1e-4f);
            Assert.AreEqual(0f, p.AccrualRate, 1e-4f);
        }
    }
}
