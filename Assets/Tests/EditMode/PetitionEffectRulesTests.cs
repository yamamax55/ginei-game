using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>具申採用時の効果適用：種別判定・予算枠加算・根拠地設定・未知キーは無効。</summary>
    public class PetitionEffectRulesTests
    {
        [Test]
        public void Classify_ByEffectKey()
        {
            Assert.AreEqual(PetitionEffect.予算増額, PetitionEffectRules.Classify("fleet.budget:0:5000"));
            Assert.AreEqual(PetitionEffect.根拠地変更, PetitionEffectRules.Classify("fleet.base:0:-1"));
            Assert.AreEqual(PetitionEffect.なし, PetitionEffectRules.Classify("career.petition"));
            Assert.AreEqual(PetitionEffect.なし, PetitionEffectRules.Classify(null));
        }

        [Test]
        public void Apply_Budget_AccumulatesGrant()
        {
            var g = new PetitionGrants();
            Assert.AreEqual(PetitionEffect.予算増額, PetitionEffectRules.Apply(g, "fleet.budget:0:5000"));
            Assert.AreEqual(5000, g.fleetBudgetGrant);
            // 重ねて採用＝累積。
            PetitionEffectRules.Apply(g, "fleet.budget:0:3000");
            Assert.AreEqual(8000, g.fleetBudgetGrant);
        }

        [Test]
        public void Apply_Base_SetsSystem()
        {
            var g = new PetitionGrants();
            Assert.AreEqual(PetitionEffect.根拠地変更, PetitionEffectRules.Apply(g, "fleet.base:0:12"));
            Assert.AreEqual(12, g.fleetBaseSystemId);
            // 一任（-1）も設定できる。
            PetitionEffectRules.Apply(g, "fleet.base:0:-1");
            Assert.AreEqual(-1, g.fleetBaseSystemId);
        }

        [Test]
        public void Apply_Unknown_NoChange()
        {
            var g = new PetitionGrants { fleetBudgetGrant = 100, fleetBaseSystemId = 5 };
            Assert.AreEqual(PetitionEffect.なし, PetitionEffectRules.Apply(g, "career.petition"));
            Assert.AreEqual(100, g.fleetBudgetGrant);
            Assert.AreEqual(5, g.fleetBaseSystemId);
            Assert.AreEqual(PetitionEffect.なし, PetitionEffectRules.Apply(null, "fleet.budget:0:5000")); // null安全
        }
    }
}
