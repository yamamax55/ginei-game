using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>党の役職で問う行為（党内の権限と政府・軍の権限を分けて固定するための列挙）。</summary>
    public enum PartyExecutiveAction
    {
        党役職任免,
        党運営,
        選挙候補調整,
        政策調整,
        党内合意,
        政府決裁,
        閣僚任免,
        艦隊作戦指揮,
        国庫支出
    }

    /// <summary>
    /// 党三役（幹事長・政調会長・総務会長）の任免の純ロジック（#2768 #159・唯一の窓口）。在任は既存の <see cref="Party.posts"/>、
    /// 就任操作は <see cref="PartyOrganizationRules.AppointPost"/>、党首は <see cref="Party.leaderId"/> が単一の出所（ここでは選ばない＝総裁選）。
    /// 任免権者は現党首だけ、就けるのは同じ党の党員で生存・自由な政治家。幹事長＝党務と選挙の候補調整、政調会長＝政策の取りまとめ、
    /// 総務会長＝党内の合意形成。党の役職では政府の決裁・閣僚任免・国庫・艦隊の作戦指揮を許さない。
    /// 党首が替われば三役は新党首が改めて任命する（続投は再任で記録）、党首不在の間は期限つきの暫定。決定論・test-first。
    /// </summary>
    public static class PartyExecutiveRules
    {
        /// <summary>党三役（任免の対象）。</summary>
        public static readonly PartyPost[] ExecutivePosts = { PartyPost.幹事長, PartyPost.政調会長, PartyPost.総務会長 };

        /// <summary>職の役割の説明。</summary>
        public static string RoleText(PartyPost post)
        {
            switch (post)
            {
                case PartyPost.幹事長: return "党務・選挙の候補調整・党内支持の取りまとめ";
                case PartyPost.政調会長: return "綱領・政策案の集約と政府への政策上申の取りまとめ（所管大臣の決裁は代替しない）";
                case PartyPost.総務会長: return "党内の政策・人事の合意形成（政府の裁可とは別）";
                case PartyPost.党首: return "党の長（総裁選で選ぶ）";
                default: return "党の役職";
            }
        }

        /// <summary>その党でその人物が就く三役（無ければ null）。</summary>
        public static PartyAppointment AppointmentOf(Party party, PartyPost post)
        {
            if (party == null || party.posts == null) return null;
            for (int i = 0; i < party.posts.Count; i++)
                if (party.posts[i] != null && party.posts[i].post == post && party.posts[i].holderId >= 0) return party.posts[i];
            return null;
        }

        /// <summary>党首が任免権者として有効か（有効なら党首ID、無ければ -1 と理由）。</summary>
        public static int FormalLeader(Party party, Faction f, IList<Person> roster, out string problem)
        {
            problem = null;
            if (party == null) { problem = "党がない"; return -1; }
            if (party.leaderId < 0) { problem = "党首が不在（総裁選の選出待ち）"; return -1; }
            if (!PartyOrganizationRules.IsMember(party, party.leaderId)) { problem = "党首が党籍を持たない"; return -1; }
            string pp = CabinetAppointmentRules.PersonProblem(ElectionCycleRules.FindPerson(roster, party.leaderId), f);
            if (pp != null) { problem = "党首（人物#" + party.leaderId + "）が職務を続けられない（" + pp + "）"; return -1; }
            return party.leaderId;
        }

        /// <summary>
        /// 党三役に任命する（唯一の入口・AI も通す）。対象の職（三役だけ）・任免権者（有効な党首本人）・空席・党籍・人物の資格・
        /// 兼任（党首本人／首相／知事／他の三役／閣僚）を確かめ、拒否は理由つきで何も変えない。党首以外の任命は党首への提案として返す。
        /// </summary>
        public static AppointmentResult TryAppoint(PoliticsState pol, Faction f, Party party, int actorId, PartyPost post, int personId,
            IList<Person> roster, int year, string reason, CabinetParams prm)
        {
            if (party == null) return AppointmentResult.Deny("党がない");
            if (post == PartyPost.党首) return AppointmentResult.Deny("党首は総裁選で選ぶ（任命しない）");
            if (!PartyOrganizationRules.IsThreeLeadership(post)) return AppointmentResult.Deny(post + " は今回の任免の対象外（党三役のみ）");
            int leader = FormalLeader(party, f, roster, out string leaderProblem);
            if (leader < 0) return AppointmentResult.Deny("任免権者（党首）が不在：" + leaderProblem);
            if (actorId != leader)
                return AppointmentResult.Petition("権限外：党三役の任免権者は党首（人物#" + leader + "）＝提案として扱う", leader);
            PartyAppointment current = AppointmentOf(party, post);
            if (current != null)
                return AppointmentResult.Deny(current.holderId == personId ? "既に " + post + " に在任している"
                                                                          : post + " には在任者（人物#" + current.holderId + "）がいる＝先に解任");
            string problem = CandidateProblem(pol, f, party, personId, roster, prm);
            if (problem != null) return AppointmentResult.Deny(problem);

            bool continued = WasJustVacatedBy(party, post, personId, year);
            if (!PartyOrganizationRules.AppointPost(party, post, personId)) return AppointmentResult.Deny("党籍がない");
            PartyAppointment a = AppointmentOf(party, post);
            a.appointedYear = year;
            a.appointedById = actorId;
            a.reason = reason ?? "";
            a.caretakerUntilYear = 0;
            RemoveVacancyNote(party, post);
            AddHistory(party, f, year, continued ? "続投" : "就任", post, personId, actorId, a.reason, prm);
            return AppointmentResult.Allow(party.partyName + " " + post + " に人物#" + personId + " を任命（党首の任命）");
        }

        /// <summary>三役に就けない理由（就けるなら null）。任免権者・空席は見ない。</summary>
        public static string CandidateProblem(PoliticsState pol, Faction f, Party party, int personId, IList<Person> roster, CabinetParams prm)
        {
            if (!PartyOrganizationRules.IsMember(party, personId)) return "党員でない（党籍が必要）";
            string pp = CabinetAppointmentRules.PersonProblem(ElectionCycleRules.FindPerson(roster, personId), f);
            if (pp != null) return "任命できない：" + pp;
            if (party.leaderId == personId) return "党首は三役を兼ねない";
            if (pol != null && pol.government != null && pol.government.premierPersonId == personId) return "首相は党三役を兼ねない";
            int sys = LocalElectionRules.GovernedSystemOf(pol, personId, -1);
            if (sys >= 0) return "星系#" + sys + " の知事と党三役は兼任しない";
            for (int k = 0; k < ExecutivePosts.Length; k++)
                if (PartyOrganizationRules.HolderOf(party, ExecutivePosts[k]) == personId)
                    return "兼任不可：既に " + ExecutivePosts[k] + " に在任";
            if (!prm.allowPartyExecutiveConcurrent && pol != null)
            {
                CabinetPost held = CabinetAppointmentRules.PostHeldBy(pol.cabinet, personId);
                if (held != null) return "閣僚（" + CabinetAppointmentRules.PostTitle(held) + "）と党三役は兼任しない";
            }
            return null;
        }

        /// <summary>党三役を解任する（党首本人のみ）。</summary>
        public static AppointmentResult Dismiss(Party party, Faction f, int actorId, PartyPost post, IList<Person> roster, int year,
            string reason, CabinetParams prm)
        {
            if (party == null) return AppointmentResult.Deny("党がない");
            if (!PartyOrganizationRules.IsThreeLeadership(post)) return AppointmentResult.Deny(post + " は今回の任免の対象外");
            int leader = FormalLeader(party, f, roster, out string leaderProblem);
            if (leader < 0) return AppointmentResult.Deny("任免権者（党首）が不在：" + leaderProblem);
            if (actorId != leader)
                return AppointmentResult.Petition("権限外：党三役の任免権者は党首（人物#" + leader + "）＝提案として扱う", leader);
            PartyAppointment a = AppointmentOf(party, post);
            if (a == null) return AppointmentResult.Deny(post + " は既に空席");
            int who = a.holderId;
            Vacate(party, f, post, who, year, "解任", string.IsNullOrEmpty(reason) ? "党首による解任" : reason, actorId, prm);
            return AppointmentResult.Allow(party.partyName + " " + post + " の人物#" + who + " を解任");
        }

        /// <summary>
        /// その人物がその党でその行為をできるか（状態は変えない）。党首＝党役職の任免と党内の行為全般、幹事長＝党運営・選挙候補調整、
        /// 政調会長＝政策調整、総務会長＝党内合意。党首不在の暫定は期限まで。政府決裁・閣僚任免・国庫・艦隊の作戦指揮は党の役職では許さない。
        /// </summary>
        public static AppointmentResult Authority(Party party, Faction f, int personId, PartyExecutiveAction action, IList<Person> roster, int year)
        {
            if (action == PartyExecutiveAction.政府決裁 || action == PartyExecutiveAction.閣僚任免
                || action == PartyExecutiveAction.艦隊作戦指揮 || action == PartyExecutiveAction.国庫支出)
                return AppointmentResult.Deny("党の役職は政府の決裁・閣僚任免・国庫・軍の作戦指揮権を含まない（政府の役職とは別）");
            if (party == null || !PartyOrganizationRules.IsMember(party, personId)) return AppointmentResult.Deny("権限なし：党員でない");
            string pp = CabinetAppointmentRules.PersonProblem(ElectionCycleRules.FindPerson(roster, personId), f);
            if (pp != null) return AppointmentResult.Deny("権限なし：" + pp);

            if (party.leaderId == personId) return AppointmentResult.Allow("党首（" + party.partyName + " の長）");
            if (action == PartyExecutiveAction.党役職任免)
                return AppointmentResult.Petition("権限外：党役職の任免は党首だけ", party.leaderId);

            for (int k = 0; k < ExecutivePosts.Length; k++)
            {
                PartyAppointment a = AppointmentOf(party, ExecutivePosts[k]);
                if (a == null || a.holderId != personId) continue;
                if (!Covers(a.post, action))
                    return AppointmentResult.Petition(a.post + " の所掌外（" + RoleText(a.post) + "）", party.leaderId);
                if (a.caretakerUntilYear > 0)
                {
                    if (year > a.caretakerUntilYear) return AppointmentResult.Deny(a.post + " の暫定期限（SE" + a.caretakerUntilYear + "）を過ぎた");
                    return AppointmentResult.Allow(a.post + "（党首不在の暫定・SE" + a.caretakerUntilYear + "まで）");
                }
                return AppointmentResult.Allow(a.post + "（" + RoleText(a.post) + "）");
            }
            return AppointmentResult.Petition("党役職に就いていない＝提案として党首へ", party.leaderId);
        }

        private static bool Covers(PartyPost post, PartyExecutiveAction action)
        {
            switch (post)
            {
                case PartyPost.幹事長: return action == PartyExecutiveAction.党運営 || action == PartyExecutiveAction.選挙候補調整;
                case PartyPost.政調会長: return action == PartyExecutiveAction.政策調整;
                case PartyPost.総務会長: return action == PartyExecutiveAction.党内合意;
                default: return false;
            }
        }

        /// <summary>
        /// 党三役を現況へ合わせる（任命はしない）：党籍を失った・死亡/拘束・党首/首相/知事に就いた在任者は失職、党首が替われば新党首の改任待ちで退任、
        /// 党首不在の間は期限つきの暫定→期限切れで退任。旧セーブの任命（任命者不明）は現党首の任命として続ける。返り値は加えた履歴。
        /// </summary>
        public static List<AppointmentHistoryEntry> Reconcile(PoliticsState pol, Faction f, IList<Person> roster, int year, CabinetParams prm)
        {
            var added = new List<AppointmentHistoryEntry>();
            if (pol == null || pol.parties == null) return added;
            for (int i = 0; i < pol.parties.Count; i++)
            {
                Party party = pol.parties[i];
                if (party == null) continue;
                EnsureLists(party);
                int before = party.postHistory.Count;
                int dropped = party.postHistoryDropped;
                int leader = party.leaderId;

                for (int k = 0; k < ExecutivePosts.Length; k++)
                {
                    PartyPost post = ExecutivePosts[k];
                    PartyAppointment a = AppointmentOf(party, post);
                    if (a == null)
                    {
                        // 離党・移籍などで名簿から外れた（PartyOrganizationRules.Leave が就任を消す）＝直前の就任の記録から失職を残す
                        AppointmentHistoryEntry last = LastEntry(party, post);
                        if (last != null && (last.action == "就任" || last.action == "続投" || last.action == "暫定"))
                        {
                            string why = PartyOrganizationRules.IsMember(party, last.personId) ? "在任が外れた" : "党籍を失った（離党・移籍など）";
                            AddHistory(party, f, year, "失職", post, last.personId, -1, why, prm);
                            SetVacancyNote(party, f, post, year, why);
                        }
                        continue;
                    }
                    string problem = null;
                    if (!PartyOrganizationRules.IsMember(party, a.holderId)) problem = "党籍を失った";
                    else
                    {
                        string pp = CabinetAppointmentRules.PersonProblem(ElectionCycleRules.FindPerson(roster, a.holderId), f);
                        if (pp != null) problem = pp;
                        else if (a.holderId == leader) problem = "党首に就いた（三役を兼ねない）";
                        else if (pol.government != null && pol.government.premierPersonId == a.holderId) problem = "首相に就いた（三役を兼ねない）";
                        else
                        {
                            int sys = LocalElectionRules.GovernedSystemOf(pol, a.holderId, -1);
                            if (sys >= 0) problem = "星系#" + sys + " の知事に就いた（三役を兼ねない）";
                        }
                    }
                    if (problem != null)
                    {
                        Vacate(party, f, post, a.holderId, year, "失職", problem + "のため失職", -1, prm);
                        continue;
                    }
                    if (a.appointedById < 0)
                    {
                        // 旧セーブの任命：任命者は不明＝現党首の任命として続ける（再任命・履歴は起こさない）
                        if (leader >= 0)
                        {
                            a.appointedById = leader;
                            if (string.IsNullOrEmpty(a.reason)) a.reason = "旧セーブの任命（任命者不明＝現党首の任命として続行）";
                        }
                        continue;
                    }
                    if (leader >= 0 && a.appointedById != leader)
                    {
                        Vacate(party, f, post, a.holderId, year, "党首交代",
                            "党首交代（前 人物#" + a.appointedById + " → 人物#" + leader + "）＝三役は新党首が改めて任命", -1, prm);
                        continue;
                    }
                    if (leader < 0)
                    {
                        if (a.caretakerUntilYear <= 0)
                        {
                            a.caretakerUntilYear = year + prm.caretakerYears;
                            AddHistory(party, f, year, "暫定", post, a.holderId, -1, "党首不在＝SE" + a.caretakerUntilYear + "まで所掌の範囲で暫定", prm);
                        }
                        else if (year > a.caretakerUntilYear)
                            Vacate(party, f, post, a.holderId, year, "失職", "党首不在の暫定期限（SE" + a.caretakerUntilYear + "）を過ぎた", -1, prm);
                    }
                    else if (a.caretakerUntilYear > 0) a.caretakerUntilYear = 0; // 同じ党首が戻った
                }
                CabinetAppointmentRules.CollectAdded(party.postHistory, before, party.postHistoryDropped - dropped, added);
            }
            return added;
        }

        /// <summary>
        /// AI の党三役の補充：有効な党首を任命者として、空いた三役（幹事長→政調会長→総務会長）へ候補評価の最上位を <see cref="TryAppoint"/> で就ける。
        /// 候補がいなければ空席の理由だけを残す。先に <see cref="Reconcile"/> を呼ぶこと。
        /// </summary>
        public static List<AppointmentHistoryEntry> AutoFill(PoliticsState pol, Faction f, IList<Person> roster, int year, CabinetParams prm)
        {
            var added = new List<AppointmentHistoryEntry>();
            if (pol == null || pol.parties == null) return added;
            for (int i = 0; i < pol.parties.Count; i++)
            {
                Party party = pol.parties[i];
                if (party == null) continue;
                EnsureLists(party);
                int leader = FormalLeader(party, f, roster, out string leaderProblem);
                if (leader < 0)
                {
                    for (int k = 0; k < ExecutivePosts.Length; k++)
                        if (AppointmentOf(party, ExecutivePosts[k]) == null)
                            SetVacancyNote(party, f, ExecutivePosts[k], year, "任免権者（党首）不在：" + leaderProblem);
                    continue;
                }
                int before = party.postHistory.Count;
                int dropped = party.postHistoryDropped;
                var ids = new List<int>(party.memberIds);
                ids.Sort();
                for (int k = 0; k < ExecutivePosts.Length; k++)
                {
                    PartyPost post = ExecutivePosts[k];
                    if (AppointmentOf(party, post) != null) continue;
                    int bestId = -1;
                    float bestScore = 0f;
                    string bestReason = "";
                    for (int m = 0; m < ids.Count; m++)
                    {
                        if (m > 0 && ids[m] == ids[m - 1]) continue;
                        if (CandidateProblem(pol, f, party, ids[m], roster, prm) != null) continue;
                        float score = Score(pol, party, ElectionCycleRules.FindPerson(roster, ids[m]), prm, out string why);
                        if (bestId < 0 || score > bestScore) { bestId = ids[m]; bestScore = score; bestReason = why; }
                    }
                    if (bestId < 0)
                    {
                        SetVacancyNote(party, f, post, year, "適格な候補なし（生存・自由な党員の政治家で、党首・首相・知事・他の三役・閣僚でない人物が足りない）");
                        continue;
                    }
                    AppointmentResult r = TryAppoint(pol, f, party, leader, post, bestId, roster, year, "党首の任命（選定理由：" + bestReason + "）", prm);
                    if (!r.ok) SetVacancyNote(party, f, post, year, r.reason);
                }
                CabinetAppointmentRules.CollectAdded(party.postHistory, before, party.postHistoryDropped - dropped, added);
            }
            return added;
        }

        /// <summary>三役の候補評価＝能力＋年功（当選回数の逓減）＋簡易の派閥均衡。</summary>
        public static float Score(PoliticsState pol, Party party, Person p, CabinetParams prm, out string reason)
        {
            reason = "";
            if (p == null) return 0f;
            float ability = CabinetAppointmentRules.AbilityOf(p);
            SeniorityInfo si = PartySeniorityRules.InfoOf(pol, p.id, PartySeniorityParams.Default);
            PartyFaction pf = CabinetAppointmentRules.FactionOf(party, p.id);
            int factionPosts = 0;
            if (pf != null)
                for (int k = 0; k < ExecutivePosts.Length; k++)
                {
                    int h = PartyOrganizationRules.HolderOf(party, ExecutivePosts[k]);
                    if (h >= 0 && pf.memberIds.Contains(h)) factionPosts++;
                }
            float score = prm.abilityWeight * ability + prm.seniorityWeight * si.standing
                        + (si.historyRegistered && si.tier == SeniorityTier.ベテラン ? prm.tierFitBonus : 0f)
                        + (pf != null ? prm.factionBalanceBonus / (1 + factionPosts) : 0f);
            reason = "能力" + ability.ToString("0.00")
                   + "・" + (si.historyRegistered ? "国政当選" + si.nationalWins + "回（" + si.tier + "）" : "当選履歴未登録")
                   + (pf != null ? "・" + pf.name + "（三役" + factionPosts + "名）" : "・無派閥")
                   + "・評価" + score.ToString("0.000");
            return score;
        }

        /// <summary>セーブ読込の穴埋め（読込だけでは任命しない）。</summary>
        public static void NormalizeLoaded(PoliticsState pol)
        {
            if (pol == null || pol.parties == null) return;
            for (int i = 0; i < pol.parties.Count; i++)
            {
                Party p = pol.parties[i];
                if (p == null) continue;
                EnsureLists(p);
                CabinetAppointmentRules.NormalizeHistory(p.postHistory);
                CabinetAppointmentRules.NormalizeHistory(p.postVacancyNotes);
                if (p.posts != null)
                    for (int k = 0; k < p.posts.Count; k++)
                        if (p.posts[k] != null && p.posts[k].reason == null) p.posts[k].reason = "";
            }
        }

        // ===== 内部 =====

        private static void EnsureLists(Party party)
        {
            if (party.posts == null) party.posts = new List<PartyAppointment>();
            if (party.postHistory == null) party.postHistory = new List<AppointmentHistoryEntry>();
            if (party.postVacancyNotes == null) party.postVacancyNotes = new List<AppointmentHistoryEntry>();
        }

        private static void Vacate(Party party, Faction f, PartyPost post, int personId, int year, string action, string reason, int actorId, CabinetParams prm)
        {
            AddHistory(party, f, year, action, post, personId, actorId, reason, prm);
            for (int i = party.posts.Count - 1; i >= 0; i--)
                if (party.posts[i] != null && party.posts[i].post == post) party.posts.RemoveAt(i);
            SetVacancyNote(party, f, post, year, reason);
        }

        private static AppointmentHistoryEntry LastEntry(Party party, PartyPost post)
        {
            if (party.postHistory == null) return null;
            string label = post.ToString();
            for (int i = party.postHistory.Count - 1; i >= 0; i--)
            {
                AppointmentHistoryEntry e = party.postHistory[i];
                if (e != null && e.postLabel == label) return e;
            }
            return null;
        }

        private static bool WasJustVacatedBy(Party party, PartyPost post, int personId, int year)
        {
            AppointmentHistoryEntry last = LastEntry(party, post);
            return last != null && last.year == year && last.personId == personId && last.action == "党首交代";
        }

        private static void SetVacancyNote(Party party, Faction f, PartyPost post, int year, string reason)
        {
            string label = post.ToString();
            for (int i = 0; i < party.postVacancyNotes.Count; i++)
            {
                AppointmentHistoryEntry e = party.postVacancyNotes[i];
                if (e == null || e.postLabel != label) continue;
                if (e.reason != reason) { e.reason = reason ?? ""; e.year = year; }
                return;
            }
            party.postVacancyNotes.Add(new AppointmentHistoryEntry
            {
                eventId = (int)f + ":党" + party.id + ":空席:" + label, year = year, action = "空席", postLabel = label, reason = reason ?? "",
            });
        }

        private static void RemoveVacancyNote(Party party, PartyPost post)
        {
            string label = post.ToString();
            for (int i = party.postVacancyNotes.Count - 1; i >= 0; i--)
                if (party.postVacancyNotes[i] == null || party.postVacancyNotes[i].postLabel == label) party.postVacancyNotes.RemoveAt(i);
        }

        /// <summary>空席の理由（無ければ空）。表示用。</summary>
        public static string VacancyReason(Party party, PartyPost post)
        {
            if (party == null || party.postVacancyNotes == null) return "";
            string label = post.ToString();
            for (int i = 0; i < party.postVacancyNotes.Count; i++)
                if (party.postVacancyNotes[i] != null && party.postVacancyNotes[i].postLabel == label) return party.postVacancyNotes[i].reason;
            return "";
        }

        private static void AddHistory(Party party, Faction f, int year, string action, PartyPost post, int personId, int actorId, string reason, CabinetParams prm)
        {
            var e = new AppointmentHistoryEntry
            {
                eventId = (int)f + ":党" + party.id + ":" + year + ":" + post + ":" + personId + ":" + action,
                year = year, action = action, postLabel = post.ToString(), personId = personId, actorId = actorId, reason = reason ?? "",
            };
            CabinetAppointmentRules.AppendCapped(party.postHistory, e, prm.maxHistory, ref party.postHistoryDropped);
        }
    }
}
