using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>省内職位の人事で問う行為（配属＝入省、異動＝他省へ、昇任/降任＝隣の段へ、解任＝職を解く）。</summary>
    public enum CivilServiceAction
    {
        配属,
        異動,
        昇任,
        降任,
        解任
    }

    /// <summary>省内職位の人事の調整値（ゲーム用。現実の公式な規則ではない）。</summary>
    public readonly struct CivilServicePostParams
    {
        /// <summary>保持する退任記録の上限（勢力ごと）。</summary>
        public readonly int maxHistory;
        /// <summary>課長級の定員（省あたり）。</summary>
        public readonly int sectionChiefSlots;
        /// <summary>局長級の定員（省あたり）。</summary>
        public readonly int bureauChiefSlots;
        /// <summary>事務次官級の定員（省あたり。1＝事務方の長は一人）。</summary>
        public readonly int viceMinisterSlots;
        /// <summary>昇任に要する現職位での最低在職年（段が1つ上がるごとにこの年数ぶん増える）。</summary>
        public readonly int minTenureYears;
        /// <summary>昇任に要する考第の平均（課長級の下限。段が1つ上がるごとに1点ずつ厳しくなる）。</summary>
        public readonly float minMeritScore;

        public CivilServicePostParams(int maxHistory, int sectionChiefSlots, int bureauChiefSlots, int viceMinisterSlots,
            int minTenureYears, float minMeritScore)
        {
            this.maxHistory = Mathf.Max(1, maxHistory);
            this.sectionChiefSlots = Mathf.Max(0, sectionChiefSlots);
            this.bureauChiefSlots = Mathf.Max(0, bureauChiefSlots);
            this.viceMinisterSlots = Mathf.Max(0, viceMinisterSlots);
            this.minTenureYears = Mathf.Max(0, minTenureYears);
            this.minMeritScore = Mathf.Max(0f, minMeritScore);
        }

        /// <summary>既定＝履歴60件・課長級4/局長級2/事務次官級1・在職3年きざみ・考第平均5.0（中中）から。</summary>
        public static CivilServicePostParams Default => new CivilServicePostParams(60, 4, 2, 1, 3, 5f);
    }

    /// <summary>
    /// 省内職位（<see cref="BureaucratGrade"/>）の任用・異動・昇任・降任・解任の純ロジック（#141・唯一の窓口）。
    /// <para>官位（<see cref="Person.courtRank"/>）・考課（<see cref="Person.merit"/>）・省庁の配属（<see cref="Ministry.staffIds"/>）という
    /// 既存の仕組みを再利用し、新たに持つのは「どの省のどの段に誰がいつから就いているか」の台帳（<see cref="CivilServiceState"/>）だけ。
    /// 内閣の政治任用（<see cref="CabinetAppointmentRules"/>）・官職（<see cref="GovernmentRegistry"/>）・軍の階級は複製しない。</para>
    /// <para><b>内閣人事局の承認</b>：事務次官級は首相、局長級以下は所管大臣が承認する。副大臣は大臣から所管決裁の委任を
    /// 受けている間だけ承認できる（判定は <see cref="CabinetAppointmentRules.Authority"/> に委ね、期限・委任者の在任もそこで見る）。
    /// 政務官・党三役・官僚本人は承認できない。承認しても艦隊の作戦指揮権・政府の決裁権は生じない（<see cref="GradeAuthority"/>）。</para>
    /// <para>確認（<see cref="Check"/>）と実行（<see cref="Execute"/>）は同じ判定を同じ順序で通る＝表示と実行が食い違わない。
    /// 確認と拒否される実行は台帳を一切書き換えない（<see cref="CivilServiceState.records"/> などが欠けていても作らない）。
    /// 自動昇任は本段階では行わない（AI・UI は後段）。決定論・test-first・状態は台帳と <see cref="Ministry.staffIds"/> のみ更新。</para>
    /// </summary>
    public static class CivilServicePostRules
    {
        /// <summary>段の数（一般官僚〜事務次官級）。</summary>
        public const int GradeCount = 4;

        // ===== 参照 =====

        /// <summary>職位の呼び名（省名の末尾「省」を除いて段を付ける。一般官僚は「○○省 職員」）。</summary>
        public static string GradeTitle(string ministryName, BureaucratGrade grade)
        {
            string b = ministryName ?? "";
            if (b.EndsWith("省", System.StringComparison.Ordinal)) b = b.Substring(0, b.Length - 1);
            switch (grade)
            {
                case BureaucratGrade.事務次官級: return b + "事務次官";
                case BureaucratGrade.局長級: return b + "局長";
                case BureaucratGrade.課長級: return b + "課長";
                default: return b + "省 職員";
            }
        }

        /// <summary>その段に就くのに要る官位（一般官僚は無位でよい＝入省の段）。</summary>
        public static CourtRank RequiredRank(BureaucratGrade grade)
        {
            switch (grade)
            {
                case BureaucratGrade.事務次官級: return CourtRank.正五位下;
                case BureaucratGrade.局長級: return CourtRank.従五位下; // 五位の壁の上＝貴族
                case BureaucratGrade.課長級: return CourtRank.正七位上;
                default: return CourtRank.無位;
            }
        }

        /// <summary>その段に就くのに要る考第の平均（一般官僚は不問＝0）。段が1つ上がるごとに1点きざみで厳しくなる。</summary>
        public static float RequiredMerit(BureaucratGrade grade, CivilServicePostParams prm)
            => grade == BureaucratGrade.一般官僚 ? 0f : prm.minMeritScore + ((int)grade - 1);

        /// <summary>その段に就くのに要る、いま就いている段での最低在職年（一般官僚＝入省なので0）。</summary>
        public static int RequiredTenureYears(BureaucratGrade grade, CivilServicePostParams prm)
            => prm.minTenureYears * (int)grade;

        /// <summary>省あたりのその段の定員（一般官僚は省の配属定員 <see cref="Ministry.staffSlots"/> の枠内）。</summary>
        public static int SlotsFor(Ministry m, BureaucratGrade grade, CivilServicePostParams prm)
        {
            if (m == null) return 0;
            switch (grade)
            {
                case BureaucratGrade.事務次官級: return prm.viceMinisterSlots;
                case BureaucratGrade.局長級: return prm.bureauChiefSlots;
                case BureaucratGrade.課長級: return prm.sectionChiefSlots;
                default: return Mathf.Max(0, m.staffSlots);
            }
        }

        /// <summary>その人物が今就いている省内職位（どこにも就いていなければ null）。人物は1省1職位。</summary>
        public static CivilServiceRecord FindServing(CivilServiceState st, int personId)
        {
            if (st == null || st.records == null || personId < 0) return null;
            for (int i = 0; i < st.records.Count; i++)
            {
                CivilServiceRecord r = st.records[i];
                if (r != null && r.personId == personId && r.IsServing) return r;
            }
            return null;
        }

        /// <summary>その省のその段の在任者数。</summary>
        public static int ServingCount(CivilServiceState st, int ministryId, BureaucratGrade grade)
        {
            int n = 0;
            if (st == null || st.records == null) return 0;
            for (int i = 0; i < st.records.Count; i++)
            {
                CivilServiceRecord r = st.records[i];
                if (r != null && r.IsServing && r.ministryId == ministryId && r.grade == grade) n++;
            }
            return n;
        }

        /// <summary>その省の在任者数（全段）。</summary>
        public static int ServingCount(CivilServiceState st, int ministryId)
        {
            int n = 0;
            if (st == null || st.records == null) return 0;
            for (int i = 0; i < st.records.Count; i++)
            {
                CivilServiceRecord r = st.records[i];
                if (r != null && r.IsServing && r.ministryId == ministryId) n++;
            }
            return n;
        }

        /// <summary>
        /// 省内職位に就いていることで何ができるか（状態は変えない）。どの段でも起案（政策提案）と事務の調整はできるが、
        /// 艦隊・軍団の作戦指揮権、国庫の直接支出、閣僚の任免、所管の政策決定・決裁は<b>生じない</b>
        /// （これらは内閣の政治任用＝<see cref="CabinetAppointmentRules.Authority"/> の領分）。
        /// </summary>
        public static AppointmentResult GradeAuthority(BureaucratGrade grade, CabinetAction action)
        {
            switch (action)
            {
                case CabinetAction.艦隊作戦指揮:
                    return AppointmentResult.Deny("省内職位は軍の指揮系統の外＝艦隊・軍団の作戦指揮権を含まない");
                case CabinetAction.国庫支出:
                    return AppointmentResult.Deny("省内職位は国庫を直接動かさない（予算・稟議の手続きによる）");
                case CabinetAction.閣僚任免:
                    return AppointmentResult.Deny("閣僚の任免は首相の権限＝事務方の職位には含まれない");
                case CabinetAction.所管政策決定:
                case CabinetAction.所管決裁:
                    return AppointmentResult.Deny(grade + " は事務方＝所管の" + action + "は大臣（または委任を受けた副大臣）が行う");
                default:
                    return AppointmentResult.Allow(grade + "（起案・事務の調整）");
            }
        }

        /// <summary>
        /// 省内職位に就けない理由（就けるなら null）：名簿に無い・死亡・拘束/不在・他勢力・在野・軍人・政治家。
        /// 人物は書き換えない。
        /// </summary>
        public static string PersonProblem(Person p, Faction f)
        {
            if (p == null) return "名簿に存在しない人物";
            if (p.IsDeceased) return "死亡";
            if (!p.IsAvailable) return "拘束・不在（" + p.captiveStatus + "）";
            if (p.faction != f) return "他勢力（" + p.faction + "）の人物";
            if (p.isFreeAgent) return "在野";
            if (p.role != PersonRole.文民) return "軍人（職業官僚の職位に就けない）";
            if (p.isPolitician) return "政治家（政治任用の職に就く）＝職業官僚の職位には就けない";
            return null;
        }

        /// <summary>
        /// 内閣人事局の承認権限（状態は変えない）。事務次官級は首相、局長級以下は所管大臣（所管決裁の委任を受けた副大臣を含む）。
        /// 権限外は上申先つきで返す。判定は <see cref="CabinetAppointmentRules"/> に委ね、役職や委任を台帳へ複製しない。
        /// </summary>
        public static AppointmentResult ApprovalAuthority(PoliticsState pol, Faction f, int actorId, int ministryId,
            BureaucratGrade grade, IList<Person> roster, int year)
        {
            if (grade == BureaucratGrade.事務次官級)
            {
                int premier = CabinetAppointmentRules.FormalPremier(pol, f, roster, out string premierProblem);
                if (premier < 0) return AppointmentResult.Deny("承認権者（内閣人事局＝首相）が不在：" + premierProblem);
                if (actorId != premier)
                    return AppointmentResult.Petition("権限外：事務次官級の人事を承認するのは内閣人事局＝首相（人物#" + premier + "）", premier);
                return AppointmentResult.Allow("首相（内閣人事局）が事務次官級の人事を承認");
            }

            CabinetState cab = pol != null ? pol.cabinet : null;
            CabinetPost minister = CabinetAppointmentRules.FindPost(cab, ministryId, CabinetPostKind.大臣);
            int to = minister != null ? minister.holderId : -1;
            if (to < 0)
                return AppointmentResult.Deny("承認権者（省#" + ministryId + " の所管大臣）が空席＝" + grade + " の人事を承認できない");
            AppointmentResult r = CabinetAppointmentRules.Authority(pol, f, actorId, ministryId, CabinetAction.所管決裁, roster, year);
            if (!r.ok)
                return AppointmentResult.Petition("権限外：" + grade + " の人事を承認するのは所管大臣（人物#" + to + "）＝" + r.reason, to);
            return AppointmentResult.Allow(grade + " の人事を承認（" + r.reason + "）");
        }

        // ===== 確認・実行 =====

        /// <summary>人事の確認（状態は変えない）。<see cref="Execute"/> と同じ判定を同じ順序で通る。</summary>
        public static AppointmentResult Check(PoliticsState pol, Faction f, int actorId, List<Ministry> tree, int ministryId,
            CivilServiceAction action, int personId, BureaucratGrade targetGrade, IList<Person> roster, int year,
            CivilServiceState st, CivilServicePostParams prm)
            => Core(pol, f, actorId, tree, ministryId, action, personId, targetGrade, roster, year, "", st, prm, true);

        /// <summary>
        /// 人事の実行（唯一の入口・AI もここを通す）。拒否のときは理由を返して何も変えない。
        /// 成功すると台帳（<see cref="CivilServiceState"/>）と省庁の配属（<see cref="Ministry.staffIds"/>）だけが動く。
        /// </summary>
        public static AppointmentResult Execute(PoliticsState pol, Faction f, int actorId, List<Ministry> tree, int ministryId,
            CivilServiceAction action, int personId, BureaucratGrade targetGrade, IList<Person> roster, int year,
            string reason, CivilServiceState st, CivilServicePostParams prm)
            => Core(pol, f, actorId, tree, ministryId, action, personId, targetGrade, roster, year, reason, st, prm, false);

        private static AppointmentResult Core(PoliticsState pol, Faction f, int actorId, List<Ministry> tree, int ministryId,
            CivilServiceAction action, int personId, BureaucratGrade targetGrade, IList<Person> roster, int year,
            string reason, CivilServiceState st, CivilServicePostParams prm, bool dryRun)
        {
            if (st == null) return AppointmentResult.Deny("人事台帳がない");
            // 欠けた台帳（records/history が null）の穴埋めは⑤の直前まで遅らせる
            // ＝確認（Check）と拒否される実行は台帳に一切触れない。読み取りは各所で null 安全。
            Ministry ministry = MinistryRules.Get(tree, ministryId);
            if (ministry == null) return AppointmentResult.Deny("存在しない省（#" + ministryId + "）＝" + action + "できない");

            Person p = ElectionCycleRules.FindPerson(roster, personId);
            string pp = PersonProblem(p, f);
            if (pp != null) return AppointmentResult.Deny("人事できない：" + pp);
            if (actorId == personId) return AppointmentResult.Deny("官僚本人が自分の人事を承認することはできない");

            // ① 職位の組み立て（在籍・重複・飛び級）
            CivilServiceRecord current = FindServing(st, personId);
            BureaucratGrade grade;      // 承認と資格を問う段
            switch (action)
            {
                case CivilServiceAction.配属:
                    if (current != null)
                        return AppointmentResult.Deny("既に " + GradeTitle(current.ministryName, current.grade)
                                                      + " に在任（人物は同時に1省1職位）＝異動で移す");
                    if (targetGrade != BureaucratGrade.一般官僚)
                        return AppointmentResult.Deny("配属は一般官僚から（" + targetGrade + " への飛び級の入省は認めない）");
                    grade = BureaucratGrade.一般官僚;
                    break;

                case CivilServiceAction.異動:
                    if (current == null) return AppointmentResult.Deny("どの省にも在籍していない＝異動できない（先に配属）");
                    if (current.ministryId == ministryId)
                        return AppointmentResult.Deny("既に " + (ministry.ministryName ?? "") + " に在籍している＝異動先が同じ");
                    grade = current.grade; // 異動は段を変えない（段を変えるなら昇任・降任）
                    break;

                case CivilServiceAction.昇任:
                case CivilServiceAction.降任:
                    if (current == null) return AppointmentResult.Deny("どの省にも在籍していない＝" + action + "できない");
                    if (current.ministryId != ministryId)
                        return AppointmentResult.Deny("当該省に在籍していない（在籍は省#" + current.ministryId + "）");
                    int step = (int)targetGrade - (int)current.grade;
                    if (action == CivilServiceAction.昇任 && step != 1)
                        return AppointmentResult.Deny("昇任は1つ上の段まで（" + current.grade + " → " + targetGrade + " は飛び級・据置・降格）");
                    if (action == CivilServiceAction.降任 && step != -1)
                        return AppointmentResult.Deny("降任は1つ下の段まで（" + current.grade + " → " + targetGrade + " は飛び越し・据置・昇格）");
                    grade = targetGrade;
                    break;

                default: // 解任
                    if (current == null) return AppointmentResult.Deny("どの省にも在籍していない＝解任できない");
                    if (current.ministryId != ministryId)
                        return AppointmentResult.Deny("当該省に在籍していない（在籍は省#" + current.ministryId + "）");
                    grade = current.grade;
                    break;
            }

            // ② 承認権限（内閣人事局）
            AppointmentResult auth = ApprovalAuthority(pol, f, actorId, ministryId, grade, roster, year);
            if (!auth.ok) return auth;

            // ③ 本人の資格（官位・考課・最低在職年）。降任・解任・異動は段が上がらないので問わない。
            if (action == CivilServiceAction.配属 || action == CivilServiceAction.昇任)
            {
                CourtRank need = RequiredRank(grade);
                if (JapaneseCourtRankRules.Compare(p.courtRank, need) < 0)
                    return AppointmentResult.Deny(grade + " に要る官位に届かない（要 " + need + "・現 " + p.courtRank + "）");
                float needMerit = RequiredMerit(grade, prm);
                if (needMerit > 0f)
                {
                    if (p.merit == null || !p.merit.HasRecord)
                        return AppointmentResult.Deny(grade + " には考課の記録が要る（未評定）");
                    if (p.merit.AverageScore < needMerit)
                        return AppointmentResult.Deny(grade + " に要る考第に届かない（要 " + needMerit.ToString("0.0")
                                                      + "・現 " + p.merit.AverageScore.ToString("0.0") + "）");
                }
                int needYears = RequiredTenureYears(grade, prm);
                if (needYears > 0)
                {
                    int tenure = current != null ? year - current.appointedYear : 0;
                    if (tenure < needYears)
                        return AppointmentResult.Deny(grade + " に要る在職年に届かない（要 " + needYears + "年・現 " + tenure + "年）");
                }
            }

            // ④ 空席・定員（一般官僚の枠は省の配属定員そのもの＝職位別の空席と二重に問わない）
            if (action != CivilServiceAction.解任)
            {
                if (grade != BureaucratGrade.一般官僚)
                {
                    int slots = SlotsFor(ministry, grade, prm);
                    if (ServingCount(st, ministryId, grade) >= slots)
                        return AppointmentResult.Deny(GradeTitle(ministry.ministryName, grade) + " に空席がない（定員 " + slots + "名）");
                }
                bool joining = action == CivilServiceAction.配属 || action == CivilServiceAction.異動;
                if (joining && ServingCount(st, ministryId) >= Mathf.Max(0, ministry.staffSlots))
                    return AppointmentResult.Deny((ministry.ministryName ?? "") + " の配属定員がいっぱい（" + ministry.staffSlots + "名）");
            }

            string title = GradeTitle(ministry.ministryName, grade);
            if (dryRun)
                return AppointmentResult.Allow(action == CivilServiceAction.解任
                    ? title + " の人物#" + personId + " を解任できる"
                    : "人物#" + personId + " を " + title + " へ" + action + "できる");

            // ⑤ 実行（台帳と省庁の配属だけを動かす）。ここで初めて欠けた台帳を用意する
            if (st.records == null) st.records = new List<CivilServiceRecord>();
            if (st.history == null) st.history = new List<CivilServiceRecord>();
            string why = string.IsNullOrEmpty(reason) ? action.ToString() : reason;
            if (current != null) End(st, current, year, EndStatusOf(action), why, prm);
            if (action != CivilServiceAction.解任)
            {
                Begin(st, ministry, personId, grade, year, actorId, why);
                MinistryRules.AssignOfficial(tree, ministryId, personId); // 単一所属（他省からは外れる）
            }
            else
            {
                MinistryRules.RemoveOfficial(tree, ministryId, personId);
            }
            return AppointmentResult.Allow(action + "：人物#" + personId + " を " + title + "（" + auth.reason + "）");
        }

        private static CivilServiceStatus EndStatusOf(CivilServiceAction action)
        {
            switch (action)
            {
                case CivilServiceAction.異動: return CivilServiceStatus.異動;
                case CivilServiceAction.昇任: return CivilServiceStatus.昇任;
                case CivilServiceAction.降任: return CivilServiceStatus.降任;
                default: return CivilServiceStatus.解任;
            }
        }

        // ===== 台帳の出し入れ =====

        private static void Begin(CivilServiceState st, Ministry m, int personId, BureaucratGrade grade, int year, int actorId, string reason)
        {
            st.records.Add(new CivilServiceRecord
            {
                ministryId = m.id,
                ministryName = m.ministryName ?? "",
                personId = personId,
                grade = grade,
                appointedYear = year,
                vacatedYear = 0,
                appointedById = actorId,
                reason = reason ?? "",
                status = CivilServiceStatus.在任
            });
            SortRecords(st.records);
        }

        private static void End(CivilServiceState st, CivilServiceRecord rec, int year, CivilServiceStatus status, string reason, CivilServicePostParams prm)
        {
            rec.status = status;
            rec.vacatedYear = year;
            rec.reason = reason ?? "";
            st.records.Remove(rec);
            AppendCapped(st, rec, prm);
        }

        /// <summary>退任記録を上限つきで積む（あふれた古い件数は <see cref="CivilServiceState.historyDropped"/> に数える＝黙って捨てない）。</summary>
        internal static void AppendCapped(CivilServiceState st, CivilServiceRecord rec, CivilServicePostParams prm)
        {
            if (st.history == null) st.history = new List<CivilServiceRecord>();
            st.history.Add(rec);
            while (st.history.Count > prm.maxHistory)
            {
                st.history.RemoveAt(0);
                st.historyDropped++;
            }
        }

        private static void SortRecords(List<CivilServiceRecord> list)
        {
            if (list == null) return;
            list.Sort(CompareRecords);
        }

        private static int CompareRecords(CivilServiceRecord a, CivilServiceRecord b)
        {
            if (a.ministryId != b.ministryId) return a.ministryId.CompareTo(b.ministryId);
            int ga = (int)b.grade, gb = (int)a.grade; // 段の高い順
            if (ga != gb) return ga.CompareTo(gb);
            return a.personId.CompareTo(b.personId);
        }

        // ===== 保存 =====

        /// <summary>
        /// セーブ読込の穴埋め（読込だけでは任命も解任もしない）：欠けた配列・null 文字列を整え、壊れた記録と
        /// 同一人物の重複在任（先頭＝省ID小・段の高い方を残す）を取り除く。
        /// </summary>
        public static void NormalizeLoaded(CivilServiceState st)
        {
            if (st == null) return;
            if (st.records == null) st.records = new List<CivilServiceRecord>();
            if (st.history == null) st.history = new List<CivilServiceRecord>();
            if (st.historyDropped < 0) st.historyDropped = 0;

            for (int i = st.records.Count - 1; i >= 0; i--)
            {
                CivilServiceRecord r = st.records[i];
                if (r == null || r.ministryId < 0 || r.personId < 0 || r.status != CivilServiceStatus.在任)
                {
                    st.records.RemoveAt(i);
                    continue;
                }
                if (r.ministryName == null) r.ministryName = "";
                if (r.reason == null) r.reason = "";
                r.vacatedYear = 0;
            }
            SortRecords(st.records);

            var seen = new HashSet<int>();
            for (int i = 0; i < st.records.Count; i++)
            {
                if (seen.Add(st.records[i].personId)) continue;
                st.records.RemoveAt(i); // 重複在任は落とす（1省1職位）。履歴には積まない＝起きなかった任用として扱う
                i--;
            }

            for (int i = st.history.Count - 1; i >= 0; i--)
            {
                CivilServiceRecord r = st.history[i];
                if (r == null) { st.history.RemoveAt(i); continue; }
                if (r.ministryName == null) r.ministryName = "";
                if (r.reason == null) r.reason = "";
                if (r.status == CivilServiceStatus.在任) r.status = CivilServiceStatus.退職;
            }
        }

        /// <summary>
        /// 台帳の在任者を省庁ツリーの配属（<see cref="Ministry.staffIds"/>）へ写す（読込後の再構築用）。
        /// 台帳に無い配属は外さない＝他の仕組みが入れた配属を壊さない。
        /// </summary>
        public static void SyncStaffing(List<Ministry> tree, CivilServiceState st)
        {
            if (tree == null || st == null || st.records == null) return;
            for (int i = 0; i < st.records.Count; i++)
            {
                CivilServiceRecord r = st.records[i];
                if (r == null || !r.IsServing) continue;
                Ministry m = MinistryRules.Get(tree, r.ministryId);
                if (m == null || m.staffIds.Contains(r.personId)) continue;
                MinistryRules.AssignOfficial(tree, r.ministryId, r.personId);
            }
        }
    }
}
