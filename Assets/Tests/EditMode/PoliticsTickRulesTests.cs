using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 勢力の政治年次 Tick（政党システムの配線オーケストレータ・GOV-6 #159）を固定する：二院の選挙日程が設立され、
    /// 衆（4年）/参（3年=半数改選）の選挙が正しい年に起き、成熟した民主政治では政党制が二大政党へ収束して分断危機へ突入する
    /// （立ち上がりは一度だけ通知）。数値は PartySystemRules/ElectionScheduleRules へ委譲（二重実装しない）。
    /// </summary>
    public class PoliticsTickRulesTests
    {
        static FactionState MatureDemocracy()
        {
            var s = new FactionState(Faction.同盟);
            s.regime.legitimacy = 0.95f; s.regime.corruption = 0.05f;
            s.polity.cooperation = 0.95f; s.inclusiveness = 0.95f; // 成熟度 ≒ 0.95
            return s;
        }

        [Test]
        public void TickYear_EstablishesChambers()
        {
            var s = MatureDemocracy();
            PoliticsTickRules.TickYear(s, 800);
            Assert.IsNotNull(s.politics);
            Assert.IsNotNull(s.politics.lowerHouse); // 下院（衆議院相当）
            Assert.IsNotNull(s.politics.upperHouse); // 上院（参議院相当）
            Assert.AreEqual(LegislativeChamber.下院, s.politics.lowerHouse.chamber);
            Assert.AreEqual(LegislativeChamber.上院, s.politics.upperHouse.chamber);
        }

        [Test]
        public void TickYear_NullSafe()
        {
            Assert.DoesNotThrow(() => PoliticsTickRules.TickYear(null, 800));
            var r = PoliticsTickRules.TickYear(null, 800);
            Assert.IsFalse(r.lowerHouseElection);
            Assert.IsFalse(r.dividedCrisis);
        }

        [Test]
        public void FullPoliticalCycle_ElectionsRun_ConvergesToTwoParty_ThenDividedCrisisOnce()
        {
            var s = MatureDemocracy();
            // 多党乱立から出発（有効政党数=4）
            s.politics = new PoliticsState();
            for (int id = 1; id <= 4; id++)
                s.politics.parties.Add(new Party(id, "党" + id, Faction.同盟) { support = 0.25f });

            int firstLower = -1, firstUpper = -1, onsetCount = 0, lowerCount = 0, upperCount = 0;
            float lastEnp = 0f;

            for (int year = 800; year <= 845; year++)
            {
                var r = PoliticsTickRules.TickYear(s, year);
                if (r.lowerHouseElection) { lowerCount++; if (firstLower < 0) firstLower = year; }
                if (r.upperHouseElection) { upperCount++; if (firstUpper < 0) firstUpper = year; }
                if (r.dividedCrisisOnset) onsetCount++;
                lastEnp = r.effectiveParties;
            }

            // 衆＝4年ごと（最初は804）、参＝3年ごと（最初は803）
            Assert.AreEqual(804, firstLower);
            Assert.AreEqual(803, firstUpper);
            Assert.Greater(lowerCount, 0);
            Assert.Greater(upperCount, lowerCount); // 参（3年）の方が回数が多い

            // 成熟した民主政治＝二大政党へ収束
            Assert.Less(lastEnp, 2.3f);
            Assert.IsTrue(PartySystemRules.IsTwoPartySystem(s.politics.parties));

            // 分断危機への突入は一度だけ（通知の立ち上がり）。以後は継続。
            Assert.AreEqual(1, onsetCount);
            Assert.IsTrue(s.politics.dividedCrisisActive);
        }

        [Test]
        public void ImmatureDemocracy_NoDividedCrisis()
        {
            var s = new FactionState(Faction.同盟);
            s.regime.legitimacy = 0.3f; s.regime.corruption = 0.7f;
            s.polity.cooperation = 0.3f; s.inclusiveness = 0.2f; // 成熟度 ≒ 0.275
            s.politics = new PoliticsState();
            // 二大政党でも、未成熟なら分断危機にならない
            s.politics.parties.Add(new Party(1, "A", Faction.同盟) { support = 0.5f });
            s.politics.parties.Add(new Party(2, "B", Faction.同盟) { support = 0.5f });

            var r = PoliticsTickRules.TickYear(s, 800);
            Assert.IsFalse(r.dividedCrisis); // 0.275 × prox(2)=0.275 < 0.6
            Assert.IsFalse(r.dividedCrisisOnset);
        }
    }
}
