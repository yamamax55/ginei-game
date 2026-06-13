using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 選挙の実施タイミング（選挙システム基盤・日本の国会を範に）を固定する：下院＝衆議院相当（任期4年・解散で総選挙・全議席改選）、
    /// 上院＝参議院相当（任期6年・解散なし・3年ごとに半数改選）。年次の進行で正しい年に選挙が起き、改選割合/区分が合うこと。
    /// </summary>
    public class ElectionScheduleRulesTests
    {
        [Test]
        public void Profiles_MatchJapaneseDiet()
        {
            var lower = ElectionScheduleRules.ProfileFor(LegislativeChamber.下院);
            Assert.AreEqual(4, lower.termYears);
            Assert.IsTrue(lower.dissolvable);
            Assert.AreEqual(1, lower.classCount);
            Assert.AreEqual(4, lower.ElectionIntervalYears);          // 4年ごと
            Assert.AreEqual(1f, lower.SeatFractionPerElection, 1e-4f); // 全議席改選

            var upper = ElectionScheduleRules.ProfileFor(LegislativeChamber.上院);
            Assert.AreEqual(6, upper.termYears);
            Assert.IsFalse(upper.dissolvable);
            Assert.AreEqual(2, upper.classCount);
            Assert.AreEqual(3, upper.ElectionIntervalYears);           // 3年ごと
            Assert.AreEqual(0.5f, upper.SeatFractionPerElection, 1e-4f); // 半数改選

            Assert.IsTrue(ElectionScheduleRules.CanDissolve(LegislativeChamber.下院));
            Assert.IsFalse(ElectionScheduleRules.CanDissolve(LegislativeChamber.上院));
        }

        [Test]
        public void LowerHouse_FourYearTerm_Or_Dissolution()
        {
            var s = ElectionScheduleRules.Found(LegislativeChamber.下院, foundedYear: 800);
            Assert.AreEqual(804, s.nextElectionYear);                 // 任期満了は4年後
            Assert.AreEqual(1f, ElectionScheduleRules.SeatFractionUp(s), 1e-4f); // 全議席改選

            Assert.IsFalse(ElectionScheduleRules.IsElectionDue(s, 803));
            Assert.IsTrue(ElectionScheduleRules.IsElectionDue(s, 804));

            ElectionScheduleRules.RunElection(s, 804);
            Assert.AreEqual(804, s.lastElectionYear);
            Assert.AreEqual(808, s.nextElectionYear);                 // 次の任期満了は+4年

            // 解散総選挙：任期満了(808)を待たず806に実施＝任期リセット（次は810）
            Assert.IsTrue(ElectionScheduleRules.TryDissolve(s, 806));
            Assert.AreEqual(806, s.lastElectionYear);
            Assert.AreEqual(810, s.nextElectionYear);
        }

        [Test]
        public void UpperHouse_SixYearTerm_HalfEveryThreeYears_NoDissolution()
        {
            var s = ElectionScheduleRules.Found(LegislativeChamber.上院, foundedYear: 800);
            Assert.AreEqual(803, s.nextElectionYear);                  // 最初の通常選挙は3年後
            Assert.AreEqual(0.5f, ElectionScheduleRules.SeatFractionUp(s), 1e-4f); // 半数改選
            Assert.AreEqual(0, ElectionScheduleRules.CurrentClassUp(s));

            // 解散できない＝日程は動かない
            Assert.IsFalse(ElectionScheduleRules.TryDissolve(s, 801));
            Assert.AreEqual(803, s.nextElectionYear);

            // 3年ごとに半数改選、改選区分は交互（0→1→0…＝議員任期は6年）
            ElectionScheduleRules.RunElection(s, 803);
            Assert.AreEqual(1, s.currentClass);
            Assert.AreEqual(806, s.nextElectionYear);
            ElectionScheduleRules.RunElection(s, 806);
            Assert.AreEqual(0, s.currentClass);                       // 6年で一巡（両区分が改選済み）
            Assert.AreEqual(809, s.nextElectionYear);
        }

        [Test]
        public void YearlyTick_FiresElectionsOnSchedule()
        {
            var lower = ElectionScheduleRules.Found(LegislativeChamber.下院, 800);
            var upper = ElectionScheduleRules.Found(LegislativeChamber.上院, 800);
            var lowerYears = new List<int>();
            var upperYears = new List<int>();

            for (int year = 800; year <= 812; year++)
            {
                if (ElectionScheduleRules.TickYear(lower, year)) lowerYears.Add(year);
                if (ElectionScheduleRules.TickYear(upper, year)) upperYears.Add(year);
            }

            // 下院＝4年ごと、上院＝3年ごと（半数改選）
            CollectionAssert.AreEqual(new[] { 804, 808, 812 }, lowerYears);
            CollectionAssert.AreEqual(new[] { 803, 806, 809, 812 }, upperYears);
        }

        [Test]
        public void NullSafe()
        {
            Assert.IsFalse(ElectionScheduleRules.IsElectionDue(null, 800));
            Assert.AreEqual(0f, ElectionScheduleRules.SeatFractionUp(null));
            Assert.AreEqual(0, ElectionScheduleRules.CurrentClassUp(null));
            Assert.IsFalse(ElectionScheduleRules.TryDissolve(null, 800));
            Assert.IsFalse(ElectionScheduleRules.TickYear(null, 800));
            Assert.DoesNotThrow(() => ElectionScheduleRules.RunElection(null, 800));
        }
    }
}
