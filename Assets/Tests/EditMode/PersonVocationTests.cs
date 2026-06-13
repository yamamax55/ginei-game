using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ネームド人物の職分（POP 職業#110 とは別管理・<see cref="PersonVocationRules"/>）：役割からの職分導出（君主含む）／
    /// 君主判定／JSOC アナログ（分析用）／POP→ネームド昇格の経路（君主には到達しない）。
    /// </summary>
    public class PersonVocationTests
    {
        [Test]
        public void VocationOf_Sovereign_IsRuler()
        {
            var king = new Person(1, "皇帝", Faction.帝国, PersonRole.軍人) { isSovereign = true, leadership = 90 };
            Assert.AreEqual(PersonVocation.君主, PersonVocationRules.VocationOf(king));
            Assert.IsTrue(PersonVocationRules.IsRuler(king));
            // 君主は POP の JSOC 分類には載らない地位＝別管理（PersonVocation 側で扱う）
        }

        [Test]
        public void VocationOf_Politician_Military_Civil()
        {
            var politician = new Person(2, "議員", Faction.同盟, PersonRole.文民) { isPolitician = true };
            var admiral = new Person(3, "提督", Faction.帝国, PersonRole.軍人) { leadership = 80 };
            var technocrat = new Person(4, "技術士官", Faction.同盟, PersonRole.文民)
            { research = 80, engineering = 80, planning = 60, production = 60, operation = 20, intelligence = 20 };
            var clerk = new Person(5, "行政官", Faction.同盟, PersonRole.文民) { operation = 70, intelligence = 70 };

            Assert.AreEqual(PersonVocation.政治家, PersonVocationRules.VocationOf(politician));
            Assert.AreEqual(PersonVocation.武官,   PersonVocationRules.VocationOf(admiral));
            Assert.AreEqual(PersonVocation.技術者, PersonVocationRules.VocationOf(technocrat));
            Assert.AreEqual(PersonVocation.文官,   PersonVocationRules.VocationOf(clerk));
            Assert.IsFalse(PersonVocationRules.IsRuler(admiral));
        }

        [Test]
        public void SovereignTakesPrecedence()
        {
            // 元首はたとえ政治家フラグや軍人でも君主が優先
            var p = new Person(6, "覇王", Faction.帝国, PersonRole.軍人) { isSovereign = true, isPolitician = true };
            Assert.AreEqual(PersonVocation.君主, PersonVocationRules.VocationOf(p));
        }

        [Test]
        public void JsocAnalog_ForAnalyticsOnly()
        {
            Assert.AreEqual(OccupationCategory.管理,     PersonVocationRules.JsocAnalog(PersonVocation.政治家));
            Assert.AreEqual(OccupationCategory.管理,     PersonVocationRules.JsocAnalog(PersonVocation.君主));   // 別格だが便宜上 管理
            Assert.AreEqual(OccupationCategory.事務,     PersonVocationRules.JsocAnalog(PersonVocation.文官));
            Assert.AreEqual(OccupationCategory.保安,     PersonVocationRules.JsocAnalog(PersonVocation.武官));
            Assert.AreEqual(OccupationCategory.専門技術, PersonVocationRules.JsocAnalog(PersonVocation.技術者));
            Assert.AreEqual(OccupationCategory.無職,     PersonVocationRules.JsocAnalog(PersonVocation.その他)); // 対応なし
        }

        // --- POP→ネームド昇格の経路は残す ---
        [Test]
        public void PromotionVocation_FromPopPools()
        {
            Assert.AreEqual(PersonVocation.武官,   PersonVocationRules.PromotionVocation(Occupation.軍属)); // 兵→武官
            Assert.AreEqual(PersonVocation.文官,   PersonVocationRules.PromotionVocation(Occupation.官吏)); // 官吏→文官
            Assert.AreEqual(PersonVocation.技術者, PersonVocationRules.PromotionVocation(Occupation.工員)); // 叩き上げ
            Assert.AreEqual(PersonVocation.技術者, PersonVocationRules.PromotionVocation(Occupation.鉱員));
            Assert.AreEqual(PersonVocation.その他, PersonVocationRules.PromotionVocation(Occupation.農民));
            Assert.AreEqual(PersonVocation.その他, PersonVocationRules.PromotionVocation(Occupation.無職)); // 在野
        }

        [Test]
        public void Promotion_NeverReachesSovereign_ButPathAlwaysOpen()
        {
            foreach (Occupation o in System.Enum.GetValues(typeof(Occupation)))
            {
                Assert.AreNotEqual(PersonVocation.君主, PersonVocationRules.PromotionVocation(o), "POP昇格で君主には到達しない（別格）");
                Assert.IsTrue(PersonVocationRules.CanPromoteToNamed(o), "どのPOP職業からも昇格の道は開いている");
            }
        }

        // --- 昇格の整合：軍属(保安POP)→武官(保安アナログ)/官吏(事務POP)→文官(事務アナログ) は系統一致 ---
        [Test]
        public void PromotionConsistency_SecurityAndClerical()
        {
            // 兵(保安)から上がった武官は JSOC アナログも保安＝系統が通る
            var v1 = PersonVocationRules.PromotionVocation(Occupation.軍属);
            Assert.AreEqual(OccupationClassificationRules.MajorGroupOf(Occupation.軍属), PersonVocationRules.JsocAnalog(v1));
            // 官吏(事務)→文官(事務アナログ)
            var v2 = PersonVocationRules.PromotionVocation(Occupation.官吏);
            Assert.AreEqual(OccupationClassificationRules.MajorGroupOf(Occupation.官吏), PersonVocationRules.JsocAnalog(v2));
        }
    }
}
