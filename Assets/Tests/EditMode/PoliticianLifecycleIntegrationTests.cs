using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政治家の一生が「頭角→党首→入閣→統治→失脚→老衰死」と一通り成立することを統合的に固定する（純ロジックの結合）：
    /// 功績で人気を得て（<see cref="PoliticianRules.ApplyAchievement"/>）党首の器になり（<see cref="PoliticianRules.IsViableLeader"/>）、
    /// 総裁選で勝ち（<see cref="LeadershipElectionRules"/> へ委譲）、与党党首＝首班になり（<see cref="PartyRules.Premier"/>）、
    /// 民主国家の高位政治職（政治任用専用）に就き（<see cref="OfficeRules.CanHold"/>）、<b>民意と票</b>で決裁し
    /// （<see cref="PoliticianRules.PetitionApproval"/>）、スキャンダルで党員票を失って失脚（再選で交代）、最後に老衰死
    /// （<see cref="LifecycleRules"/>）する。実ルールを順に通す（GalaxyView 非依存・CI 検証可能）。
    /// </summary>
    public class PoliticianLifecycleIntegrationTests
    {
        [Test]
        public void PoliticianLife_RiseGovernFallDeath_PassesEveryStage()
        {
            const int startYear = 800;

            // --- ① 政界入り：文民が政治家になる（職分ゲート＝民意と票の土俵に乗る） ---
            var hero = new Person(1, "駆け出しの政治家", Faction.同盟, PersonRole.文民)
            {
                isPolitician = true,
                rankTier = 9,                 // 大臣級の階級
                birthYear = startYear - 50,   // 開始時50歳
            };
            Assert.AreEqual(PersonVocation.政治家, PersonVocationRules.VocationOf(hero));
            Assert.IsTrue(PoliticianRules.IsPolitician(hero), "政治家として土俵に乗らない");

            // 駆け出し＝低人気でまだ党首の器でない
            var pol = new PoliticianProfile(hero.id) { popularity = 0.4f, homeRegionKey = "首都星系" };
            Assert.IsFalse(PoliticianRules.IsViableLeader(pol), "駆け出しが党首候補になってしまう");

            // --- ② 頭角：功績で人気が上がり、弁舌・党内基盤を備えて党首の器に ---
            PoliticianRules.ApplyAchievement(pol, 0.45f); // 人気 0.4→0.85
            pol.oratory = 80;
            pol.partyStanding = 0.7f;
            pol.integrity = 30; // 清濁あわせ呑む（スキャンダルに弱い）
            Assert.IsTrue(PoliticianRules.IsViableLeader(pol), "頭角を現しても党首候補にならない");

            // --- ③ 総裁選：ライバルに勝って党首へ（選挙の解決は LeadershipElectionRules へ委譲） ---
            var rivalPerson = new Person(2, "対立候補", Faction.同盟, PersonRole.文民) { isPolitician = true, rankTier = 9, birthYear = startYear - 55 };
            var rival = new PoliticianProfile(rivalPerson.id) { popularity = 0.6f, oratory = 70, partyStanding = 0.6f, integrity = 60 };

            var leadershipVote = new LeadershipElectionRules.VoteParams(0.7f, 0.3f); // 党員票（民意連動）が重い総裁選
            var round1 = new List<LeadershipElectionRules.Candidate>
            {
                PoliticianRules.ToCandidate(pol),
                PoliticianRules.ToCandidate(rival),
            };
            int leader = LeadershipElectionRules.Elect(round1, leadershipVote, out _);
            Assert.AreEqual(hero.id, leader, "総裁選で党首になれなかった");

            // 与党党首＝首班（党勢で首班決定）
            var rulingParty = new Party(1, "民政党", Faction.同盟) { leaderId = leader, support = 0.55f };
            var oppositionParty = new Party(2, "革新党", Faction.同盟) { leaderId = rivalPerson.id, support = 0.45f };
            var parties = new List<Party> { rulingParty, oppositionParty };
            Assert.AreEqual(hero.id, PartyRules.Premier(parties), "党首が首班にならなかった");

            // --- ④ 入閣：民主国家では高位政治職は政治任用専用＝官僚は就けず政治家のみ ---
            var minister = new Office(10, "内務大臣", OfficeScope.国家, OfficeDomain.内政) { requiredTier = 9 };
            var viceMinister = new Office(11, "事務次官", OfficeScope.国家, OfficeDomain.内政) { requiredTier = 7 };
            PartyRules.MarkDemocraticAppointments(new List<Office> { minister, viceMinister }, careerCeilingTier: 7, CivilianControlType.文民統制);

            Assert.IsTrue(OfficeRules.CanHold(hero, minister), "政治家が大臣に就けない");
            var careerOfficial = new Person(3, "職業官僚", Faction.同盟, PersonRole.文民) { rankTier = 9, isPolitician = false };
            Assert.IsFalse(OfficeRules.CanHold(careerOfficial, minister), "官僚が大臣に就けてしまう（政治任用の壁が無い）");
            Assert.IsTrue(OfficeRules.CanHold(careerOfficial, viceMinister), "官僚が事務次官に就けない");

            // --- ⑤ 統治：民意と票で決裁する（国王の大局/正統性ではない） ---
            // 全国的に人気の案件は通す
            float popularPolicy = PoliticianRules.PetitionApproval(pol, publicSupport: 0.9f, petitionRegionKey: "");
            Assert.Greater(popularPolicy, 0.5f, "人気のある案件を通さない");
            // 地盤（票田）に効く陳情は、全国的に不人気でも票のために通す（他人の地盤の同じ案件より通しやすい）
            float pork = PoliticianRules.PetitionApproval(pol, publicSupport: 0.2f, petitionRegionKey: "首都星系");
            float rivalTurf = PoliticianRules.PetitionApproval(pol, publicSupport: 0.2f, petitionRegionKey: "辺境星系");
            Assert.Greater(pork, rivalTurf, "地盤への利益誘導が成立しない（票で動かない）");

            // --- ⑥ 失脚：スキャンダルで党員票を失い、再選でライバルに敗れる ---
            float beforeScandal = PoliticianRules.EffectivePopularity(pol);
            PoliticianRules.ApplyScandal(pol, 0.9f);
            Assert.Less(PoliticianRules.EffectivePopularity(pol), beforeScandal, "スキャンダルで実効人気が落ちない");

            var round2 = new List<LeadershipElectionRules.Candidate>
            {
                PoliticianRules.ToCandidate(pol),
                PoliticianRules.ToCandidate(rival),
            };
            int newLeader = LeadershipElectionRules.Elect(round2, leadershipVote, out _);
            Assert.AreEqual(rivalPerson.id, newLeader, "スキャンダル後も党首のまま（失脚しない）");
            rulingParty.leaderId = newLeader;
            Assert.AreEqual(rivalPerson.id, PartyRules.Premier(parties), "失脚後も首班のまま");

            // --- ⑦ 老衰死：年を送る（人気は中庸へ風化・スキャンダルは忘れられる）→ 高齢で死亡 ---
            var life = LifecycleRules.LifespanParams.Default;
            bool died = false;
            for (int year = startYear; year <= startYear + 50 && !died; year++)
            {
                PoliticianRules.TickYear(pol); // 人気は中庸へ・スキャンダルは時間で薄れる
                int age = LifecycleRules.Age(hero, year);
                float roll = age >= 75 ? 0f : 1f; // 高齢で確実に
                if (LifecycleRules.ShouldDieOfAge(age, roll, 1, life))
                {
                    hero.deathYear = year;
                    died = true;
                }
            }

            Assert.IsTrue(died, "老衰死に到達しなかった");
            Assert.IsFalse(hero.IsAvailable, "死亡後も就任可能のまま");
            Assert.Less(pol.scandalLevel, 0.1f, "スキャンダルが時間で忘れられていない");
        }

        [Test]
        public void CareerBureaucrat_StaysOffThePoliticalArena()
        {
            // 官僚（文民・非政治家）は、いくら有能でも政治家の土俵（民意と票）に乗らない＝高位政治職に就けない。
            var official = new Person(1, "有能な官僚", Faction.同盟, PersonRole.文民) { rankTier = 10, isPolitician = false };
            Assert.IsFalse(PoliticianRules.IsPolitician(official));

            var minister = new Office(10, "宰相", OfficeScope.国家, OfficeDomain.元首) { requiredTier = 9 };
            PartyRules.MarkDemocraticAppointments(new List<Office> { minister }, careerCeilingTier: 7, CivilianControlType.文民統制);
            Assert.IsFalse(OfficeRules.CanHold(official, minister), "官僚が政治任用専用の高位職に就けてしまう");
        }
    }
}
