using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政治家システム基盤（#159 GOV-6 / 目安箱 政治家箱 #1296）を固定する：政治家は民意と票で生き死にする＝
    /// 選挙力は人気/弁舌（党員票）と党内基盤/清廉さ（議員票）から導かれ、決裁は「民意と票」だけを見る
    /// （国王の大局/正統性を見ない）。スキャンダルは実効人気を削り時間で薄れる。選挙の解決は
    /// <see cref="LeadershipElectionRules"/> へ委譲する（<see cref="PoliticianRules.ToCandidate"/> で橋渡し＝二重実装しない）。
    /// </summary>
    public class PoliticianRulesTests
    {
        [Test]
        public void IsPolitician_GatesViaVocation()
        {
            var politician = new Person(1, "政治家", Faction.同盟, PersonRole.文民) { isPolitician = true };
            var bureaucrat = new Person(2, "官僚", Faction.同盟, PersonRole.文民) { isPolitician = false };
            // 君主フラグは政治家を上書きしない（別格＝VocationOf は君主）
            var monarch = new Person(3, "皇帝", Faction.帝国, PersonRole.文民) { isPolitician = true, isSovereign = true };

            Assert.IsTrue(PoliticianRules.IsPolitician(politician));
            Assert.IsFalse(PoliticianRules.IsPolitician(bureaucrat));
            Assert.IsFalse(PoliticianRules.IsPolitician(monarch)); // 君主は政治家でなく別格
            Assert.IsFalse(PoliticianRules.IsPolitician(null));
        }

        [Test]
        public void EffectivePopularity_DiscountedByScandal_BaseUntouched()
        {
            var pol = new PoliticianProfile(1) { popularity = 0.8f, scandalLevel = 0.5f };
            // 0.8 × (1 - 0.5×0.5) = 0.8 × 0.75 = 0.6
            Assert.AreEqual(0.6f, PoliticianRules.EffectivePopularity(pol), 1e-4f);
            Assert.AreEqual(0.8f, pol.popularity, 1e-4f); // 基準値は非破壊
        }

        [Test]
        public void MemberVoteAppeal_RisesWithOratory()
        {
            var quiet = new PoliticianProfile(1) { popularity = 0.6f, oratory = 0 };
            var orator = new PoliticianProfile(2) { popularity = 0.6f, oratory = 100 };
            // 人気同じでも弁舌が票を増幅する
            Assert.Greater(PoliticianRules.MemberVoteAppeal(orator), PoliticianRules.MemberVoteAppeal(quiet));
            // 弁舌0：0.6×0.7=0.42／弁舌100：0.6×1.0=0.6
            Assert.AreEqual(0.42f, PoliticianRules.MemberVoteAppeal(quiet), 1e-4f);
            Assert.AreEqual(0.6f, PoliticianRules.MemberVoteAppeal(orator), 1e-4f);
        }

        [Test]
        public void LegislatorVoteAppeal_DrivenByPartyStanding()
        {
            var insider = new PoliticianProfile(1) { partyStanding = 1f, integrity = 100 };
            var outsider = new PoliticianProfile(2) { partyStanding = 0.2f, integrity = 100 };
            Assert.Greater(PoliticianRules.LegislatorVoteAppeal(insider), PoliticianRules.LegislatorVoteAppeal(outsider));
            // 基盤1.0×(0.8+0.2×1.0)=1.0
            Assert.AreEqual(1f, PoliticianRules.LegislatorVoteAppeal(insider), 1e-4f);
        }

        [Test]
        public void ToCandidate_BridgesToLeadershipElection()
        {
            // 党員票に強い候補と議員票に強い候補＝総裁選のねじれ（人気者 vs 党内基盤）
            var popular = new PoliticianProfile(10) { popularity = 1f, oratory = 100, partyStanding = 0.2f, integrity = 50 };
            var insider = new PoliticianProfile(20) { popularity = 0.3f, oratory = 0, partyStanding = 1f, integrity = 100 };

            var candidates = new List<LeadershipElectionRules.Candidate>
            {
                PoliticianRules.ToCandidate(popular),
                PoliticianRules.ToCandidate(insider),
            };

            // 党員票重視なら人気者が勝つ
            int memberPick = LeadershipElectionRules.Elect(candidates, new LeadershipElectionRules.VoteParams(1f, 0f), out _);
            Assert.AreEqual(10, memberPick);

            // 議員票重視なら党内基盤が勝つ
            int legPick = LeadershipElectionRules.Elect(candidates, new LeadershipElectionRules.VoteParams(0f, 1f), out _);
            Assert.AreEqual(20, legPick);

            // 人気と党内基盤の乖離＝ねじれ
            Assert.IsTrue(LeadershipElectionRules.HasTwist(candidates));
        }

        [Test]
        public void PetitionApproval_WeighsPublicOpinionAndVotes()
        {
            var pol = new PoliticianProfile(1);
            // 民意と票が同等：両方高ければ通す、両方低ければ通さない
            Assert.AreEqual(1f, PoliticianRules.PetitionApproval(pol, 1f, 1f), 1e-4f);
            Assert.AreEqual(0f, PoliticianRules.PetitionApproval(pol, 0f, 0f), 1e-4f);
            // 民意0.5/票0.5 → 0.5
            Assert.AreEqual(0.5f, PoliticianRules.PetitionApproval(pol, 0.5f, 0.5f), 1e-4f);
        }

        [Test]
        public void PetitionApproval_PorkOverridesUnpopularity_ForHomeRegion()
        {
            var pol = new PoliticianProfile(1) { homeRegionKey = "イゼルローン方面" };
            // 自分の票田に効く案件は、全国的に不人気でも票のために通す
            float home = PoliticianRules.PetitionApproval(pol, publicSupport: 0.2f, petitionRegionKey: "イゼルローン方面");
            // 0.5×0.2 + 0.5×1.0 = 0.6
            Assert.AreEqual(0.6f, home, 1e-4f);

            // 他人の地盤の案件は自票に薄い＝同じ民意でも通しにくい
            float rival = PoliticianRules.PetitionApproval(pol, publicSupport: 0.2f, petitionRegionKey: "フェザーン");
            // 0.5×0.2 + 0.5×0.1 = 0.15
            Assert.AreEqual(0.15f, rival, 1e-4f);
            Assert.Greater(home, rival);
        }

        [Test]
        public void VoteGainForRegion_NationalPoliticianIsNeutral()
        {
            var national = new PoliticianProfile(1) { homeRegionKey = "" };
            Assert.AreEqual(PoliticianRules.NeutralVoteGain, PoliticianRules.VoteGainForRegion(national, "どこか"), 1e-4f);

            var local = new PoliticianProfile(2) { homeRegionKey = "ハイネセン" };
            Assert.AreEqual(PoliticianRules.HomeRegionVoteGain, PoliticianRules.VoteGainForRegion(local, "ハイネセン"), 1e-4f);
            Assert.AreEqual(PoliticianRules.RivalRegionVoteGain, PoliticianRules.VoteGainForRegion(local, "オーディン"), 1e-4f);
            // 地盤持ちでも全国案件（regionKey空）は中立
            Assert.AreEqual(PoliticianRules.NeutralVoteGain, PoliticianRules.VoteGainForRegion(local, ""), 1e-4f);
        }

        [Test]
        public void ApplyScandal_IntegrityResists()
        {
            var clean = new PoliticianProfile(1) { integrity = 100 };
            var dirty = new PoliticianProfile(2) { integrity = 0 };

            PoliticianRules.ApplyScandal(clean, 0.4f);
            PoliticianRules.ApplyScandal(dirty, 0.4f);

            // 清廉100は被害半減（0.4×0.5=0.2）、清廉0は満額（0.4）
            Assert.AreEqual(0.2f, clean.scandalLevel, 1e-4f);
            Assert.AreEqual(0.4f, dirty.scandalLevel, 1e-4f);
            Assert.Greater(dirty.scandalLevel, clean.scandalLevel);
        }

        [Test]
        public void ApplyAchievement_MovesPopularityBothWays()
        {
            var pol = new PoliticianProfile(1) { popularity = 0.5f };
            PoliticianRules.ApplyAchievement(pol, 0.3f);
            Assert.AreEqual(0.8f, pol.popularity, 1e-4f);
            PoliticianRules.ApplyAchievement(pol, -0.5f); // 失政
            Assert.AreEqual(0.3f, pol.popularity, 1e-4f);
            // 0..1 クランプ
            PoliticianRules.ApplyAchievement(pol, -10f);
            Assert.AreEqual(0f, pol.popularity, 1e-4f);
        }

        [Test]
        public void TickYear_PopularityFadesAndScandalForgotten()
        {
            var hero = new PoliticianProfile(1) { popularity = 1f, scandalLevel = 0.5f };
            PoliticianRules.TickYear(hero); // 既定：人気-0.05/年, スキャンダル-0.2/年
            Assert.AreEqual(0.95f, hero.popularity, 1e-4f); // 昨日の英雄も薄れる
            Assert.AreEqual(0.3f, hero.scandalLevel, 1e-4f); // スキャンダルは忘れられる

            // 不人気な政治家は中庸へ向かって持ち直す（風化＝両側へ収束）
            var unpopular = new PoliticianProfile(2) { popularity = 0.2f };
            PoliticianRules.TickYear(unpopular);
            Assert.AreEqual(0.25f, unpopular.popularity, 1e-4f);
        }

        [Test]
        public void IsViableLeader_RequiresElectoralStrength()
        {
            var strong = new PoliticianProfile(1) { popularity = 1f, oratory = 100, partyStanding = 1f, integrity = 100 };
            var weak = new PoliticianProfile(2) { popularity = 0.1f, oratory = 0, partyStanding = 0.1f, integrity = 0 };
            Assert.IsTrue(PoliticianRules.IsViableLeader(strong));
            Assert.IsFalse(PoliticianRules.IsViableLeader(weak));
        }

        [Test]
        public void NullSafe()
        {
            Assert.AreEqual(0f, PoliticianRules.EffectivePopularity(null));
            Assert.AreEqual(0f, PoliticianRules.MemberVoteAppeal(null));
            Assert.AreEqual(0f, PoliticianRules.LegislatorVoteAppeal(null));
            Assert.AreEqual(0f, PoliticianRules.ElectoralStrength(null));
            Assert.AreEqual(-1, PoliticianRules.ToCandidate(null).id);
            Assert.AreEqual(PoliticianRules.NeutralVoteGain, PoliticianRules.VoteGainForRegion(null, "x"));
            Assert.DoesNotThrow(() => PoliticianRules.ApplyScandal(null, 0.5f));
            Assert.DoesNotThrow(() => PoliticianRules.ApplyAchievement(null, 0.5f));
            Assert.DoesNotThrow(() => PoliticianRules.TickYear(null));
        }
    }
}
