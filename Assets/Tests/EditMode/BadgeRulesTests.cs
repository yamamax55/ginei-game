using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>徽章システム：階級/兵科/技能章の導出・認知名誉・明示付与（部隊章）台帳。</summary>
    public class BadgeRulesTests
    {
        [TearDown]
        public void Cleanup() => BadgeRegistry.Clear();

        [Test]
        public void Derive_RankBranchAndQualifications()
        {
            // 軍人・中将(7)・SOF・参謀 → 階級章+兵科章+特殊作戦徽章+参謀徽章＝4枚。
            var b = BadgeRules.Derive(7, PersonRole.軍人, isSpecialForces: true, isStaff: true);
            Assert.AreEqual(4, b.Count);
            Assert.IsTrue(b.Exists(x => x.kind == BadgeKind.階級章 && x.tier == 7));
            Assert.IsTrue(b.Exists(x => x.kind == BadgeKind.兵科章));
            Assert.IsTrue(b.Exists(x => x.kind == BadgeKind.技能章 && x.name == "特殊作戦徽章"));
            Assert.IsTrue(b.Exists(x => x.kind == BadgeKind.技能章 && x.name == "参謀徽章"));

            // 資格なし軍人 → 階級章＋兵科章のみ。
            var plain = BadgeRules.Derive(6, PersonRole.軍人, false, false);
            Assert.AreEqual(2, plain.Count);
            // 文民は兵科章が行政章。
            var civ = BadgeRules.Derive(0, PersonRole.文民, false, false);
            Assert.IsFalse(civ.Exists(x => x.kind == BadgeKind.階級章)); // tier0は階級章なし
            Assert.IsTrue(civ.Exists(x => x.name == "行政章"));
        }

        [Test]
        public void RecognitionPrestige_CountsSkillBadges_Capped()
        {
            Assert.AreEqual(0f, BadgeRules.RecognitionPrestige(null), 1e-4f);
            var two = BadgeRules.Derive(8, PersonRole.軍人, true, true); // 技能章2
            Assert.AreEqual(2 * BadgeRules.RecognitionPerSkillBadge, BadgeRules.RecognitionPrestige(two), 1e-4f);
            // 多数でも上限
            var many = new List<Badge>();
            for (int i = 0; i < 10; i++) many.Add(BadgeRules.SkillInsignia(SkillBadge.操艦));
            Assert.AreEqual(BadgeRules.MaxRecognition, BadgeRules.RecognitionPrestige(many), 1e-4f);
        }

        [Test]
        public void Registry_GrantUnitBadge_AndAllBadges()
        {
            BadgeRegistry.GrantUnitBadge(5, "黒色槍騎兵艦隊");
            Assert.AreEqual(1, BadgeRegistry.GrantedCount(5));
            // 導出（階級+兵科+特殊作戦）＋付与（部隊章）＝4
            var all = BadgeRegistry.AllBadges(5, 8, PersonRole.軍人, isSpecialForces: true, isStaff: false);
            Assert.AreEqual(4, all.Count);
            Assert.IsTrue(all.Exists(x => x.kind == BadgeKind.部隊章 && x.name == "黒色槍騎兵艦隊"));

            BadgeRegistry.Clear();
            Assert.AreEqual(0, BadgeRegistry.GrantedCount(5));
        }
    }
}
