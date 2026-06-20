using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 世継ぎ生成（採用「主人公の死で詰まない」継承側・<see cref="ProtagonistHeirRules"/>）を固定する：
    /// 能力の平均回帰・同勢力・出自の初任段からの出仕。
    /// </summary>
    public class ProtagonistHeirRulesTests
    {
        [Test]
        public void InheritStat_RegressesTowardMean()
        {
            Assert.AreEqual(50, ProtagonistHeirRules.InheritStat(50));        // 平均はそのまま
            Assert.AreEqual(78, ProtagonistHeirRules.InheritStat(90));        // 50+40*0.7=78
            Assert.AreEqual(36, ProtagonistHeirRules.InheritStat(30));        // 50-20*0.7=36
            Assert.GreaterOrEqual(ProtagonistHeirRules.InheritStat(100), 1);
            Assert.LessOrEqual(ProtagonistHeirRules.InheritStat(100), 100);
        }

        [Test]
        public void CreateHeir_SameFaction_OriginCommissionTier()
        {
            var pre = new Person(900001, "先代", Faction.同盟, PersonRole.軍人)
            { rankTier = 8, leadership = 90, attack = 80, defense = 70, mobility = 60, operation = 50, intelligence = 40 };

            Person royalHeir = ProtagonistHeirRules.CreateHeir(pre, PersonOrigin.王家, 900001, "世継ぎ", 820);
            Assert.AreEqual(Faction.同盟, royalHeir.faction);
            Assert.AreEqual(3, royalHeir.rankTier);              // 王家＝初任段3
            Assert.AreEqual(78, royalHeir.leadership);           // 平均回帰（90→78）
            Assert.AreEqual(820, royalHeir.birthYear);

            Person nobleHeir = ProtagonistHeirRules.CreateHeir(pre, PersonOrigin.貴族, 900001, "世継ぎ", 820);
            Assert.AreEqual(2, nobleHeir.rankTier);              // 貴族＝初任段2
        }
    }
}
