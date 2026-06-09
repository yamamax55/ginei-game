using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国家状態↔諸侯の忠誠の連結（社会シミュ ↔ 関ヶ原 #817）を固定する：
    /// 腐った国は諸侯の基準忠誠が低く調略に弱い＝武力でなく寝返りやすさで会戦に負ける。
    /// </summary>
    public class FactionLoyaltyRulesTests
    {
        private static FactionState State(float legit, float cohesion, float hope)
        {
            var s = new FactionState(Faction.帝国);
            s.regime.legitimacy = legit;
            s.organization.cohesion = cohesion;
            s.community.hope = hope;
            return s;
        }

        [Test]
        public void HealthyState_HighLoyalty_LowBribeSusceptibility()
        {
            var s = State(1f, 1f, 1f);
            Assert.AreEqual(1f, FactionLoyaltyRules.BaselineLoyalty(s), 1e-4f);
            Assert.AreEqual(0f, FactionLoyaltyRules.BribeSusceptibility(s), 1e-4f);
        }

        [Test]
        public void RottenState_LowLoyalty_HighBribeSusceptibility()
        {
            var s = State(0.2f, 0.3f, 0.1f);
            Assert.AreEqual(0.2f, FactionLoyaltyRules.BaselineLoyalty(s), 1e-4f); // (0.2+0.3+0.1)/3
            Assert.AreEqual(0.8f, FactionLoyaltyRules.BribeSusceptibility(s), 1e-4f);
        }

        [Test]
        public void ApplyBaseline_SetsAllegianceLoyalty()
        {
            var s = State(0.6f, 0.6f, 0.6f);
            var a = new Allegiance(1, Faction.帝国, 1000, loyalty: 1f);
            FactionLoyaltyRules.ApplyBaseline(a, s);
            Assert.AreEqual(0.6f, a.loyalty, 1e-4f);
        }

        [Test]
        public void RottenFaction_LosesSekigahara_ToBribery()
        {
            // 健全な帝国の核(忠誠由来高) vs 腐った同盟の大兵力（基準忠誠が低く同じ調略で寝返る）
            var healthy = State(1f, 1f, 1f);   // 帝国
            var rotten = State(0.2f, 0.3f, 0.1f); // 同盟（腐敗）

            var imp = new Allegiance(1, Faction.帝国, 20000);
            FactionLoyaltyRules.ApplyBaseline(imp, healthy);   // loyalty 1.0 → 戦う

            var ally = new Allegiance(2, Faction.同盟, 40000) { intrigue = 0.5f };
            FactionLoyaltyRules.ApplyBaseline(ally, rotten);   // loyalty 0.2 → net -0.3 → 調略で寝返る

            var list = new List<Allegiance> { imp, ally };
            var winner = LoyaltyRules.ResolveWinner(list, Faction.帝国, Faction.同盟, out int effImp, out int effAlly);

            Assert.AreEqual(Faction.帝国, winner);          // 兵力で劣る帝国が、相手の腐敗ゆえに勝つ
            Assert.AreEqual(Stance.寝返り, ally.stance);     // 腐った国の大兵力が寝返る
            Assert.AreEqual(60000, effImp);                 // 2万＋寝返り4万
            Assert.AreEqual(0, effAlly);
        }
    }
}
