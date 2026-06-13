using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 政党の結成と党役職への就任の純ロジック（政党システム・GOV-6 #159・唯一の窓口）。
    /// 党を<b>つくり</b>（<see cref="Create"/>＝創設者が初代党首）、党員を入退党させ、<b>党役職</b>（党首/党三役/国対）に
    /// <b>就任</b>させる（<see cref="AppointPost"/>＝党員のみ）。党首は <see cref="Party.leaderId"/> が単一の出所、ほかは
    /// <see cref="Party.posts"/>。総裁選（党首の選び方）は <see cref="LeadershipElectionRules"/>、政府の役職は <see cref="OfficeRules"/> が別系統で担う。
    /// 決定論・test-first。
    /// </summary>
    public static class PartyOrganizationRules
    {
        /// <summary>党三役（幹事長/政調会長/総務会長）か。</summary>
        public static bool IsThreeLeadership(PartyPost post)
            => post == PartyPost.幹事長 || post == PartyPost.政調会長 || post == PartyPost.総務会長;

        /// <summary>党を結成する（創設者が初代党首＝党員に加わる）。創設者が無効（&lt;0）でも党は作れる（空党）。</summary>
        public static Party Create(int id, string partyName, Faction faction, int founderId, string platform = "")
        {
            var p = new Party(id, partyName, faction) { platform = platform ?? "" };
            if (founderId >= 0)
            {
                p.memberIds.Add(founderId);
                p.leaderId = founderId; // 初代党首
            }
            return p;
        }

        /// <summary>その人物が党員か。</summary>
        public static bool IsMember(Party party, int personId)
            => party != null && personId >= 0 && party.memberIds.Contains(personId);

        /// <summary>入党（既に党員/無効 id は false）。</summary>
        public static bool Join(Party party, int personId)
        {
            if (party == null || personId < 0 || party.memberIds.Contains(personId)) return false;
            party.memberIds.Add(personId);
            return true;
        }

        /// <summary>離党（党員でなければ false）。党首/各役職に就いていれば自動で解任する。</summary>
        public static bool Leave(Party party, int personId)
        {
            if (party == null || !party.memberIds.Remove(personId)) return false;
            if (party.leaderId == personId) party.leaderId = -1;
            // 就いていた党役職を空席化
            for (int i = party.posts.Count - 1; i >= 0; i--)
                if (party.posts[i] != null && party.posts[i].holderId == personId)
                    party.posts.RemoveAt(i);
            return true;
        }

        /// <summary>その役職の就任者（党首は <see cref="Party.leaderId"/>。空席/未設定は -1）。</summary>
        public static int HolderOf(Party party, PartyPost post)
        {
            if (party == null) return -1;
            if (post == PartyPost.党首) return party.leaderId;
            foreach (var a in party.posts)
                if (a != null && a.post == post) return a.holderId;
            return -1;
        }

        /// <summary>その役職に就けるか（党員であること＝党外の人物は就任不可）。</summary>
        public static bool CanAppoint(Party party, PartyPost post, int personId)
            => party != null && personId >= 0 && IsMember(party, personId);

        /// <summary>
        /// 党役職に就任させる（党員のみ・同一役職は1人＝既存就任者を置換）。党首就任は <see cref="Party.leaderId"/> を更新。
        /// 党員でない/無効 id は false。
        /// </summary>
        public static bool AppointPost(Party party, PartyPost post, int personId)
        {
            if (!CanAppoint(party, post, personId)) return false;
            if (post == PartyPost.党首)
            {
                party.leaderId = personId;
                return true;
            }
            foreach (var a in party.posts)
                if (a != null && a.post == post) { a.holderId = personId; return true; }
            party.posts.Add(new PartyAppointment(post, personId));
            return true;
        }

        /// <summary>党役職を空席にする（就任者がいれば true）。党首は <see cref="Party.leaderId"/> を -1 に。</summary>
        public static bool DismissPost(Party party, PartyPost post)
        {
            if (party == null) return false;
            if (post == PartyPost.党首)
            {
                if (party.leaderId < 0) return false;
                party.leaderId = -1;
                return true;
            }
            for (int i = 0; i < party.posts.Count; i++)
                if (party.posts[i] != null && party.posts[i].post == post && party.posts[i].holderId >= 0)
                {
                    party.posts.RemoveAt(i);
                    return true;
                }
            return false;
        }

        /// <summary>就任済みの役職を列挙（党首が居れば含む）。</summary>
        public static IEnumerable<PartyPost> FilledPosts(Party party)
        {
            if (party == null) yield break;
            if (party.leaderId >= 0) yield return PartyPost.党首;
            foreach (var a in party.posts)
                if (a != null && a.holderId >= 0) yield return a.post;
        }
    }
}
