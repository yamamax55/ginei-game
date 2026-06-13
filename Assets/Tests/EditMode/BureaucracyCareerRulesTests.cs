using System.Collections.Generic;
using NUnit.Framework;
using Ginei;
using CP = Ginei.BureaucracyCareerRules.CareerParams;
using Kind = Ginei.BureaucracyCareerRules.CareerEventKind;

namespace Ginei.Tests
{
    /// <summary>
    /// 文官のネームド化の官歴オーケストレータ（位階の出身→考課→叙位、五位の壁は朝廷の権威で開く）を固定する。
    /// </summary>
    public class BureaucracyCareerRulesTests
    {
        private static Person Civil(int id, int op, int intel, int rankTier = 4, int gradYear = 0)
            => new Person(id, "文官" + id, Faction.帝国, PersonRole.文民)
               { operation = op, intelligence = intel, rankTier = rankTier, graduationYear = gradYear };

        [Test]
        public void InitialCourtRank_HighOfficeIsNoble_CommonerBelowWall()
        {
            Assert.AreEqual(CourtRank.従五位下, BureaucracyCareerRules.InitialCourtRank(Civil(1, 0, 0, rankTier: 8)));
            Assert.AreEqual(CourtRank.正六位上, BureaucracyCareerRules.InitialCourtRank(Civil(1, 0, 0, rankTier: 7)));
            Assert.AreEqual(CourtRank.正八位上, BureaucracyCareerRules.InitialCourtRank(Civil(1, 0, 0, rankTier: 0)));
            // 進士級(7)は五位の壁の下、高官(8)は貴族
            Assert.IsFalse(JapaneseCourtRankRules.IsNobility(CourtRank.正六位上));
            Assert.IsTrue(JapaneseCourtRankRules.IsNobility(CourtRank.従五位下));
        }

        [Test]
        public void TickYear_SeedsRankAndMerit_AndPromotesCompetent()
        {
            var roster = new List<Person> { Civil(1, 80, 80, rankTier: 4, gradYear: 790) };
            var changes = new List<BureaucracyCareerRules.CareerChange>();
            BureaucracyCareerRules.TickYear(roster, courtAuthority: 0.3f, currentYear: 800, CP.Default, changes);

            Person p = roster[0];
            Assert.IsNotNull(p.merit);
            Assert.AreEqual(1, p.merit.evaluations);
            Assert.IsTrue(JapaneseCourtRankRules.IsRanked(p.courtRank)); // 無位から叙位された
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(Kind.叙位, changes[0].kind);
        }

        [Test]
        public void TickYear_FifthRankWall_ClosedUnderLowAuthority_OpenUnderHigh()
        {
            // 正六位上の俊英（高能力・清廉・長勤）＝上上を取る
            Person Talent()
            {
                var t = Civil(1, 95, 95, rankTier: 7, gradYear: 780);
                t.courtRank = CourtRank.正六位上;
                t.merit = new OfficialMerit(1, 1f);
                return t;
            }

            // 朝廷の権威が低い（封建の世）＝五位の壁が閉じる＝六位で頭打ち
            var low = new List<Person> { Talent() };
            BureaucracyCareerRules.TickYear(low, courtAuthority: 0.3f, currentYear: 810, CP.Default);
            Assert.AreEqual(CourtRank.正六位上, low[0].courtRank);

            // 朝廷の権威が高い（律令が機能）＝勅授で壁を越えて貴族入り
            var high = new List<Person> { Talent() };
            var changes = new List<BureaucracyCareerRules.CareerChange>();
            BureaucracyCareerRules.TickYear(high, courtAuthority: 0.8f, currentYear: 810, CP.Default, changes);
            Assert.AreEqual(CourtRank.従五位下, high[0].courtRank);
            Assert.IsTrue(JapaneseCourtRankRules.IsNobility(high[0].courtRank));
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(Kind.五位突破, changes[0].kind);
        }

        [Test]
        public void TickYear_PoorOfficial_IsDemoted()
        {
            var p = Civil(1, 0, 0, rankTier: 3);
            p.courtRank = CourtRank.正七位上;
            p.merit = new OfficialMerit(1, 0f); // 無能・汚職＝下下
            var roster = new List<Person> { p };
            var changes = new List<BureaucracyCareerRules.CareerChange>();
            BureaucracyCareerRules.TickYear(roster, courtAuthority: 0.5f, currentYear: 800, CP.Default, changes);
            Assert.AreEqual(CourtRank.正七位下, p.courtRank); // 一階貶位
            Assert.AreEqual(Kind.貶位, changes[0].kind);
        }

        [Test]
        public void TickYear_SkipsMilitaryAndDeceased()
        {
            var soldier = new Person(1, "提督", Faction.帝国, PersonRole.軍人) { operation = 90, intelligence = 90 };
            var dead = Civil(2, 90, 90);
            dead.deathYear = 799;
            var roster = new List<Person> { soldier, dead };
            BureaucracyCareerRules.TickYear(roster, courtAuthority: 0.8f, currentYear: 800, CP.Default);
            Assert.AreEqual(CourtRank.無位, soldier.courtRank); // 軍人は対象外
            Assert.IsNull(soldier.merit);
            Assert.AreEqual(CourtRank.無位, dead.courtRank);    // 故人は評定しない
            Assert.IsNull(dead.merit);
        }

        [Test]
        public void TickYear_NullRoster_IsSafe()
        {
            Assert.DoesNotThrow(() => BureaucracyCareerRules.TickYear(null, 0.5f, 800, CP.Default));
        }
    }
}
