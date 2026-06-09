using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 「権力は借り物」（ガンジー #836/#837）を固定する：実効統治力＝直接戦力＋協力×人口、
    /// 抑圧で協力が下がり閾値割れで統治不能、非協力（協力の引き上げ）で戦わずに崩壊。
    /// </summary>
    public class ConsentRulesTests
    {
        private static readonly ConsentParams P = ConsentParams.Default; // collapse 0.3 / decay 0.2 / recover 0.1

        [Test]
        public void ControlStrength_RestsOnCooperation()
        {
            // 10万の支配側 vs 3.5億の被支配者（大英帝国 vs インドの縮図）
            var p = new Polity(1, Faction.帝国, population: 350000000, rulerForce: 100000, cooperation: 1f);
            Assert.AreEqual(350100000f, ConsentRules.ControlStrength(p), 1f); // 協力1＝被支配者が統治を実行

            ConsentRules.Withdraw(p, 1f); // 全員が協力を引き上げる
            Assert.AreEqual(100000f, ConsentRules.ControlStrength(p), 1f);     // 残るは直接戦力のみ
            Assert.IsTrue(ConsentRules.IsUngovernable(p, P));                  // 戦わずに統治不能
        }

        [Test]
        public void HighOppression_ErodesCooperation_ToUngovernable()
        {
            var p = new Polity(1, Faction.帝国, 1000000, 10000, cooperation: 1f, legitimacy: 0f, oppression: 1f);
            // 抑圧1・正統性0 → 協力が 0.2/秒 で低下
            for (int i = 0; i < 4; i++) ConsentRules.Tick(p, 1f, P);
            Assert.AreEqual(0.2f, p.cooperation, 1e-3f); // 1.0 - 0.2*4
            Assert.IsTrue(ConsentRules.IsUngovernable(p, P));
        }

        [Test]
        public void Legitimacy_StabilizesOrRecovers()
        {
            // 正統性高・抑圧低 → 協力は回復（純差分 +0.1/秒）
            var p = new Polity(1, Faction.帝国, 1000000, 10000, cooperation: 0.5f, legitimacy: 1f, oppression: 0f);
            ConsentRules.Tick(p, 2f, P);
            Assert.AreEqual(0.7f, p.cooperation, 1e-3f); // 0.5 + 0.1*2
            Assert.IsFalse(ConsentRules.IsUngovernable(p, P));
        }

        [Test]
        public void Cooperation_ClampedToUnit()
        {
            var p = new Polity(1, Faction.帝国, 1000, 100, cooperation: 0.95f, legitimacy: 1f, oppression: 0f);
            ConsentRules.Tick(p, 10f, P); // 大きく回復しても 1.0 で頭打ち
            Assert.AreEqual(1f, p.cooperation, 1e-4f);
        }

        [Test]
        public void Withdraw_ClampsAtZero()
        {
            var p = new Polity(1, Faction.帝国, 1000, 100, cooperation: 0.2f);
            ConsentRules.Withdraw(p, 0.5f);
            Assert.AreEqual(0f, p.cooperation, 1e-4f);
        }
    }
}
