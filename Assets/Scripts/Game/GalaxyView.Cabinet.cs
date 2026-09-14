using System.Collections.Generic;
using System.Text;

namespace Ginei
{
    public partial class GalaxyView
    {
        // ===== 内閣の政治任用（大臣・副大臣・政務官）と党三役の配線（#2768 #141 #159 #145） =====
        // 在任・委任・履歴は PoliticsState.cabinet / Party.posts（戦役セーブに乗る）が単一の出所。GovernmentRegistry へは登録しない
        // （軍事所掌の役職として登録すると全軍の指揮権に数えられるため＝閣僚職は艦隊・軍団の作戦指揮権を含まない）。
        // 省庁の職業官僚の配属（Ministry.staffIds）には触れない。

        private static readonly CabinetParams CabinetPrm = CabinetParams.Default;

        /// <summary>
        /// 内閣と党三役を現況へ合わせ（首相交代の総辞職・首相不在の職務執行・死亡/不在/離党の失職・党首交代の改任・委任の失効）、
        /// <paramref name="autoFill"/> なら首相・党首を任命者として空席を補充する（プレイヤー勢力も同じ入口＝手動任免の操作 UI は未配線）。
        /// 同じ状態で繰り返しても履歴・通知は増えない。<paramref name="notify"/> が false なら通知しない（読込時）。
        /// </summary>
        private void RunCabinetAndPartyExecutives(FactionState s, int year, List<Person> roster, bool notify, bool autoFill)
        {
            if (s == null || s.politics == null) return;
            SeedMinistries();
            int idx = FactionIndex(s.faction);
            List<Ministry> tree = ministries != null && idx >= 0 && idx < ministries.Length ? ministries[idx] : null;
            int top = TopMinistryIdOf(s.faction);

            var changes = new List<AppointmentHistoryEntry>();
            var partyChanges = new List<AppointmentHistoryEntry>();
            partyChanges.AddRange(PartyExecutiveRules.Reconcile(s.politics, s.faction, roster, year, CabinetPrm));
            changes.AddRange(CabinetAppointmentRules.Reconcile(s.politics, s.faction, tree, top, roster, year, CabinetPrm));
            if (autoFill)
            {
                // 組閣を先に（閣僚を決めてから残る党員で三役）。どちらも兼任しない。
                changes.AddRange(CabinetAppointmentRules.AutoFill(s.politics, s.faction, tree, top, roster, year, CabinetPrm));
                partyChanges.AddRange(PartyExecutiveRules.AutoFill(s.politics, s.faction, roster, year, CabinetPrm));
            }
            if (!notify) return;
            NotifyCabinet(s, changes);
            NotifyPartyExecutives(s, partyChanges);
        }

        /// <summary>内閣の変化を通知する（就任はまとめて1通・総辞職/職務執行/失職は1件ずつ）。</summary>
        private void NotifyCabinet(FactionState s, List<AppointmentHistoryEntry> changes)
        {
            if (changes == null || changes.Count == 0) return;
            var appointed = new StringBuilder();
            int count = 0;
            for (int i = 0; i < changes.Count; i++)
            {
                AppointmentHistoryEntry e = changes[i];
                switch (e.action)
                {
                    case "就任":
                    case "続投":
                        if (count < MaxCabinetNamesInNotice)
                        {
                            if (count > 0) appointed.Append('・');
                            appointed.Append(e.postLabel).Append(' ').Append(ElectionPersonName(e.personId));
                            if (e.action == "続投") appointed.Append("（続投）");
                        }
                        count++;
                        break;
                    case "総辞職":
                    case "職務執行":
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意, $"{s.faction} 内閣{e.action}：{e.reason}");
                        break;
                    case "失職":
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                            $"{s.faction} {e.postLabel} {ElectionPersonName(e.personId)} 失職（{e.reason}）");
                        break;
                }
            }
            if (count > 0)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{s.faction} 内閣の任命 {count}名：{appointed}{(count > MaxCabinetNamesInNotice ? $" ほか{count - MaxCabinetNamesInNotice}名" : "")}");
        }

        /// <summary>党三役の変化を党ごとに1通へまとめて通知する。</summary>
        private void NotifyPartyExecutives(FactionState s, List<AppointmentHistoryEntry> changes)
        {
            if (changes == null || changes.Count == 0 || s.politics == null) return;
            for (int p = 0; p < s.politics.parties.Count; p++)
            {
                Party party = s.politics.parties[p];
                if (party == null) continue;
                var sb = new StringBuilder();
                int n = 0;
                for (int i = 0; i < changes.Count; i++)
                {
                    AppointmentHistoryEntry e = changes[i];
                    if (!e.eventId.Contains(":党" + party.id + ":")) continue;
                    if (e.action == "暫定") continue;
                    if (n > 0) sb.Append('・');
                    sb.Append(e.postLabel).Append(' ').Append(ElectionPersonName(e.personId)).Append(' ').Append(e.action);
                    if (e.action != "就任" && e.action != "続投") sb.Append('（').Append(e.reason).Append('）');
                    n++;
                }
                if (n > 0)
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{s.faction} {party.partyName} 党三役：{sb}");
            }
        }

        private const int MaxCabinetNamesInNotice = 6;

        /// <summary>
        /// 閣僚の決裁権限の判定材料（#2768 #67）。決裁・上申の確定・見込み表示のたびに組み直す（読み取りのみ・省庁のシードもしない）。
        /// 内閣が置かれていなければ null＝従来の役職判定だけになる。
        /// </summary>
        public CabinetDecisionContext CabinetDecisionContextOf(Faction f)
        {
            FactionState s = StateOf(f);
            if (s == null || s.politics == null || s.politics.cabinet == null) return null;
            int idx = FactionIndex(f);
            List<Ministry> tree = ministries != null && idx >= 0 && idx < ministries.Length ? ministries[idx] : null;
            return new CabinetDecisionContext(s.politics, f, tree, TopMinistryIdOf(f), ElectionRoster(), ElectionYear());
        }
    }
}
