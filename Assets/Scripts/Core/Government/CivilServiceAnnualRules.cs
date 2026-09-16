using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>年次人事で起きたことの種類（呼出側が通知の色分けに使う）。</summary>
    public enum CivilServiceAnnualKind
    {
        退職,   // 死亡・拘束・他勢力化などで職を離れた（整理）
        昇任,   // 空席を1つ上の段へ埋めた
        配属,   // 空いた配属定員へ入省させた
        見送り  // 埋められなかった（承認権者不在・資格不足・候補なし など）
    }

    /// <summary>年次人事の結果1件（純データ・そのまま通知文に組める）。</summary>
    [System.Serializable]
    public class CivilServiceAnnualEntry
    {
        public CivilServiceAnnualKind kind;
        /// <summary>対象の省（<see cref="Ministry.id"/>・-1＝省に紐づかない全体の見送り）。</summary>
        public int ministryId = -1;
        public string ministryName = "";
        /// <summary>対象の人物（<see cref="Person.id"/>・-1＝人物が定まらない見送り）。</summary>
        public int personId = -1;
        /// <summary>就いた（または埋められなかった）段。</summary>
        public BureaucratGrade grade = BureaucratGrade.一般官僚;
        /// <summary>理由（退職の事由・昇任/配属の承認の根拠・見送りの事由）。</summary>
        public string reason = "";
    }

    /// <summary>年次人事の結果（件数と明細）。呼出側はこれを読むだけで通知を組める。</summary>
    [System.Serializable]
    public class CivilServiceAnnualReport
    {
        public List<CivilServiceAnnualEntry> entries = new List<CivilServiceAnnualEntry>();
        public int retiredCount;
        public int promotedCount;
        public int assignedCount;
        public int skippedCount;
        /// <summary>上限で明細に載せなかった見送りの件数（黙って捨てない）。</summary>
        public int noticesDropped;

        public int TotalChanges => retiredCount + promotedCount + assignedCount;
    }

    /// <summary>年次人事の調整値（ゲーム用）。</summary>
    public readonly struct CivilServiceAnnualParams
    {
        /// <summary>1省あたり1年に通す昇任の上限（全段あわせて）。</summary>
        public readonly int maxPromotionsPerMinistry;
        /// <summary>1省あたり1年に通す新規配属（入省）の上限。</summary>
        public readonly int maxAssignmentsPerMinistry;
        /// <summary>明細に載せる見送りの上限（退職・昇任・配属は定員で上限があるため必ず載せる）。</summary>
        public readonly int maxNotices;

        public CivilServiceAnnualParams(int maxPromotionsPerMinistry, int maxAssignmentsPerMinistry, int maxNotices)
        {
            this.maxPromotionsPerMinistry = Mathf.Max(0, maxPromotionsPerMinistry);
            this.maxAssignmentsPerMinistry = Mathf.Max(0, maxAssignmentsPerMinistry);
            this.maxNotices = Mathf.Max(0, maxNotices);
        }

        /// <summary>既定＝1省あたり昇任2件・入省2件・見送りの明細は60件まで。</summary>
        public static CivilServiceAnnualParams Default => new CivilServiceAnnualParams(2, 2, 60);
    }

    /// <summary>
    /// 省内職位（<see cref="BureaucratGrade"/>）の<b>年次処理</b>の純ロジック（#141・Core のみ。UI・戦略マップの配線は後段）。
    /// <para>1年ぶんを「①失職整理 → ②上位の段から昇任 → ③一般官僚の空席補充」の順に回す。①以外はすべて既存の
    /// <see cref="CivilServicePostRules.Check"/>／<see cref="CivilServicePostRules.Execute"/> を通す＝資格（官位・考課・在職年）・
    /// 定員・内閣人事局の承認を自動処理が迂回しない（事務次官級は首相、局長級以下は所管大臣が承認者）。
    /// 権限者が不在なら埋めずに見送る＝自動処理は新しい権限を生まない。</para>
    /// <para>①の失職整理だけは裁量人事ではない（死亡・拘束・他勢力化・在野化・軍人化・政治家化・名簿消失）ため承認を要さないが、
    /// 整理専用の <see cref="CivilServicePostRules.RetireIfIneligible"/> に限定し、正常な在任者は退職させられない。</para>
    /// <para>同一人物の成功は1年に1回まで（上位の段から埋めるので1人が同じ年に2段上がらない）。候補の並びは決定論
    /// （昇任＝考課平均↓→現職在職年↓→官位↓→人物ID↑／配属＝考課平均↓→官位↓→人物ID↑）＝乱数を使わない。
    /// 内閣・<see cref="GovernmentRegistry"/>・軍・国庫には触れず、動かすのは人事台帳と <see cref="Ministry.staffIds"/> だけ。</para>
    /// <para>台帳へ移していない既存の配属（<see cref="Ministry.staffIds"/>）とも整合する：省の空きは
    /// <see cref="CivilServicePostRules.OccupiedStaffCount"/>（既存の配属と在任者の重複なし集合）で数え、入省の候補からは
    /// どこかの省に既に配属されている人物を除く＝年次処理が既存の配属者を黙って別省へ移さない。</para>
    /// </summary>
    public static class CivilServiceAnnualRules
    {
        /// <summary>昇任で空席を埋める順（上位の段から）。直下の段からのみ引き上げる。</summary>
        private static readonly BureaucratGrade[] PromotionOrder =
        {
            BureaucratGrade.事務次官級,
            BureaucratGrade.局長級,
            BureaucratGrade.課長級
        };

        /// <summary>候補の並べ替えに使う一時の値（台帳・人物は書き換えない）。</summary>
        private struct Candidate
        {
            public int personId;
            public float merit;     // 考課の平均（未評定＝0）
            public int tenure;      // 現職での在職年
            public CourtRank rank;  // 官位
        }

        /// <summary>
        /// 1年ぶんの年次人事を回す（唯一の入口）。null や空の入力でも例外にせず、見送りとして返して台帳を壊さない。
        /// </summary>
        public static CivilServiceAnnualReport TickYear(PoliticsState pol, Faction f, List<Ministry> tree,
            IList<Person> roster, int year, CivilServiceState st, CivilServicePostParams prm, CivilServiceAnnualParams ap)
        {
            var rep = new CivilServiceAnnualReport();
            if (st == null) { Skip(rep, ap, -1, "", -1, BureaucratGrade.一般官僚, "人事台帳がない＝年次人事を行わない"); return rep; }
            if (tree == null || tree.Count == 0) { Skip(rep, ap, -1, "", -1, BureaucratGrade.一般官僚, "省庁がない＝年次人事を行わない"); return rep; }
            if (roster == null || roster.Count == 0) { Skip(rep, ap, -1, "", -1, BureaucratGrade.一般官僚, "名簿がない＝年次人事を行わない"); return rep; }

            var acted = new HashSet<int>();                 // その年に既に動かした人物（成功は1年に1回まで）
            List<Ministry> ministries = SortedMinistries(tree);

            Retire(rep, f, tree, roster, year, st, prm, acted);
            Promote(rep, ap, pol, f, tree, ministries, roster, year, st, prm, acted);
            Assign(rep, ap, pol, f, tree, ministries, roster, year, st, prm, acted);
            return rep;
        }

        // ===== ① 失職整理 =====

        /// <summary>職に就き続けられなくなった在任者だけを退職させる（承認は要さない＝裁量人事ではない）。</summary>
        private static void Retire(CivilServiceAnnualReport rep, Faction f, List<Ministry> tree,
            IList<Person> roster, int year, CivilServiceState st, CivilServicePostParams prm, HashSet<int> acted)
        {
            if (st.records == null || st.records.Count == 0) return;
            var targets = new List<int>();
            var ministryIds = new List<int>();
            var ministryNames = new List<string>();
            var grades = new List<BureaucratGrade>();
            for (int i = 0; i < st.records.Count; i++)
            {
                CivilServiceRecord r = st.records[i];
                if (r == null || !r.IsServing) continue;
                targets.Add(r.personId);
                ministryIds.Add(r.ministryId);
                ministryNames.Add(r.ministryName ?? "");
                grades.Add(r.grade);
            }
            for (int i = 0; i < targets.Count; i++)
            {
                bool retired = CivilServicePostRules.RetireIfIneligible(tree, f, targets[i], roster, year,
                    "年次整理", st, prm, out string problem);
                if (!retired) continue; // 正常な在任者は動かさない
                acted.Add(targets[i]);
                rep.retiredCount++;
                rep.entries.Add(new CivilServiceAnnualEntry
                {
                    kind = CivilServiceAnnualKind.退職,
                    ministryId = ministryIds[i],
                    ministryName = ministryNames[i],
                    personId = targets[i],
                    grade = grades[i],
                    reason = problem ?? "在任できない"
                });
            }
        }

        // ===== ② 昇任（上位の段から） =====

        private static void Promote(CivilServiceAnnualReport rep, CivilServiceAnnualParams ap, PoliticsState pol, Faction f,
            List<Ministry> tree, List<Ministry> ministries, IList<Person> roster, int year,
            CivilServiceState st, CivilServicePostParams prm, HashSet<int> acted)
        {
            var promoted = new int[ministries.Count]; // 省ごとの昇任件数（年の上限）
            for (int g = 0; g < PromotionOrder.Length; g++)
            {
                BureaucratGrade grade = PromotionOrder[g];
                BureaucratGrade from = (BureaucratGrade)((int)grade - 1); // 直下の段からのみ
                for (int mi = 0; mi < ministries.Count; mi++)
                {
                    Ministry m = ministries[mi];
                    if (promoted[mi] >= ap.maxPromotionsPerMinistry) continue;
                    int vacancies = CivilServicePostRules.SlotsFor(m, grade, prm)
                                    - CivilServicePostRules.ServingCount(st, m.id, grade);
                    if (vacancies <= 0) continue;

                    int actor = ApproverFor(pol, f, m.id, grade, roster, out string authProblem);
                    if (actor < 0)
                    {
                        Skip(rep, ap, m.id, m.ministryName, -1, grade, authProblem);
                        continue; // 権限を迂回しない＝埋めずに見送る
                    }

                    List<Candidate> cands = ServingCandidates(st, roster, m.id, from, year, acted);
                    if (cands.Count == 0)
                    {
                        Skip(rep, ap, m.id, m.ministryName, -1, grade, from + " に昇任させられる候補がいない");
                        continue;
                    }
                    for (int c = 0; c < cands.Count && vacancies > 0 && promoted[mi] < ap.maxPromotionsPerMinistry; c++)
                    {
                        int pid = cands[c].personId;
                        if (acted.Contains(pid)) continue; // 同一人物は1年に1回まで
                        AppointmentResult r = CivilServicePostRules.Execute(pol, f, actor, tree, m.id,
                            CivilServiceAction.昇任, pid, grade, roster, year, "年次人事（昇任）", st, prm);
                        if (!r.ok)
                        {
                            Skip(rep, ap, m.id, m.ministryName, pid, grade, r.reason);
                            continue; // 資格不足は飛ばして次の候補へ
                        }
                        acted.Add(pid);
                        promoted[mi]++;
                        vacancies--;
                        rep.promotedCount++;
                        rep.entries.Add(new CivilServiceAnnualEntry
                        {
                            kind = CivilServiceAnnualKind.昇任,
                            ministryId = m.id,
                            ministryName = m.ministryName ?? "",
                            personId = pid,
                            grade = grade,
                            reason = r.reason
                        });
                    }
                }
            }
        }

        // ===== ③ 一般官僚の空席補充 =====

        private static void Assign(CivilServiceAnnualReport rep, CivilServiceAnnualParams ap, PoliticsState pol, Faction f,
            List<Ministry> tree, List<Ministry> ministries, IList<Person> roster, int year,
            CivilServiceState st, CivilServicePostParams prm, HashSet<int> acted)
        {
            List<Candidate> pool = null; // 未配属の適格者は勢力で1つ（省ごとに作り直さない）
            for (int mi = 0; mi < ministries.Count; mi++)
            {
                Ministry m = ministries[mi];
                // 空きは既存の配属（staffIds）と台帳の在任者を重ねずに数える＝台帳へ移していない配属で満員の省を空と見ない
                int free = Mathf.Max(0, m.staffSlots) - CivilServicePostRules.OccupiedStaffCount(m, st);
                if (free <= 0) continue;

                int actor = ApproverFor(pol, f, m.id, BureaucratGrade.一般官僚, roster, out string authProblem);
                if (actor < 0)
                {
                    Skip(rep, ap, m.id, m.ministryName, -1, BureaucratGrade.一般官僚, authProblem);
                    continue;
                }
                if (pool == null) pool = UnassignedCandidates(st, tree, roster, f, acted);

                int done = 0;
                for (int c = 0; c < pool.Count && done < free && done < ap.maxAssignmentsPerMinistry; c++)
                {
                    int pid = pool[c].personId;
                    if (acted.Contains(pid)) continue; // 他省で配属済み／その年に動いた
                    if (CivilServicePostRules.FindServing(st, pid) != null) continue;
                    AppointmentResult r = CivilServicePostRules.Execute(pol, f, actor, tree, m.id,
                        CivilServiceAction.配属, pid, BureaucratGrade.一般官僚, roster, year, "年次人事（配属）", st, prm);
                    if (!r.ok)
                    {
                        // 一般官僚への配属に本人固有の資格はない＝拒否は省側の事情（定員・承認）＝この省は打ち切る
                        Skip(rep, ap, m.id, m.ministryName, pid, BureaucratGrade.一般官僚, r.reason);
                        break;
                    }
                    acted.Add(pid);
                    done++;
                    rep.assignedCount++;
                    rep.entries.Add(new CivilServiceAnnualEntry
                    {
                        kind = CivilServiceAnnualKind.配属,
                        ministryId = m.id,
                        ministryName = m.ministryName ?? "",
                        personId = pid,
                        grade = BureaucratGrade.一般官僚,
                        reason = r.reason
                    });
                }
                if (done == 0 && free > 0 && pool.Count == 0)
                    Skip(rep, ap, m.id, m.ministryName, -1, BureaucratGrade.一般官僚, "入省させられる未配属の文民がいない");
            }
        }

        // ===== 候補・承認者 =====

        /// <summary>その省のその段の在任者を候補に並べる（考課平均↓→現職在職年↓→官位↓→人物ID↑）。</summary>
        private static List<Candidate> ServingCandidates(CivilServiceState st, IList<Person> roster, int ministryId,
            BureaucratGrade grade, int year, HashSet<int> acted)
        {
            var list = new List<Candidate>();
            if (st.records == null) return list;
            for (int i = 0; i < st.records.Count; i++)
            {
                CivilServiceRecord r = st.records[i];
                if (r == null || !r.IsServing || r.ministryId != ministryId || r.grade != grade) continue;
                if (acted.Contains(r.personId)) continue;
                Person p = ElectionCycleRules.FindPerson(roster, r.personId);
                if (p == null) continue;
                list.Add(new Candidate
                {
                    personId = r.personId,
                    merit = MeritOf(p),
                    tenure = year - r.appointedYear,
                    rank = p.courtRank
                });
            }
            list.Sort(CompareCandidates);
            return list;
        }

        /// <summary>
        /// どの省にも就いていない適格な文民を候補に並べる（考課平均↓→官位↓→人物ID↑・在職年は問わない）。
        /// 台帳の在任者だけでなく、どこかの <see cref="Ministry.staffIds"/> に既にいる人物も除く
        /// ＝台帳へ移していない既存の配属者を年次処理が黙って別省へ移さない（移すなら台帳の在任者への明示の異動）。
        /// </summary>
        private static List<Candidate> UnassignedCandidates(CivilServiceState st, List<Ministry> tree, IList<Person> roster,
            Faction f, HashSet<int> acted)
        {
            var list = new List<Candidate>();
            for (int i = 0; i < roster.Count; i++)
            {
                Person p = roster[i];
                if (p == null || acted.Contains(p.id)) continue;
                if (CivilServicePostRules.PersonProblem(p, f) != null) continue; // 軍人・政治家・他勢力・在野・死亡・拘束は入省できない
                if (CivilServicePostRules.FindServing(st, p.id) != null) continue;
                if (CivilServicePostRules.FindStaffedMinistryId(tree, p.id) >= 0) continue; // 既存の配属者は動かさない
                list.Add(new Candidate { personId = p.id, merit = MeritOf(p), tenure = 0, rank = p.courtRank });
            }
            list.Sort(CompareCandidates);
            return list;
        }

        private static float MeritOf(Person p)
            => p != null && p.merit != null && p.merit.HasRecord ? p.merit.AverageScore : 0f;

        private static int CompareCandidates(Candidate a, Candidate b)
        {
            int m = b.merit.CompareTo(a.merit);          // 考課平均の高い順
            if (m != 0) return m;
            if (a.tenure != b.tenure) return b.tenure.CompareTo(a.tenure); // 在職年の長い順
            int rc = JapaneseCourtRankRules.Compare(a.rank, b.rank);       // 正＝a が上位
            if (rc != 0) return -rc;                     // 官位の高い順
            return a.personId.CompareTo(b.personId);     // 最後は人物IDの小さい順（決定論）
        }

        /// <summary>その段の承認権者（事務次官級＝正式首相／局長級以下＝所管大臣）。不在なら -1 と理由を返す。</summary>
        private static int ApproverFor(PoliticsState pol, Faction f, int ministryId, BureaucratGrade grade,
            IList<Person> roster, out string problem)
        {
            if (grade == BureaucratGrade.事務次官級)
            {
                int premier = CabinetAppointmentRules.FormalPremier(pol, f, roster, out string premierProblem);
                problem = premier >= 0 ? null : "承認権者（内閣人事局＝首相）が不在：" + premierProblem;
                return premier;
            }
            CabinetState cab = pol != null ? pol.cabinet : null;
            CabinetPost minister = CabinetAppointmentRules.FindPost(cab, ministryId, CabinetPostKind.大臣);
            int holder = minister != null ? minister.holderId : -1;
            problem = holder >= 0 ? null : "承認権者（所管大臣）が空席＝" + grade + " の人事を承認できない";
            return holder;
        }

        // ===== 明細 =====

        /// <summary>省庁を ID 昇順に並べる（走査の順が保存順に左右されない＝決定論）。</summary>
        private static List<Ministry> SortedMinistries(List<Ministry> tree)
        {
            var list = new List<Ministry>();
            for (int i = 0; i < tree.Count; i++)
                if (tree[i] != null) list.Add(tree[i]);
            list.Sort((a, b) => a.id.CompareTo(b.id));
            return list;
        }

        /// <summary>見送りを積む（上限を超えたぶんは件数だけ数えて明細に載せない＝黙って捨てない）。</summary>
        private static void Skip(CivilServiceAnnualReport rep, CivilServiceAnnualParams ap, int ministryId, string ministryName,
            int personId, BureaucratGrade grade, string reason)
        {
            int shown = rep.skippedCount - rep.noticesDropped; // 明細に載っている見送りの件数
            rep.skippedCount++;
            if (shown >= ap.maxNotices) { rep.noticesDropped++; return; }
            rep.entries.Add(new CivilServiceAnnualEntry
            {
                kind = CivilServiceAnnualKind.見送り,
                ministryId = ministryId,
                ministryName = ministryName ?? "",
                personId = personId,
                grade = grade,
                reason = reason ?? ""
            });
        }

    }
}
