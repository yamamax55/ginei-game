using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政党の結成と党役職の就任を固定する（政党システム GOV-6 #159）：党を作ると創設者が初代党首・党員になり、
    /// 党役職（党首/党三役/国対）には党員のみ就任でき、党首は <see cref="Party.leaderId"/> が単一の出所、離党で自動解任される。
    /// </summary>
    public class PartyOrganizationRulesTests
    {
        [Test]
        public void Create_FounderBecomesLeaderAndMember()
        {
            var p = PartyOrganizationRules.Create(1, "民政党", Faction.同盟, founderId: 100, platform: "自由");
            Assert.AreEqual(100, p.leaderId);
            Assert.IsTrue(PartyOrganizationRules.IsMember(p, 100));
            Assert.AreEqual(100, PartyOrganizationRules.HolderOf(p, PartyPost.党首)); // 党首は leaderId が出所
            Assert.AreEqual("自由", p.platform);
        }

        [Test]
        public void AppointPost_OnlyMembers_AndReplaceHolder()
        {
            var p = PartyOrganizationRules.Create(1, "民政党", Faction.同盟, 100);

            // 党外の人物は就任不可
            Assert.IsFalse(PartyOrganizationRules.CanAppoint(p, PartyPost.幹事長, 200));
            Assert.IsFalse(PartyOrganizationRules.AppointPost(p, PartyPost.幹事長, 200));

            // 入党すれば党役職に就ける
            PartyOrganizationRules.Join(p, 200);
            Assert.IsTrue(PartyOrganizationRules.AppointPost(p, PartyPost.幹事長, 200));
            Assert.AreEqual(200, PartyOrganizationRules.HolderOf(p, PartyPost.幹事長));

            // 同一役職は1人＝置換（重複行を作らない）
            PartyOrganizationRules.Join(p, 201);
            Assert.IsTrue(PartyOrganizationRules.AppointPost(p, PartyPost.幹事長, 201));
            Assert.AreEqual(201, PartyOrganizationRules.HolderOf(p, PartyPost.幹事長));
            Assert.AreEqual(1, p.posts.Count);
        }

        [Test]
        public void AppointLeader_UpdatesLeaderId()
        {
            var p = PartyOrganizationRules.Create(1, "民政党", Faction.同盟, 100);
            PartyOrganizationRules.Join(p, 300);
            Assert.IsTrue(PartyOrganizationRules.AppointPost(p, PartyPost.党首, 300)); // 党首交代
            Assert.AreEqual(300, p.leaderId);
            Assert.AreEqual(300, PartyOrganizationRules.HolderOf(p, PartyPost.党首));
        }

        [Test]
        public void Leave_ClearsLeadershipAndPosts()
        {
            var p = PartyOrganizationRules.Create(1, "民政党", Faction.同盟, 100);
            PartyOrganizationRules.Join(p, 200);
            PartyOrganizationRules.AppointPost(p, PartyPost.政調会長, 200);

            // 政調会長が離党＝党役職が空席化
            Assert.IsTrue(PartyOrganizationRules.Leave(p, 200));
            Assert.AreEqual(-1, PartyOrganizationRules.HolderOf(p, PartyPost.政調会長));
            Assert.IsFalse(PartyOrganizationRules.IsMember(p, 200));

            // 党首が離党＝党首空席
            Assert.IsTrue(PartyOrganizationRules.Leave(p, 100));
            Assert.AreEqual(-1, p.leaderId);
        }

        [Test]
        public void DismissPost_AndThreeLeadership()
        {
            var p = PartyOrganizationRules.Create(1, "民政党", Faction.同盟, 100);
            PartyOrganizationRules.Join(p, 200);
            PartyOrganizationRules.AppointPost(p, PartyPost.総務会長, 200);
            Assert.IsTrue(PartyOrganizationRules.DismissPost(p, PartyPost.総務会長));
            Assert.AreEqual(-1, PartyOrganizationRules.HolderOf(p, PartyPost.総務会長));
            Assert.IsFalse(PartyOrganizationRules.DismissPost(p, PartyPost.総務会長)); // もう空席

            // 党三役＝幹事長/政調会長/総務会長
            Assert.IsTrue(PartyOrganizationRules.IsThreeLeadership(PartyPost.幹事長));
            Assert.IsTrue(PartyOrganizationRules.IsThreeLeadership(PartyPost.政調会長));
            Assert.IsTrue(PartyOrganizationRules.IsThreeLeadership(PartyPost.総務会長));
            Assert.IsFalse(PartyOrganizationRules.IsThreeLeadership(PartyPost.党首));
            Assert.IsFalse(PartyOrganizationRules.IsThreeLeadership(PartyPost.国対委員長));
        }
    }
}
