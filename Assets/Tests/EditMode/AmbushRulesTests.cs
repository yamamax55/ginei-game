using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 伏兵・奇襲を固定する：成功率＝秘匿×(1−警戒)、roll決定論、初撃倍率、隊形不備は時間で線形回復
    /// （実効戦闘倍率は下限0.5）、士気ショックは規模比例。境界を担保。
    /// </summary>
    public class AmbushRulesTests
    {
        private static readonly AmbushParams P = AmbushParams.Default;
        // 初撃1.5/回復10/ショック0.3

        [Test]
        public void AmbushChance_ConcealmentVsAlertness()
        {
            Assert.AreEqual(1f, AmbushRules.AmbushChance(1f, 0f), 1e-5f);   // 完全秘匿×無警戒
            Assert.AreEqual(0f, AmbushRules.AmbushChance(1f, 1f), 1e-5f);   // 完全警戒＝奇襲なし
            Assert.AreEqual(0f, AmbushRules.AmbushChance(0f, 0f), 1e-5f);   // 隠れてない
            Assert.AreEqual(0.25f, AmbushRules.AmbushChance(0.5f, 0.5f), 1e-5f);
        }

        [Test]
        public void IsSprung_DeterministicByRoll()
        {
            Assert.IsTrue(AmbushRules.IsSprung(0.5f, 0.5f, 0.24f));
            Assert.IsFalse(AmbushRules.IsSprung(0.5f, 0.5f, 0.26f));
        }

        [Test]
        public void FirstStrikeFactor_OnlyWhenSurprised()
        {
            Assert.AreEqual(1.5f, AmbushRules.FirstStrikeFactor(true, P), 1e-5f);
            Assert.AreEqual(1f, AmbushRules.FirstStrikeFactor(false, P), 1e-5f);
        }

        [Test]
        public void DisarrayRecovery_LinearOverDuration()
        {
            Assert.AreEqual(0f, AmbushRules.DisarrayRecovery(0f, P), 1e-5f);
            Assert.AreEqual(0.5f, AmbushRules.DisarrayRecovery(5f, P), 1e-5f);
            Assert.AreEqual(1f, AmbushRules.DisarrayRecovery(10f, P), 1e-5f);
            Assert.AreEqual(1f, AmbushRules.DisarrayRecovery(100f, P), 1e-5f); // 回復済みで頭打ち
        }

        [Test]
        public void VictimCombatFactor_FloorAtHalf()
        {
            Assert.AreEqual(0.5f, AmbushRules.VictimCombatFactor(0f, P), 1e-5f);   // 直後＝半減
            Assert.AreEqual(0.75f, AmbushRules.VictimCombatFactor(5f, P), 1e-5f);  // 回復途中
            Assert.AreEqual(1f, AmbushRules.VictimCombatFactor(10f, P), 1e-5f);    // 完全回復
        }

        [Test]
        public void MoraleShock_ScalesWithSurprise()
        {
            Assert.AreEqual(0.3f, AmbushRules.MoraleShock(1f, P), 1e-5f);
            Assert.AreEqual(0.15f, AmbushRules.MoraleShock(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, AmbushRules.MoraleShock(0f, P), 1e-5f);
        }
    }
}
