using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 死亡・捕虜・引退で空いた席の後任補充（LIFE-2 #152・継承）。新しい任免窓口は作らず <see cref="GovernmentRegistry"/>
    /// （役職）や候補プールに委譲する＝指揮系統/政府が「故人のまま固まる」のを防ぐ。後任は資格を満たす最適候補
    /// （既定＝最高階級 tier・同点は若い順）を選ぶが、<b>適任不在なら空席のまま</b>＝機能低下を許容（無痛補充にしない）。test-first。
    /// </summary>
    public static class VacancyRules
    {
        /// <summary>
        /// 候補から後任を1人選ぶ（資格 <paramref name="eligible"/> を満たし、任に就ける＝<see cref="ICharacter.IsAvailable"/> な者のうち
        /// 最高階級 tier。同点は <see cref="Person"/> 等の若さ（id 昇順で安定）で決める）。該当なしは null＝空席。
        /// </summary>
        public static ICharacter SelectSuccessor(IEnumerable<ICharacter> candidates, System.Func<ICharacter, bool> eligible)
        {
            if (candidates == null) return null;
            ICharacter best = null;
            foreach (ICharacter c in candidates)
            {
                if (c == null || !c.IsAvailable) continue;
                if (eligible != null && !eligible(c)) continue;
                if (best == null
                    || c.RankTier > best.RankTier
                    || (c.RankTier == best.RankTier && c.Id < best.Id))
                {
                    best = c;
                }
            }
            return best;
        }

        /// <summary>
        /// 役職の後任を補充する：保持者が任に就けない（死亡/捕虜）なら解任し、資格を満たす後任を任命する。
        /// 補充できたら true、空席のままなら false。任免権限・資格は <see cref="OfficeRules"/>/<see cref="GovernmentRegistry"/> に委譲。
        /// </summary>
        public static bool FillVacancy(Faction faction, Office office, IEnumerable<ICharacter> candidates, int scopeKey = 0)
        {
            if (office == null) return false;

            // 現保持者が任に就けないなら解任（空席化）
            ICharacter current = GovernmentRegistry.GetHolder(office, scopeKey);
            if (current != null && !current.IsAvailable)
                GovernmentRegistry.Dismiss(office, current, scopeKey);

            // 既に有資格者が就いていれば補充不要
            if (GovernmentRegistry.GetHolder(office, scopeKey) != null) return true;

            ICharacter successor = SelectSuccessor(candidates, c => OfficeRules.CanHold(c, office));
            if (successor == null) return false; // 適任不在＝空席のまま
            return GovernmentRegistry.TryAppoint(faction, office, successor, scopeKey);
        }

        /// <summary>
        /// 任命台帳から「任に就けなくなった保持者」を一掃する（死亡/捕虜）。戻り値＝空席になった (office, scopeKey)。
        /// 呼び出し側が <see cref="FillVacancy"/> で順に補充する想定。
        /// </summary>
        public static List<(Office office, int scopeKey, Faction faction)> ClearDeparted()
        {
            var vacated = new List<(Office, int, Faction)>();
            var snapshot = new List<GovernmentRegistry.Appointment>(GovernmentRegistry.Appointments);
            foreach (GovernmentRegistry.Appointment a in snapshot)
            {
                if (a.holder == null || !a.holder.IsAvailable)
                {
                    GovernmentRegistry.Dismiss(a.office, a.holder, a.scopeKey);
                    vacated.Add((a.office, a.scopeKey, a.faction));
                }
            }
            return vacated;
        }
    }
}
