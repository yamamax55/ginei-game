using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 適材適所の<b>最適任命</b>の純ロジック（人事最適化・唯一の窓口）。艦隊の自動編成（<see cref="FleetAutoOrganizeRules"/>＝
    /// 艦隊へ司令を貪欲割付）の<b>政府版</b>＝「候補者プール」を「空席の集合（<see cref="OfficeVacancy"/>）」へ<b>全体最適</b>で
    /// 割り当て、総適合度を最大化する（一人一職・一席一人）。1席ずつ最良を選ぶ <see cref="PersonRules.BestFor"/>／
    /// <see cref="VacancyRules.FillVacancy"/>／<see cref="CivilAppointmentRules.FillVacancy"/> に対し、本ルールは<b>複数席を同時に</b>
    /// 見て「良い人を席の取り合いで奪い合わない」配分を出す（要職に良い人が偏らず、全体で最も適所へ収まる）。
    ///
    /// <para>適合度（<see cref="FitForOffice"/>）＝役職の所掌に合う適性（軍務＝軍才／政務＝文才）×役割一致（<see cref="PersonRules.Effectiveness"/>）
    /// ×階級適正（必要 tier をどれだけ満たすか）×実績（<see cref="MeritEvaluationRules"/> の考第平均）。資格は <see cref="OfficeRules.CanHold"/> に委譲＝
    /// 不適格（階級不足・専用ゲート違反・役割違い）は割り当てない。</para>
    ///
    /// 決定論（同点は人物 id 昇順で安定）・null/空安全・基準値非破壊（実効値パターン）。状態は持たない
    /// （任命台帳の更新は <see cref="GovernmentRegistry.TryAppoint"/> へ＝Game 層が計画を実行）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AppointmentPlanRules
    {
        /// <summary>適合度の調整値（重み）。</summary>
        public readonly struct PlanParams
        {
            /// <summary>役職への素の適性（軍才/文才・役割一致込み）の重み。</summary>
            public readonly float aptitudeWeight;

            /// <summary>階級適正（必要 tier をどれだけ満たすか）の重み。</summary>
            public readonly float rankWeight;

            /// <summary>実績（考課の考第平均）の重み。</summary>
            public readonly float meritWeight;

            /// <summary>役割と所掌が不一致のときの実効力倍率（<see cref="PersonRules.PersonParams"/> へ渡す）。</summary>
            public readonly float mismatchPenalty;

            public PlanParams(float aptitudeWeight, float rankWeight, float meritWeight, float mismatchPenalty)
            {
                this.aptitudeWeight = Mathf.Max(0f, aptitudeWeight);
                this.rankWeight = Mathf.Max(0f, rankWeight);
                this.meritWeight = Mathf.Max(0f, meritWeight);
                this.mismatchPenalty = Mathf.Clamp01(mismatchPenalty);
            }

            /// <summary>既定＝適性0.6・階級0.2・実績0.2、ミスマッチ半減（0.5）。</summary>
            public static PlanParams Default => new PlanParams(0.6f, 0.2f, 0.2f, 0.5f);
        }

        /// <summary>tier 正規化の基準（tier 0..<see cref="TierNormalizer"/> を 0..1 へ。<see cref="RankSystem"/> の既定ラダー元帥=10 に合わせる）。</summary>
        public const float TierNormalizer = 10f;

        /// <summary>所掌が必要階級を欠く役職（requiredTier=0）の階級適正の既定値（中庸＝0.5）。</summary>
        public const float NeutralRankFit = 0.5f;

        /// <summary>役職の所掌（<see cref="OfficeDomain"/>）を人物適性の軸（<see cref="PostType"/>）へ写す＝軍事は軍務、その他は政務。</summary>
        public static PostType PostTypeOf(OfficeDomain domain)
            => domain == OfficeDomain.軍事 ? PostType.軍務 : PostType.政務;

        /// <summary>
        /// 階級適正（0..1）＝人物の階級 tier が役職の必要 tier をどれだけ満たすか。
        /// 必要 tier 未満は <see cref="OfficeRules.CanHold"/> で弾かれる前提だが、満たす範囲では「過不足のない適任」を高く評価する：
        /// 必要 tier 丁度=1.0、それより高位は緩やかに減点（過大ポストでない＝大物を雑用に回さない）。requiredTier=0 は <see cref="NeutralRankFit"/>。
        /// </summary>
        public static float RankFit(int personTier, int requiredTier)
        {
            if (requiredTier <= 0)
            {
                // 必要階級なし＝tier そのものを 0..1 化して「高位ほど有資格感」を薄く反映（中庸寄り）。
                float n = Mathf.Clamp01(personTier / TierNormalizer);
                return Mathf.Lerp(NeutralRankFit, 1f, n);
            }
            if (personTier < requiredTier) return 0f; // 階級不足（資格無し）
            int over = personTier - requiredTier;
            // 丁度=1.0、+1 ごとに 0.1 減（過大配置を僅かに嫌う）。下限 0.5。
            return Mathf.Clamp(1f - over * 0.1f, 0.5f, 1f);
        }

        /// <summary>実績（0..1）＝考課（<see cref="OfficialMerit"/>）の考第平均 1..9 を 0..1 へ。未評定は中庸 0.5。</summary>
        public static float MeritFit(OfficialMerit merit)
        {
            if (merit == null || !merit.HasRecord) return 0.5f;
            return Mathf.Clamp01(merit.AverageScore / 9f);
        }

        /// <summary>
        /// 人物の役職への<b>適合度</b>（0..1）。資格（<see cref="OfficeRules.CanHold"/>）を満たさなければ 0（割り当て不可）。
        /// 満たすときは 適性（役割一致込みの実効力を 0..1 化）×重み ＋ 階級適正×重み ＋ 実績×重み の重み付き和。
        /// </summary>
        public static float FitForOffice(Person p, Office office, PlanParams prm)
        {
            if (p == null || office == null) return 0f;
            if (!p.IsAvailable) return 0f;
            if (!OfficeRules.CanHold(p, office)) return 0f; // 資格ゲート（階級不足・専用違反）

            PostType post = PostTypeOf(office.domain);
            // 役割一致込みの実効力（0..100）→ 0..1。窓口＝PersonRules（適材適所の数値解決）。
            float eff = PersonRules.Effectiveness(p, post, new PersonRules.PersonParams(prm.mismatchPenalty));
            float aptitude = Mathf.Clamp01(eff / 100f);

            float rank = RankFit(p.rankTier, office.requiredTier);
            float merit = MeritFit(p.merit);

            float wSum = prm.aptitudeWeight + prm.rankWeight + prm.meritWeight;
            if (wSum <= 0f) return 0f;
            float score = (aptitude * prm.aptitudeWeight + rank * prm.rankWeight + merit * prm.meritWeight) / wSum;
            return Mathf.Clamp01(score);
        }

        /// <summary>適合度（既定パラメータ）。</summary>
        public static float FitForOffice(Person p, Office office) => FitForOffice(p, office, PlanParams.Default);

        /// <summary>
        /// 候補者プールを空席の集合へ<b>全体最適</b>で割り当てる（適材適所の一括最適化）。
        /// 全 (人物×空席) の適合度（×<see cref="OfficeVacancy.priority"/>）を計算し、適合度の高い組から貪欲に確定する
        /// （一人一職・一席一人＝stable greedy。適合度0＝資格無しは割り当てない）。同点は人物 id 昇順 → 役職 id 昇順で決定的。
        /// </summary>
        /// <param name="candidates">候補者（null/重複/不適格は無視）。</param>
        /// <param name="vacancies">埋める空席（office=null は無視）。</param>
        /// <param name="prm">適合度の重み。</param>
        public static AppointmentPlan Plan(IEnumerable<Person> candidates, IEnumerable<OfficeVacancy> vacancies, PlanParams prm)
        {
            var plan = new AppointmentPlan();
            if (candidates == null || vacancies == null) return plan;

            // 入力を確定リストへ（null/不正を除去）。
            var people = new List<Person>();
            foreach (Person p in candidates)
                if (p != null && p.IsAvailable && !people.Contains(p)) people.Add(p);

            var seats = new List<OfficeVacancy>();
            foreach (OfficeVacancy v in vacancies)
                if (v != null && v.office != null && !seats.Contains(v)) seats.Add(v);

            if (people.Count == 0 || seats.Count == 0) return plan;

            // 全ペアの (適合度×priority) を作る。
            var pairs = new List<Candidate>();
            for (int si = 0; si < seats.Count; si++)
            {
                OfficeVacancy seat = seats[si];
                float w = Mathf.Max(0f, seat.priority);
                for (int pi = 0; pi < people.Count; pi++)
                {
                    Person person = people[pi];
                    float fit = FitForOffice(person, seat.office, prm);
                    if (fit <= 0f) continue; // 資格無し＝候補にしない
                    pairs.Add(new Candidate(person, seat, pi, si, fit * w));
                }
            }

            // 適合度降順→人物 id 昇順→役職 id 昇順で安定ソート（決定論）。
            pairs.Sort(Compare);

            var usedPeople = new HashSet<int>();   // 人物 index
            var usedSeats = new HashSet<int>();    // 空席 index

            for (int i = 0; i < pairs.Count; i++)
            {
                Candidate c = pairs[i];
                if (usedPeople.Contains(c.personIndex) || usedSeats.Contains(c.seatIndex)) continue;
                usedPeople.Add(c.personIndex);
                usedSeats.Add(c.seatIndex);
                plan.assignments.Add(new AppointmentPlan.Assignment(c.person, c.seat, c.fitness));
                plan.totalFitness += c.fitness;
            }

            // 出力順も決定的に（適合度降順→人物 id 昇順）。
            plan.assignments.Sort(CompareAssignment);
            return plan;
        }

        /// <summary>一括最適化（既定パラメータ）。</summary>
        public static AppointmentPlan Plan(IEnumerable<Person> candidates, IEnumerable<OfficeVacancy> vacancies)
            => Plan(candidates, vacancies, PlanParams.Default);

        // --- 内部：ペア候補と決定的ソート ---

        private readonly struct Candidate
        {
            public readonly Person person;
            public readonly OfficeVacancy seat;
            public readonly int personIndex;
            public readonly int seatIndex;
            public readonly float fitness;

            public Candidate(Person person, OfficeVacancy seat, int personIndex, int seatIndex, float fitness)
            {
                this.person = person;
                this.seat = seat;
                this.personIndex = personIndex;
                this.seatIndex = seatIndex;
                this.fitness = fitness;
            }
        }

        private static int Compare(Candidate a, Candidate b)
        {
            int byFit = b.fitness.CompareTo(a.fitness); // 適合度降順
            if (byFit != 0) return byFit;
            int byPerson = a.person.id.CompareTo(b.person.id); // 人物 id 昇順
            if (byPerson != 0) return byPerson;
            int aOid = a.seat.office != null ? a.seat.office.id : 0;
            int bOid = b.seat.office != null ? b.seat.office.id : 0;
            int byOffice = aOid.CompareTo(bOid); // 役職 id 昇順
            if (byOffice != 0) return byOffice;
            return a.seat.scopeKey.CompareTo(b.seat.scopeKey);
        }

        private static int CompareAssignment(AppointmentPlan.Assignment a, AppointmentPlan.Assignment b)
        {
            int byFit = b.fitness.CompareTo(a.fitness);
            if (byFit != 0) return byFit;
            int ap = a.person != null ? a.person.id : 0;
            int bp = b.person != null ? b.person.id : 0;
            return ap.CompareTo(bp);
        }
    }
}
