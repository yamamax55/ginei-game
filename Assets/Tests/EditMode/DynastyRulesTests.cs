using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 王朝サイクル＝天命と易姓革命（孔子 #867・転換エンジン #801/#823・日常化 #814）を固定する：
    /// 腐敗が正統性を蝕み（徳で減速）、天命喪失/改革者発火、制度更新で回復、易姓革命で交代。
    /// </summary>
    public class DynastyRulesTests
    {
        private static readonly DynastyParams P = DynastyParams.Default; // rate 0.1 / mandate 0.3 / reform 0.6

        [Test]
        public void Corruption_Rises_Legitimacy_Falls()
        {
            var r = new Regime(1, Faction.帝国, legitimacy: 1f, corruption: 0f, virtue: 0f);
            DynastyRules.Tick(r, 1f, P); // rise = 0.1*(1-0)*1
            Assert.AreEqual(0.1f, r.corruption, 1e-4f);
            Assert.AreEqual(0.9f, r.legitimacy, 1e-4f);
        }

        [Test]
        public void Virtue_SlowsCorruption()
        {
            var r = new Regime(1, Faction.帝国, virtue: 0.9f);
            DynastyRules.Tick(r, 1f, P); // rise = 0.1*0.1 = 0.01
            Assert.AreEqual(0.01f, r.corruption, 1e-4f);
        }

        [Test]
        public void MandateLost_And_ReformerArises_OverTime()
        {
            var r = new Regime(1, Faction.帝国, virtue: 0f);
            for (int i = 0; i < 8; i++) DynastyRules.Tick(r, 1f, P); // corruption 0.8, legitimacy 0.2
            Assert.AreEqual(0.8f, r.corruption, 1e-3f);
            Assert.IsTrue(DynastyRules.MandateLost(r, P));    // 正統性0.2 < 0.3
            Assert.IsTrue(DynastyRules.ReformerArises(r, P)); // 腐敗0.8 >= 0.6
        }

        [Test]
        public void Reform_Restores()
        {
            var r = new Regime(1, Faction.帝国, legitimacy: 0.3f, corruption: 0.7f);
            DynastyRules.Reform(r, 0.5f);
            Assert.AreEqual(0.2f, r.corruption, 1e-4f);
            Assert.AreEqual(0.8f, r.legitimacy, 1e-4f);
            Assert.IsFalse(DynastyRules.ReformerArises(r, P));
        }

        [Test]
        public void Revolution_Replaces_Dynasty()
        {
            var r = new Regime(1, Faction.帝国, legitimacy: 0.1f, corruption: 0.9f, virtue: 0.2f);
            DynastyRules.Revolution(r, Faction.同盟, 0.7f);
            Assert.AreEqual(Faction.同盟, r.faction);
            Assert.AreEqual(1f, r.legitimacy, 1e-4f);
            Assert.AreEqual(0f, r.corruption, 1e-4f);
            Assert.AreEqual(0.7f, r.virtue, 1e-4f);
            Assert.IsFalse(DynastyRules.MandateLost(r, P));
        }
    }
}
