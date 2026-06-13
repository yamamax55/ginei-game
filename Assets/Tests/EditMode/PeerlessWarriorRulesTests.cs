using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>日本一の兵（真田幸村）：常時武勇・真田丸の堅守（寡兵防衛）・決死の突撃（窮地ほど苛烈）。</summary>
    public class PeerlessWarriorRulesTests
    {
        [Test]
        public void ValorFactor_AlwaysStrong()
        {
            Assert.AreEqual(1.15f, PeerlessWarriorRules.ValorFactor(true), 1e-4f);
            Assert.AreEqual(1.0f, PeerlessWarriorRules.ValorFactor(false), 1e-4f);
        }

        [Test]
        public void Fortified_SanadaMaru_TougherWhenOutnumbered()
        {
            Assert.AreEqual(0.85f, PeerlessWarriorRules.FortifiedDamageTakenFactor(true, true, 1000f, 1000f), 1e-4f); // 互角でも堅い
            Assert.AreEqual(0.6f, PeerlessWarriorRules.FortifiedDamageTakenFactor(true, true, 1000f, 2000f), 1e-4f);  // 2:1
            Assert.AreEqual(0.5f, PeerlessWarriorRules.FortifiedDamageTakenFactor(true, true, 1000f, 3000f), 1e-4f);  // 3:1（下限クランプ）
            Assert.AreEqual(1.0f, PeerlessWarriorRules.FortifiedDamageTakenFactor(true, false, 1000f, 3000f), 1e-4f); // 守勢でない
            Assert.AreEqual(1.0f, PeerlessWarriorRules.FortifiedDamageTakenFactor(false, true, 1000f, 3000f), 1e-4f); // 並
        }

        [Test]
        public void DeathCharge_FiercerWhenDesperate()
        {
            Assert.AreEqual(1.0f, PeerlessWarriorRules.DeathChargeFactor(true, 1.0f), 1e-4f);  // 満身＝上乗せ無し
            Assert.AreEqual(1.25f, PeerlessWarriorRules.DeathChargeFactor(true, 0.5f), 1e-4f);
            Assert.AreEqual(1.5f, PeerlessWarriorRules.DeathChargeFactor(true, 0.0f), 1e-4f);  // 壊滅寸前の死兵（夏の陣）
            Assert.AreEqual(1.0f, PeerlessWarriorRules.DeathChargeFactor(false, 0.0f), 1e-4f); // 並
        }
    }
}
