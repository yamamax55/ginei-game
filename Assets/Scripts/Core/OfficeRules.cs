using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 役職の資格・権限の純ロジック（GOV-1 #142／GOV-3 #144・唯一の窓口）。就任資格（軍人専用/文民専用/
    /// 政治任用/必要階級）の判定と、保持役職から導く<b>提案権限</b>（所掌×スコープ）を扱う。
    /// 階級のみの旧経路は呼び出し側でフォールバックする前提＝ここは役職由来の範囲だけを判定する（後方互換）。test-first。
    /// </summary>
    public static class OfficeRules
    {
        /// <summary>人物が役職に就けるか（軍人/文民/政治任用の専用・必要階級）。</summary>
        public static bool CanHold(ICharacter c, Office o)
        {
            if (c == null || o == null) return false;
            if (o.militaryOnly && !c.IsMilitary) return false;
            if (o.civilianOnly && c.IsMilitary) return false;
            if (o.politicalAppointmentOnly && !c.IsPolitician) return false;
            if (o.requiredTier > 0 && c.RankTier < o.requiredTier) return false;
            return true;
        }

        /// <summary>保持スコープが対象スコープを包含するか（国家 ⊇ 方面 ⊇ 星系）。</summary>
        public static bool CoversScope(OfficeScope held, OfficeScope target)
        {
            // enum 値は 国家=0/方面=1/星系=2。値が小さいほど広い＝包含する。
            return (int)held <= (int)target;
        }

        /// <summary>保持役職のいずれかが指定の所掌×スコープを提案/決定できるか（元首は全所掌を包含）。</summary>
        public static bool CanPropose(IEnumerable<Office> heldOffices, OfficeDomain domain, OfficeScope scope)
        {
            if (heldOffices == null) return false;
            foreach (Office o in heldOffices)
            {
                if (o == null) continue;
                bool domainOk = (o.domain == domain) || (o.domain == OfficeDomain.元首);
                if (domainOk && CoversScope(o.scope, scope)) return true;
            }
            return false;
        }
    }
}
