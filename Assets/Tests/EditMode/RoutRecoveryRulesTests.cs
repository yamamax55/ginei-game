using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 敗走からの立ち直りの決まり。
    ///
    /// 実機で「敗走 開始（士気 2.0→0.0）→ <b>次フレーム</b>に 敗走 解除（士気 0.0→0.0）」が
    /// 反復した。回復量 <c>recoveryRate × deltaTime</c> はごく小さな正値なので、
    /// 立ち直りの<b>待ち時間</b>が正しく効いていないと、この反復が起きる。
    /// </summary>
    public class RoutRecoveryRulesTests
    {
        private const float Delay = 4f;

        // ===== 被弾も交戦のうち =====

        /// <summary>
        /// ★一方的に叩かれている（自分は撃っていない・射界に敵もいない）部隊も<b>交戦中</b>。
        /// ここを落とすと、撃たれながら立ち直り待ちが進んでしまう。
        ///
        /// ※<b>この合格は製品への配線の証拠ではない</b>。製品（<c>FleetMorale</c>）は
        /// 被弾時に直接 <c>lastCombatTime</c> を更新する形で同じ意味を実現しており、
        /// この関数を呼んでいない。配線は PlayMode の <c>NaturalRecovery_*</c> が確かめる。
        /// </summary>
        [Test]
        public void TakingDamage_CountsAsCombatContact()
        {
            Assert.IsTrue(RoutRecoveryRules.IsCombatContact(engaging: false, tookDamage: true),
                "撃たれているのに非交戦と見なしている");
            Assert.IsTrue(RoutRecoveryRules.IsCombatContact(engaging: true, tookDamage: false));
            Assert.IsTrue(RoutRecoveryRules.IsCombatContact(engaging: true, tookDamage: true));
            Assert.IsFalse(RoutRecoveryRules.IsCombatContact(engaging: false, tookDamage: false));
        }

        // ===== 立ち直りの門 =====

        /// <summary>★交戦に触れている間は、どれだけ時間が経っていても立ち直らない。</summary>
        [Test]
        public void DuringCombat_NeverRecovers()
        {
            Assert.IsFalse(RoutRecoveryRules.CanRecover(combatContact: true, secondsSinceCombat: 0f, Delay));
            Assert.IsFalse(RoutRecoveryRules.CanRecover(combatContact: true, secondsSinceCombat: 100f, Delay),
                "交戦中なのに立ち直っている（被弾しながら敗走が解ける）");
        }

        /// <summary>待ち時間の境界（未満は不可・ちょうどと超過は可）。</summary>
        [Test]
        public void AfterCombat_RecoversOnlyPastTheDelay()
        {
            Assert.IsFalse(RoutRecoveryRules.CanRecover(false, 0f, Delay));
            Assert.IsFalse(RoutRecoveryRules.CanRecover(false, Delay - 0.01f, Delay), "待ち時間の手前で立ち直った");
            Assert.IsTrue(RoutRecoveryRules.CanRecover(false, Delay, Delay));
            Assert.IsTrue(RoutRecoveryRules.CanRecover(false, Delay + 1f, Delay));
        }

        /// <summary>
        /// ★実機で起きた並び：被弾で敗走 → 直後は交戦中なので立ち直らない →
        /// 攻撃が途切れて待ち時間を満たしたら立ち直る。
        /// </summary>
        [Test]
        public void Scenario_NoImmediateRecoveryWhileUnderFire()
        {
            // 敗走した瞬間（被弾したフレーム）。経過0秒・交戦中。
            Assert.IsFalse(RoutRecoveryRules.CanRecover(
                RoutRecoveryRules.IsCombatContact(engaging: false, tookDamage: true), 0f, Delay),
                "敗走した次のフレームに立ち直っている（実機の反復）");

            // 撃たれ続けている間（時間は経っても被弾が続く＝経過は数え直される）。
            Assert.IsFalse(RoutRecoveryRules.CanRecover(
                RoutRecoveryRules.IsCombatContact(false, tookDamage: true), 0f, Delay));

            // 攻撃が止んで待ち時間を満たした。
            Assert.IsTrue(RoutRecoveryRules.CanRecover(
                RoutRecoveryRules.IsCombatContact(false, tookDamage: false), Delay, Delay),
                "攻撃が止んで待ち時間を満たしても立ち直らない（回復が壊れている）");
        }

        /// <summary>待ち時間が0や負でも壊れない（交戦中は依然として立ち直らない）。</summary>
        [Test]
        public void Delay_IsClampedAndCombatStillWins()
        {
            Assert.IsTrue(RoutRecoveryRules.CanRecover(false, 0f, 0f));
            Assert.IsTrue(RoutRecoveryRules.CanRecover(false, 0f, -5f));
            Assert.IsFalse(RoutRecoveryRules.CanRecover(true, 0f, -5f), "交戦中は待ち時間に関係なく立ち直らない");
        }
    }
}
