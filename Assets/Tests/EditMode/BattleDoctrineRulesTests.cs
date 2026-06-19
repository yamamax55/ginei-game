using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦術ドクトリン（BattleDoctrine・#2365 STSH-5）の係数の順序/境界を固定する。
    /// 集中＝攻撃↑・集中リスク↑／分散＝生存性↑・集中リスク↓／機動＝機動↑。すべて 1.0 中心の小さな振れ。
    /// </summary>
    public class BattleDoctrineRulesTests
    {
        private const float Eps = 1e-4f;

        // 各係数の妥当な振れ幅（1.0 中心）。
        private const float MinFactor = 0.85f;
        private const float MaxFactor = 1.25f;

        [Test]
        public void AttackFactor_Concentrated_IsHighest()
        {
            float c = BattleDoctrineRules.AttackFactor(BattleDoctrine.集中);
            float d = BattleDoctrineRules.AttackFactor(BattleDoctrine.分散);
            float m = BattleDoctrineRules.AttackFactor(BattleDoctrine.機動);
            Assert.Greater(c, d);
            Assert.Greater(c, m);
            Assert.Greater(c, 1f); // 集中は攻撃↑
        }

        [Test]
        public void SurvivabilityFactor_Dispersed_IsHighest()
        {
            float c = BattleDoctrineRules.SurvivabilityFactor(BattleDoctrine.集中);
            float d = BattleDoctrineRules.SurvivabilityFactor(BattleDoctrine.分散);
            float m = BattleDoctrineRules.SurvivabilityFactor(BattleDoctrine.機動);
            Assert.Greater(d, c);
            Assert.Greater(d, m);
            Assert.Less(c, 1f); // 集中は脆い
        }

        [Test]
        public void ConcentrationRisk_Concentrated_Highest_Dispersed_Lowest()
        {
            float c = BattleDoctrineRules.ConcentrationRisk(BattleDoctrine.集中);
            float d = BattleDoctrineRules.ConcentrationRisk(BattleDoctrine.分散);
            float m = BattleDoctrineRules.ConcentrationRisk(BattleDoctrine.機動);
            Assert.Greater(c, m);
            Assert.Greater(m, d);
            Assert.Greater(c, 1f); // 集中＝広域攻撃に脆い
            Assert.Less(d, 1f);    // 分散＝広域攻撃に強い
        }

        [Test]
        public void MobilityFactor_Maneuver_IsHighest()
        {
            float c = BattleDoctrineRules.MobilityFactor(BattleDoctrine.集中);
            float d = BattleDoctrineRules.MobilityFactor(BattleDoctrine.分散);
            float m = BattleDoctrineRules.MobilityFactor(BattleDoctrine.機動);
            Assert.Greater(m, c);
            Assert.Greater(m, d);
            Assert.Greater(m, 1f); // 機動は速度↑
            Assert.AreEqual(1f, c, Eps); // 集中は機動中立
        }

        [Test]
        public void AllFactors_AreCentered_AndBounded()
        {
            foreach (BattleDoctrine d in System.Enum.GetValues(typeof(BattleDoctrine)))
            {
                AssertBounded(BattleDoctrineRules.AttackFactor(d));
                AssertBounded(BattleDoctrineRules.SurvivabilityFactor(d));
                AssertBounded(BattleDoctrineRules.ConcentrationRisk(d));
                AssertBounded(BattleDoctrineRules.MobilityFactor(d));
            }
        }

        private static void AssertBounded(float f)
        {
            Assert.GreaterOrEqual(f, MinFactor);
            Assert.LessOrEqual(f, MaxFactor);
        }

        [Test]
        public void FormationCompatibility_GoodMatchups_AboveNeutral()
        {
            // 集中×紡錘陣・分散×円陣・機動×鶴翼陣 が好相性（>1.0）。
            Assert.Greater(BattleDoctrineRules.FormationCompatibility(BattleDoctrine.集中, Formation.紡錘陣), 1f);
            Assert.Greater(BattleDoctrineRules.FormationCompatibility(BattleDoctrine.分散, Formation.円陣), 1f);
            Assert.Greater(BattleDoctrineRules.FormationCompatibility(BattleDoctrine.機動, Formation.鶴翼陣), 1f);
        }

        [Test]
        public void FormationCompatibility_PoorMatchups_BelowNeutral()
        {
            // 集中×円陣・分散×紡錘陣・機動×方陣 が逆相性（<1.0）。
            Assert.Less(BattleDoctrineRules.FormationCompatibility(BattleDoctrine.集中, Formation.円陣), 1f);
            Assert.Less(BattleDoctrineRules.FormationCompatibility(BattleDoctrine.分散, Formation.紡錘陣), 1f);
            Assert.Less(BattleDoctrineRules.FormationCompatibility(BattleDoctrine.機動, Formation.方陣), 1f);
        }

        [Test]
        public void FormationCompatibility_AlwaysBounded()
        {
            foreach (BattleDoctrine d in System.Enum.GetValues(typeof(BattleDoctrine)))
            {
                foreach (Formation f in System.Enum.GetValues(typeof(Formation)))
                {
                    float v = BattleDoctrineRules.FormationCompatibility(d, f);
                    Assert.GreaterOrEqual(v, 0f);
                    Assert.LessOrEqual(v, 1.2f + Eps);
                }
            }
        }

        [Test]
        public void DoctrineModifiers_EqualsProductOfComponentFactors()
        {
            var d = BattleDoctrine.集中;
            float expected = BattleDoctrineRules.AttackFactor(d)
                           * BattleDoctrineRules.SurvivabilityFactor(d)
                           * BattleDoctrineRules.MobilityFactor(d);
            var stack = BattleDoctrineRules.DoctrineModifiers(d);
            Assert.AreEqual(expected, stack.Value, Eps);
        }

        [Test]
        public void DoctrineModifiers_Maneuver_MatchesProduct()
        {
            var d = BattleDoctrine.機動;
            float expected = BattleDoctrineRules.AttackFactor(d)
                           * BattleDoctrineRules.SurvivabilityFactor(d)
                           * BattleDoctrineRules.MobilityFactor(d);
            var stack = BattleDoctrineRules.DoctrineModifiers(d);
            Assert.AreEqual(expected, stack.Value, Eps);
        }
    }
}
