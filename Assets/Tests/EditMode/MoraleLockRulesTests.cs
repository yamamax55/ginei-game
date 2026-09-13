using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 不退転（#2175）と敗走判定の決まり。
    ///
    /// 実機で「不退転中に敗走／解除が1フレーム間隔で反復する」現象が出た。
    /// 原因は<b>更新順への依存</b>（被弾はフレーム中のいつでも来るのに、
    /// 士気を戻すのは1フレームに1回だった）なので、
    /// ここでは<b>順序に依らないこと</b>を中心に固定する。
    /// </summary>
    public class MoraleLockRulesTests
    {
        // ===== 有効な敗走状態 =====

        /// <summary>★効果中は士気が尽きていても敗走ではない（いつ読んでも同じ）。</summary>
        [Test]
        public void Locked_IsNeverRouted()
        {
            Assert.IsFalse(MoraleLockRules.IsRouted(0f, moraleLock: true));
            Assert.IsFalse(MoraleLockRules.IsRouted(-50f, moraleLock: true), "負の士気でも敗走にしない");
            Assert.IsFalse(MoraleLockRules.IsRouted(1f, moraleLock: true));
        }

        /// <summary>効果が無ければ従来どおり＝士気0以下で敗走（通常の敗走を弱めない）。</summary>
        [Test]
        public void Unlocked_KeepsNormalRout()
        {
            Assert.IsTrue(MoraleLockRules.IsRouted(0f, moraleLock: false));
            Assert.IsTrue(MoraleLockRules.IsRouted(-1f, moraleLock: false));
            Assert.IsFalse(MoraleLockRules.IsRouted(0.1f, moraleLock: false));
            Assert.IsFalse(MoraleLockRules.IsRouted(100f, moraleLock: false));
        }

        // ===== 下限 =====

        [Test]
        public void Floor_IsOneWhileLockedAndZeroOtherwise()
        {
            Assert.AreEqual(MoraleLockRules.LockedFloor, MoraleLockRules.Floor(true), 1e-4f);
            Assert.AreEqual(0f, MoraleLockRules.Floor(false), 1e-4f);
            Assert.Greater(MoraleLockRules.LockedFloor, 0f, "下限が0だと敗走判定に落ちてしまう");
        }

        /// <summary>
        /// ★効果中は<b>何回どんな順で被弾しても</b>下限を割らない
        /// ＝敗走判定に落ちる瞬間が作られない（今回の反復の芯）。
        /// </summary>
        [Test]
        public void Locked_RepeatedDamage_NeverFallsBelowFloor()
        {
            float morale = 100f;
            float[] hits = { -30f, -5f, -80f, -0.5f, -200f, -1f, -12f };

            for (int i = 0; i < hits.Length; i++)
            {
                morale = MoraleLockRules.Clamp(morale, hits[i], 100f, moraleLock: true);
                Assert.GreaterOrEqual(morale, MoraleLockRules.LockedFloor,
                    "被弾 " + i + " 回目で下限を割った");
                Assert.IsFalse(MoraleLockRules.IsRouted(morale, true),
                    "被弾 " + i + " 回目で敗走になった（1フレーム反復の原因）");
            }
        }

        /// <summary>被弾の順番を入れ替えても結果が同じ（順序に依存しない）。</summary>
        [Test]
        public void Locked_ResultIsOrderIndependent()
        {
            float a = 100f;
            a = MoraleLockRules.Clamp(a, -90f, 100f, true);
            a = MoraleLockRules.Clamp(a, -20f, 100f, true);

            float b = 100f;
            b = MoraleLockRules.Clamp(b, -20f, 100f, true);
            b = MoraleLockRules.Clamp(b, -90f, 100f, true);

            Assert.AreEqual(a, b, 1e-4f);
            Assert.AreEqual(MoraleLockRules.LockedFloor, a, 1e-4f);
        }

        /// <summary>効果が無ければ従来どおり0まで落ちて敗走する。</summary>
        [Test]
        public void Unlocked_DamageStillReachesRout()
        {
            float morale = MoraleLockRules.Clamp(50f, -80f, 100f, moraleLock: false);
            Assert.AreEqual(0f, morale, 1e-4f);
            Assert.IsTrue(MoraleLockRules.IsRouted(morale, false), "通常の敗走が起きなくなっている");
        }

        [Test]
        public void Clamp_RespectsMaximum()
        {
            Assert.AreEqual(100f, MoraleLockRules.Clamp(90f, 50f, 100f, false), 1e-4f);
            Assert.AreEqual(100f, MoraleLockRules.Clamp(90f, 50f, 100f, true), 1e-4f);
        }

        /// <summary>上限が下限より小さい壊れた設定でも反転しない（null安全の類）。</summary>
        [Test]
        public void Clamp_DoesNotInvertWhenMaxIsTiny()
        {
            float m = MoraleLockRules.Clamp(0.5f, -10f, 0.5f, moraleLock: true);
            Assert.GreaterOrEqual(m, 0f);
            Assert.LessOrEqual(m, 1f);
        }

        // ===== 効果終了 =====

        /// <summary>
        /// ★効果が切れただけでは敗走にしない（下限で踏みとどまった状態から再開）。
        /// 以後さらに被弾すれば通常どおり敗走する。
        /// </summary>
        [Test]
        public void LockExpiry_DoesNotRoutByItself()
        {
            // 効果中に削られて下限で止まっている。
            float morale = MoraleLockRules.Clamp(100f, -500f, 100f, moraleLock: true);
            Assert.AreEqual(MoraleLockRules.LockedFloor, morale, 1e-4f);

            // 効果終了。
            morale = MoraleLockRules.OnLockExpired(morale);
            Assert.IsFalse(MoraleLockRules.IsRouted(morale, moraleLock: false),
                "効果が切れた瞬間に敗走している");

            // そのあと被弾すれば通常どおり敗走する。
            morale = MoraleLockRules.Clamp(morale, -5f, 100f, moraleLock: false);
            Assert.IsTrue(MoraleLockRules.IsRouted(morale, moraleLock: false),
                "効果終了後に通常の敗走が起きない");
        }

        /// <summary>
        /// 軍団の総退却は兵力比でも起きるので、不退転で敗走しなくても判断は働く
        /// （優先順位を弱めていないことの確認）。
        /// </summary>
        [Test]
        public void CorpsRetreat_StillTriggersByStrengthRatioWhileLocked()
        {
            // 不退転中＝指揮官は敗走していない。
            bool commanderRouted = MoraleLockRules.IsRouted(0f, moraleLock: true);
            Assert.IsFalse(commanderRouted);

            // それでも兵力比が落ちていれば総退却は発令される。
            Assert.IsTrue(CorpsRetreatRules.ShouldOrderRetreat(0.05f, commanderRouted, 50, 50),
                "不退転が軍団総退却まで止めてしまっている");

            // 兵力が十分なら発令されない（当たり前の側も固定）。
            Assert.IsFalse(CorpsRetreatRules.ShouldOrderRetreat(0.9f, commanderRouted, 50, 50));
        }
    }
}
