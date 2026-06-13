using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>革新者（織田信長）：若年うつけ（過小評価）・開花・先見性（技術採用）・新技術優位（長篠）。</summary>
    public class InnovatorRulesTests
    {
        [Test]
        public void Utsuke_PerceivedLowWhileYoung_TrueAbilityUnchanged()
        {
            // 若き革新者は実力90でも周囲には45（大うつけ）に見える。
            Assert.AreEqual(45, InnovatorRules.PerceivedAbility(90, 16, true));
            // 開花後は実力どおり。
            Assert.AreEqual(90, InnovatorRules.PerceivedAbility(90, 25, true));
            Assert.AreEqual(90, InnovatorRules.PerceivedAbility(90, 20, true)); // 開花年齢ちょうど
            // 並の者は若くても侮られない。
            Assert.AreEqual(90, InnovatorRules.PerceivedAbility(90, 16, false));

            Assert.IsTrue(InnovatorRules.IsUnderestimated(true, 16));
            Assert.IsFalse(InnovatorRules.IsUnderestimated(true, 20));
            Assert.IsFalse(InnovatorRules.IsUnderestimated(false, 16));

            Assert.IsFalse(InnovatorRules.HasBloomed(true, 16));
            Assert.IsTrue(InnovatorRules.HasBloomed(true, 20));
            Assert.IsTrue(InnovatorRules.HasBloomed(false, 16));
        }

        [Test]
        public void Foresight_TechAdoption()
        {
            Assert.AreEqual(1.5f, InnovatorRules.TechAdoptionFactor(true, 100), 1e-4f);
            Assert.AreEqual(1.25f, InnovatorRules.TechAdoptionFactor(true, 50), 1e-4f);
            Assert.AreEqual(1.0f, InnovatorRules.TechAdoptionFactor(false, 100), 1e-4f);
        }

        [Test]
        public void NewTechAdvantage_RewardsBeingAhead()
        {
            Assert.AreEqual(1.3f, InnovatorRules.NewTechAdvantage(true, 5, 2), 1e-4f); // 技術差3
            Assert.AreEqual(1.5f, InnovatorRules.NewTechAdvantage(true, 5, 0), 1e-4f); // 上限クランプ
            Assert.AreEqual(1.5f, InnovatorRules.NewTechAdvantage(true, 8, 0), 1e-4f); // クランプ
            Assert.AreEqual(1.0f, InnovatorRules.NewTechAdvantage(true, 2, 5), 1e-4f); // 後れていれば優位なし
            Assert.AreEqual(1.0f, InnovatorRules.NewTechAdvantage(false, 5, 0), 1e-4f); // 並は新技術を活かせない
        }
    }
}
