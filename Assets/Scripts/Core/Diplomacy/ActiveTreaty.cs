using System.Collections.Generic;

namespace Ginei
{
    /// <summary>締結中の条約（DIPLO-4・#2119・純データ）。種別・当事者・締結年・期間（0=恒久）。</summary>
    public class ActiveTreaty
    {
        public TreatyType type;
        public string factionA = "";
        public string factionB = "";
        public int signedYear;
        public int durationYears; // 0 ＝ 恒久

        /// <summary>失効年（恒久は int.MaxValue）。</summary>
        public int ExpiresYear => durationYears <= 0 ? int.MaxValue : signedYear + durationYears;
    }

    /// <summary>締結中条約の台帳（DIPLO-4・#2119・static）。無向ペアで保持。Core 純ロジック・test-first。</summary>
    public static class TreatyLedger
    {
        static readonly List<ActiveTreaty> treaties = new List<ActiveTreaty>();

        public static IReadOnlyList<ActiveTreaty> All => treaties;

        static bool Pair(ActiveTreaty t, string a, string b)
            => (t.factionA == a && t.factionB == b) || (t.factionA == b && t.factionB == a);

        public static ActiveTreaty Find(string a, string b, TreatyType type)
        {
            for (int i = 0; i < treaties.Count; i++)
                if (treaties[i].type == type && Pair(treaties[i], a, b)) return treaties[i];
            return null;
        }

        public static ActiveTreaty FindAny(string a, string b)
        {
            for (int i = 0; i < treaties.Count; i++)
                if (Pair(treaties[i], a, b)) return treaties[i];
            return null;
        }

        public static void Add(ActiveTreaty t) { if (t != null) treaties.Add(t); }
        public static bool Remove(ActiveTreaty t) => treaties.Remove(t);
        public static void Clear() => treaties.Clear();
    }

    /// <summary>
    /// 条約管理の純ロジック（DIPLO-4・#2119）。締結・失効を扱い、status系条約（同盟/不可侵/属国）は外交状態へ反映、
    /// opinion効果は `TreatyRules.OpinionEffect`#191 へ委譲。通商/通行は状態非変更（opinion のみ）。test-first。
    /// </summary>
    public static class TreatyManagementRules
    {
        /// <summary>条約種別→外交状態（status系のみ。通商/通行は null）。</summary>
        public static DiplomacyState.DiplomaticStatus? StatusFor(TreatyType type)
        {
            switch (type)
            {
                case TreatyType.同盟: return DiplomacyState.DiplomaticStatus.同盟;
                case TreatyType.不可侵: return DiplomacyState.DiplomaticStatus.不可侵;
                case TreatyType.属国: return DiplomacyState.DiplomaticStatus.属国;
                default: return null; // 通商/通行
            }
        }

        /// <summary>
        /// 条約を締結。status系は外交状態を設定（交戦中は不可＝false）、opinion を条約効果ぶん増減、台帳へ登録。
        /// </summary>
        public static bool Sign(DiplomacyState state, TreatyType type, string a, string b, int year, int durationYears)
        {
            if (state == null) return false;
            var status = StatusFor(type);
            if (status.HasValue)
                if (!DiplomacyRules.SignTreaty(state, a, b, status.Value)) return false; // 交戦中などで失敗

            DiplomacyRules.AdjustOpinion(state, a, b, TreatyRules.OpinionEffect(type, TreatyRules.TreatyParams.Default));
            TreatyLedger.Add(new ActiveTreaty { type = type, factionA = a, factionB = b, signedYear = year, durationYears = durationYears });
            return true;
        }

        /// <summary>
        /// 失効した条約を除去し、status系は外交状態を平時へ戻す（現在その状態のままなら）。除去件数を返す。
        /// </summary>
        public static int ExpireDue(DiplomacyState state, int currentYear)
        {
            int removed = 0;
            var all = TreatyLedger.All;
            var expired = new List<ActiveTreaty>();
            for (int i = 0; i < all.Count; i++)
                if (currentYear >= all[i].ExpiresYear) expired.Add(all[i]);

            for (int i = 0; i < expired.Count; i++)
            {
                var t = expired[i];
                var status = StatusFor(t.type);
                if (status.HasValue && state != null)
                {
                    var e = state.GetEntry(t.factionA, t.factionB);
                    if (e != null && e.status == status.Value) e.status = DiplomacyState.DiplomaticStatus.平時;
                }
                if (TreatyLedger.Remove(t)) removed++;
            }
            return removed;
        }
    }
}
