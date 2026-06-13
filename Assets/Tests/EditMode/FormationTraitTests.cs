using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>陣形の戦術特性（#72・会戦改善）：史実ベースのメリット/デメリット。</summary>
    public class FormationTraitTests
    {
        [Test]
        public void Traits_Values()
        {
            Assert.AreEqual(1.25f, FormationTraitRules.AttackFactor(Formation.横陣), 1e-4f);   // 最大火力
            Assert.AreEqual(0.80f, FormationTraitRules.AttackFactor(Formation.円陣), 1e-4f);   // 火力低
            Assert.AreEqual(0.80f, FormationTraitRules.DamageTakenFactor(Formation.円陣), 1e-4f); // 全周防御で堅い
            Assert.AreEqual(1.15f, FormationTraitRules.DamageTakenFactor(Formation.鶴翼陣), 1e-4f); // 中央薄く脆い
            Assert.AreEqual(1.15f, FormationTraitRules.MobilityFactor(Formation.紡錘陣), 1e-4f); // 機動高
            Assert.AreEqual(0.80f, FormationTraitRules.MobilityFactor(Formation.方陣), 1e-4f);  // 鈍重
        }

        [Test]
        public void Traits_Relationships()
        {
            // 横陣は円陣より火力が高い（火力特化 vs 守勢）
            Assert.Greater(FormationTraitRules.AttackFactor(Formation.横陣), FormationTraitRules.AttackFactor(Formation.円陣));
            // 円陣・方陣は紡錘陣より被ダメが小さい（守勢は堅い・突撃は脆い）
            Assert.Less(FormationTraitRules.DamageTakenFactor(Formation.円陣), FormationTraitRules.DamageTakenFactor(Formation.紡錘陣));
            Assert.Less(FormationTraitRules.DamageTakenFactor(Formation.方陣), FormationTraitRules.DamageTakenFactor(Formation.横陣));
            // 紡錘陣は方陣より機動が高い（突撃 vs 鈍重）
            Assert.Greater(FormationTraitRules.MobilityFactor(Formation.紡錘陣), FormationTraitRules.MobilityFactor(Formation.方陣));
        }
    }
}
