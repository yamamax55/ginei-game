using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊要塞（イゼルローン型）を固定する：シールド健全度に比例した防御倍率、シールド健在下でのみ撃てる主砲、
    /// 力攻め比に満たない難攻不落、守備残存での通過封鎖。境界・null安全を担保。
    /// </summary>
    public class FortressRulesTests
    {
        private static readonly FortressParams P = FortressParams.Default; // 防御+200%/主砲はシールド要/力攻め比5.0

        [Test]
        public void DefenseMultiplier_ScalesWithShield()
        {
            Assert.AreEqual(3f, FortressRules.DefenseMultiplier(new Fortress(100f, 50f, 1f), P), 1e-5f); // 1+2×1
            Assert.AreEqual(2f, FortressRules.DefenseMultiplier(new Fortress(100f, 50f, 0.5f), P), 1e-5f); // 1+2×0.5
            Assert.AreEqual(1f, FortressRules.DefenseMultiplier(new Fortress(100f, 50f, 0f), P), 1e-5f);   // 素
            Assert.AreEqual(1f, FortressRules.DefenseMultiplier(null, P), 1e-5f);                          // null安全
        }

        [Test]
        public void EffectiveDefense_GarrisonTimesMultiplier()
        {
            Assert.AreEqual(300f, FortressRules.EffectiveDefense(new Fortress(100f, 50f, 1f), P), 1e-4f);
        }

        [Test]
        public void MainGunDamage_NeedsShield()
        {
            // シールド健在＝威力×健全度
            Assert.AreEqual(50f, FortressRules.MainGunDamage(new Fortress(100f, 50f, 1f), P), 1e-4f);
            Assert.AreEqual(25f, FortressRules.MainGunDamage(new Fortress(100f, 50f, 0.5f), P), 1e-4f);
            // シールドダウン＝撃てない
            Assert.AreEqual(0f, FortressRules.MainGunDamage(new Fortress(100f, 50f, 0f), P), 1e-5f);
        }

        [Test]
        public void ShieldAfterHit_AbsorbsAndClamps()
        {
            Assert.AreEqual(0.7f, FortressRules.ShieldAfterHit(1f, 0.3f), 1e-5f);
            Assert.AreEqual(0f, FortressRules.ShieldAfterHit(0.2f, 0.5f), 1e-5f); // 割り込まない
        }

        [Test]
        public void BlocksPassage_WhileGarrisonAlive()
        {
            Assert.IsTrue(FortressRules.BlocksPassage(new Fortress(100f, 50f, 1f, true)));
            Assert.IsFalse(FortressRules.BlocksPassage(new Fortress(0f, 50f, 1f, true)));   // 守備全滅
            Assert.IsFalse(FortressRules.BlocksPassage(new Fortress(100f, 50f, 1f, false))); // 扼さない
            Assert.IsFalse(FortressRules.BlocksPassage(null));
        }

        [Test]
        public void CaptureFeasibleByForce_NeedsAssaultRatio()
        {
            var f = new Fortress(100f, 50f, 1f); // 実効防御=300、力攻め必要=1500
            Assert.IsFalse(FortressRules.CaptureFeasibleByForce(f, 1000f, P));
            Assert.IsTrue(FortressRules.CaptureFeasibleByForce(f, 1500f, P));
            // null要塞は力攻め可能扱い
            Assert.IsTrue(FortressRules.CaptureFeasibleByForce(null, 1f, P));
        }

        [Test]
        public void IsImpregnable_WhenForceInsufficient()
        {
            var f = new Fortress(100f, 50f, 1f);
            Assert.IsTrue(FortressRules.IsImpregnable(f, 1000f, P));  // 足りない＝難攻不落
            Assert.IsFalse(FortressRules.IsImpregnable(f, 1500f, P)); // 足りる＝落とせる
            Assert.IsFalse(FortressRules.IsImpregnable(new Fortress(0f, 50f, 1f), 1f, P)); // 守備無し
        }
    }
}
