using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>経済・民心 → 安定度の橋（創発ループ配線）：高税/債務/低民心が安定度を下げ、繁栄は上げることを固定する。</summary>
    public class GovernanceEconomyRulesTests
    {
        private static FactionState State(float hope, float taxRate, float debt)
        {
            var fs = new FactionState(Faction.帝国, 0.5f);
            fs.community.hope = hope;
            fs.taxRate = taxRate;
            fs.fiscal.debt = debt;
            return fs;
        }

        [Test]
        public void Prosperity_RaisesStability()
        {
            // 高民心・低税・無借金 → 安定度へプラス。
            Assert.Greater(GovernanceEconomyRules.StabilityModifier(State(1f, 0.3f, 0f)), 0f);
        }

        [Test]
        public void HighTaxDebtLowHope_LowersStability()
        {
            // 低民心0.1・高税0.8・過大債務600 → 大きくマイナス（反乱誘発の主因）。
            float mod = GovernanceEconomyRules.StabilityModifier(State(0.1f, 0.8f, 600f));
            Assert.Less(mod, -20f);
        }

        [Test]
        public void Tax_BelowFreeline_NoPenalty()
        {
            // 税が無料ライン(0.3)以下なら税ペナルティは無し（民心の寄与のみ）。
            var prm = GovernanceEconomyRules.EconomyStabilityParams.Default;
            float modLowTax = GovernanceEconomyRules.StabilityModifier(State(0.5f, 0.2f, 0f), prm);
            float modFreeTax = GovernanceEconomyRules.StabilityModifier(State(0.5f, 0.3f, 0f), prm);
            Assert.AreEqual(modFreeTax, modLowTax, 1e-4f); // 0.2 と 0.3 は同じ（どちらも無罰）
            Assert.AreEqual(0f, modFreeTax, 1e-4f);        // hope=0.5 中立・税0.3・無借金 → 0
        }

        [Test]
        public void HigherTax_LowersMore()
        {
            float t30 = GovernanceEconomyRules.StabilityModifier(State(0.5f, 0.3f, 0f));
            float t60 = GovernanceEconomyRules.StabilityModifier(State(0.5f, 0.6f, 0f));
            Assert.Less(t60, t30);
        }

        [Test]
        public void DebtSpike_AddsPenaltyAboveTolerance()
        {
            float below = GovernanceEconomyRules.StabilityModifier(State(0.5f, 0.3f, 400f));  // <500
            float above = GovernanceEconomyRules.StabilityModifier(State(0.5f, 0.3f, 600f));  // >500
            Assert.AreEqual(0f, below, 1e-4f);
            Assert.Less(above, below);
        }

        [Test]
        public void Null_IsNeutral()
        {
            Assert.AreEqual(0f, GovernanceEconomyRules.StabilityModifier(null), 1e-4f);
        }
    }
}
