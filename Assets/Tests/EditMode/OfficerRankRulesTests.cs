using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 武官の階級欠落の扱い（<see cref="OfficerRankRules"/>）。
    /// 実機報告「軍団編成で "同盟の士1/2" だけ階級が出ない」への対応を固定する：
    /// ①未設定の武官には実データとして最初の段（少尉）を入れる ②既にある階級は書き換えない
    /// ③階級を入れても任命条件は変わらない（黙って昇進させない）。
    /// </summary>
    public class OfficerRankRulesTests
    {
        private static Person Officer(int id, int rankTier, PersonRole role = PersonRole.軍人, int deathYear = 0)
            => new Person(id, "士" + id, Faction.同盟, role) { rankTier = rankTier, deathYear = deathYear };

        // ── 欠落を埋める ──

        [Test]
        public void EntryTier_IsTheFirstRungOfTheLadder()
        {
            Assert.AreEqual(1, OfficerRankRules.EntryTier);
            Assert.AreEqual("少尉", RankSystem.FullLadderName(OfficerRankRules.EntryTier));
        }

        [Test]
        public void EnsureRank_FillsUnsetOfficer()
        {
            Person p = Officer(1, 0);
            Assert.IsTrue(OfficerRankRules.EnsureRank(p));
            Assert.AreEqual(OfficerRankRules.EntryTier, p.rankTier);
        }

        /// <summary>★既に階級がある人物は絶対に書き換えない（一律昇格も降格もしない）。</summary>
        [Test]
        public void EnsureRank_NeverOverwritesExistingRank()
        {
            Person low = Officer(1, 2);
            Person high = Officer(2, 9);
            Assert.IsFalse(OfficerRankRules.EnsureRank(low));
            Assert.IsFalse(OfficerRankRules.EnsureRank(high));
            Assert.AreEqual(2, low.rankTier);
            Assert.AreEqual(9, high.rankTier, "上位者を引き下げてはいけない");
        }

        [Test]
        public void EnsureRank_SkipsCiviliansAndDeceased()
        {
            Person civil = Officer(1, 0, PersonRole.文民);
            Assert.IsFalse(OfficerRankRules.EnsureRank(civil));
            Assert.AreEqual(0, civil.rankTier, "文民に軍の階級を与えない");

            Person dead = Officer(2, 0, PersonRole.軍人, deathYear: 700);
            Assert.IsFalse(OfficerRankRules.EnsureRank(dead));
            Assert.AreEqual(0, dead.rankTier);
        }

        [Test]
        public void EnsureRank_NullIsSafe()
        {
            Assert.IsFalse(OfficerRankRules.EnsureRank(null));
        }

        [Test]
        public void EnsureRanks_FillsOnlyWhatIsMissing_AndIsIdempotent()
        {
            var roster = new List<Person>
            {
                Officer(1, 0),
                Officer(2, 7),
                Officer(3, 0),
                Officer(4, 0, PersonRole.文民),
            };

            Assert.AreEqual(2, OfficerRankRules.EnsureRanks(roster), "武官の未設定2名だけ");
            Assert.AreEqual(OfficerRankRules.EntryTier, roster[0].rankTier);
            Assert.AreEqual(7, roster[1].rankTier);
            Assert.AreEqual(OfficerRankRules.EntryTier, roster[2].rankTier);
            Assert.AreEqual(0, roster[3].rankTier);

            // 何度呼んでも結果が変わらない（旧セーブを読むたびに階級が動かない）。
            Assert.AreEqual(0, OfficerRankRules.EnsureRanks(roster));
        }

        [Test]
        public void EnsureRanks_NullListIsSafe()
        {
            Assert.AreEqual(0, OfficerRankRules.EnsureRanks(null));
        }

        [Test]
        public void FillMissing_IsPureAndMatchesEnsure()
        {
            Assert.AreEqual(OfficerRankRules.EntryTier, OfficerRankRules.FillMissing(PersonRole.軍人, 0));
            Assert.AreEqual(6, OfficerRankRules.FillMissing(PersonRole.軍人, 6));
            Assert.AreEqual(0, OfficerRankRules.FillMissing(PersonRole.文民, 0));
            Assert.AreEqual(0, OfficerRankRules.FillMissing(PersonRole.軍人, 0, isDeceased: true));
        }

        // ── 表示が解決できること（実機の症状そのもの） ──

        /// <summary>
        /// ★尉官・佐官（tier1〜4）は将官専用のフォールバックでは空文字になる。
        /// 表示は立身出世ラダー（<see cref="RankSystem.CareerRankName"/>）で解決しなければならない。
        /// </summary>
        [Test]
        public void JuniorRank_ResolvesOnlyThroughCareerLadder()
        {
            Assert.AreEqual("", RankSystem.ResolveRankNameOrDefault(null, OfficerRankRules.EntryTier),
                            "将官専用フォールバックでは尉官が出ない（これが実機の症状）");
            Assert.AreEqual("少尉", RankSystem.CareerRankName(null, OfficerRankRules.EntryTier));
        }

        [Test]
        public void FilledOfficer_ShowsRankBeforeName()
        {
            Person p = Officer(1, 0);
            OfficerRankRules.EnsureRank(p);
            string label = FleetCommandLabelRules.CommanderLabel(p.name, RankSystem.CareerRankName(null, p.rankTier));
            Assert.AreEqual("少尉 士1", label);
        }

        [Test]
        public void UnsetCivilian_ShowsNameWithoutFabricatedRank()
        {
            Person p = Officer(1, 0, PersonRole.文民);
            OfficerRankRules.EnsureRank(p);   // 何もしない
            string label = FleetCommandLabelRules.CommanderLabel(p.name, RankSystem.CareerRankName(null, p.rankTier));
            Assert.AreEqual("士1", label, "階級が無いなら付けない（架空の階級を足さない）");
        }

        // ── 任命条件（階級を埋めても指揮できる規模は変わらない） ──

        [Test]
        public void MeetsCommandGate_UsesExistingEchelonTiers()
        {
            Assert.AreEqual(CommandCapacityRules.Tier艦隊,
                            CommandCapacityRules.CommanderTierFor(EchelonType.艦隊));
            Assert.IsFalse(OfficerRankRules.MeetsCommandGate(OfficerRankRules.EntryTier, EchelonType.艦隊),
                           "少尉が艦隊司令の条件を満たしてはいけない");
            Assert.IsTrue(OfficerRankRules.MeetsCommandGate(CommandCapacityRules.Tier艦隊, EchelonType.艦隊));
            Assert.IsTrue(OfficerRankRules.MeetsCommandGate(10, EchelonType.艦隊));
        }

        [Test]
        public void CommandGateNote_ShownOnlyWhenShort()
        {
            Assert.AreEqual("", OfficerRankRules.CommandGateNote(CommandCapacityRules.Tier艦隊, EchelonType.艦隊));
            string note = OfficerRankRules.CommandGateNote(OfficerRankRules.EntryTier, EchelonType.艦隊);
            Assert.IsFalse(string.IsNullOrEmpty(note));
            StringAssert.Contains("中将", note, "必要な階級が名指しで出ること");
        }

        /// <summary>階級を補っても任命条件は変わらない＝辻褄合わせの昇進をしていない。</summary>
        [Test]
        public void FillingRank_DoesNotGrantCommandEligibility()
        {
            Person p = Officer(1, 0);
            OfficerRankRules.EnsureRank(p);
            Assert.AreEqual(OfficerRankRules.EntryTier, p.rankTier);
            Assert.IsFalse(OfficerRankRules.MeetsCommandGate(p.rankTier, EchelonType.艦隊));
        }

        // ── セーブ往復（旧セーブの欠落） ──

        [Test]
        public void RankSurvivesSaveRoundTrip_AndLegacyZeroIsFilledOnLoad()
        {
            var people = new List<Person> { Officer(1, 0), Officer(2, 8) };

            // 保存前は 0 のまま（保存そのものは階級を作らない）。
            Assert.AreEqual(0, people[0].rankTier);

            // 読み込み後に補う運用（GalaxyView.SetupPersonnel と同じ順序）。
            OfficerRankRules.EnsureRanks(people);
            Assert.AreEqual(OfficerRankRules.EntryTier, people[0].rankTier);
            Assert.AreEqual(8, people[1].rankTier, "保存済みの階級はそのまま");
        }
    }
}
